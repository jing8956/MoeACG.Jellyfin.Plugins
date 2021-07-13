module MoeACG.Jellyfin.Plugin.Resolver.Reslove

open System
open System.IO
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Model.Entities
open MoeACG.Jellyfin.Plugin

type Args = MediaBrowser.Controller.Library.ItemResolveArgs

let validate predicate message input = if predicate input then Ok input else Error message
let mapValidateFunction list = list |> List.map (fun p -> p ||> validate)

let isTvShows (args:Args) =
    let strEquals comparisonType b a = System.String.Equals(a, b, comparisonType)
    let ignoreCaseStrEquals = strEquals StringComparison.OrdinalIgnoreCase
    let isTvShowsStr = ignoreCaseStrEquals CollectionType.TvShows
    args.CollectionType |> isTvShowsStr
let isDirectory (args:Args) = args.IsDirectory
let hasParent<'TParent when 'TParent :> Folder> (args:Args) = args.HasParent<'TParent>()
let hasChildDirectory (args:Args) = 
    args.FileSystemChildren 
    |> Option.ofObj
    |> Option.map (Array.exists (fun meta -> meta.IsDirectory))
    |> Option.defaultValue false
let isChildEmpty (args:Args) = args.FileSystemChildren = null || args.FileSystemChildren |> Array.isEmpty
let isVideoFile (args:Args) = Array.contains (args.FileInfo.Extension.ToLower()) Plugin.Configuration.VideoExtensions

let reduceReasonResult switchList =
    let addReasonResult switch1 switch2 arg = 
        match switch1 arg, switch2 arg with
        | Ok _, Ok value -> Ok value
        | Error reason, Ok _ -> Error reason
        | Ok _, Error reason -> Error reason
        | Error reason1, Error reason2 -> Error(sprintf "%s\r\n%s" reason1 reason2)
    switchList |> List.reduce addReasonResult

let getPath (args:Args) = args.Path
let getFileName (path:string) = Path.GetFileName path

let matchInput (regexs:Regex seq) input = 
    regexs |> Seq.tryPick (
        fun regex -> 
            let m = regex.Match(input)
            if m.Success then Some m else None
        )
let isMatchValidate = validate Option.isSome "Match is failed." >> Result.map Option.get
let getGroupValue (groupname:string) (m:Match) = m.Groups.[groupname].Value

module Match =
    let private config() = Plugin.Configuration

    let seriesNameGroupName = config().SeriesNameGroupName
    let seasonNumberGroupName = config().SeasonNumberGroupName
    let specialSeasonGroupName = config().SpecialSeasonGroupName
    let episodeNumberGroupName = config().EpisodeNumberGroupName
    let episodeNameGroupName = config().EpisodeNameGroupName
    let subtitleOrganizationGroupName = config().SubtitleOrganizationGroupName

    let getSeriesNameGroupValue = getGroupValue (seriesNameGroupName)
    let private hanziNumberTable = "一二三四五六七八九";
    let getSeasonNumberGroupValue ssMatch = 
        let getSpecialSeasonGroupValue = getGroupValue (specialSeasonGroupName)
        let isSpecal = ssMatch |> getSpecialSeasonGroupValue |> (String.IsNullOrEmpty) |> not
        if isSpecal then 0
        else
            let strValue = ssMatch |> getGroupValue (seasonNumberGroupName)
            strValue |> (String.IsNullOrEmpty)
            |> function 
               | true -> -1
               | false -> 
                   match strValue.[0] with
                   | numChar when numChar >= '一' -> hanziNumberTable.IndexOf(numChar) + 1 // 汉字，编码非连续
                   | numChar when numChar >= '\u2160' -> int numChar - int '\u2160' + 1    // 罗马字[Ⅰ-Ⅹ]，编码是连续的
                   | numChar -> int numChar - int '1' + 1                                  // 阿拉伯数字[1-9]
                   
    let getEpisodeNumberGroupValue m = 
        let value = getGroupValue (episodeNumberGroupName) m
        if value |> (String.IsNullOrEmpty) then -1m else Decimal.Parse(value)
    let getEpisodeNameGroupName = getGroupValue episodeNameGroupName
    let getSubtitleOrganizationGroupValue = getGroupValue subtitleOrganizationGroupName
open Match
open System.Text

let logArgs (logger: ILogger<_>) (args:Args) =
    logger.LogDebug("Begin Resolve, Path: {0}", args.Path)
    args
let logResult (logger: ILogger<_>) (args:Args) toString result =
    match result with
    | Ok value -> logger.LogDebug("Resolve {0} succeed: {1}", args.Path, value |> toString)
    | Error reasons -> logger.LogDebug("Resolve {0} failed, because: {1}", args.Path, box reasons)
    result

let getResultValueOrNull = function | Ok value -> value | Error _ -> null

let createSeriesRegexs() = Plugin.Configuration.SeriesRegexs
let createSeasonRegexs() = Plugin.Configuration.SeasonRegexs
let createEpisodeRegexs() = Plugin.Configuration.EpisodeRegexs

let resloveBase validator binder logger toString args = 
    args |> logArgs logger |> validator |> Result.bind binder |> logResult logger args toString |> getResultValueOrNull
let resolveSeries logger args =
    let regexs = createSeriesRegexs()
    let validator =
        [
            isTvShows, $"CollectionType is not {CollectionType.TvShows}."
            isDirectory, "Is not Directory."
            hasParent<Series> >> not, $"Has {nameof Series} Parent."
            hasParent<Season> >> not, $"Has {nameof Season} Parent."
            isChildEmpty >> not, "Has no subfolders."
        ] |> mapValidateFunction |> reduceReasonResult
    let bindSeries (args:Args) =
        let path = args |> getPath
        let seriesMatch = path |> getFileName |> matchInput regexs

        let mapSeries (name:string) = new Series(Path = path, Name = name.Replace("‛", ""))
        seriesMatch |> isMatchValidate
        |> Result.map getSeriesNameGroupValue
        |> Result.bind (validate (String.IsNullOrEmpty >> not) $"Group \"{seriesNameGroupName}\" Value is null or empty.")
        |> Result.map (mapSeries)
    let seriesToString (series: Series) =
        let sb = new StringBuilder()
        sb.Append($"Name: {series.Name}") |> ignore
        sb.ToString()
    args |> resloveBase validator bindSeries logger seriesToString
let resolveSeason logger args =
    let regexs = createSeasonRegexs()
    let validator = 
        [
            isTvShows, $"CollectionType is not {CollectionType.TvShows}."
            isDirectory, "Is not Directory."
            hasParent<Series>, $"Has not {nameof Series} Parent."
            hasParent<Season> >> not, $"Has {nameof Season} Parent."
            isChildEmpty >> not, "Has no files."
            hasChildDirectory >> not, "Has subfolders."
        ] |> mapValidateFunction |> reduceReasonResult
    let bindSeason (args:Args) = 
        let parentSeries = 
            let rec findParent folder = 
                match box folder with
                | :? 'T as find -> find
                | :? Folder as folder -> findParent folder.Parent
                | _ -> null
            findParent args.Parent :> Series

        let path = args |> getPath
        let seasonMatch = path |> getFileName |> matchInput regexs

        let mapSeason seasonMatch = 
            let name = seasonMatch |> getSeriesNameGroupValue
            let number = seasonMatch |> getSeasonNumberGroupValue

            let season = new Season(SeriesId = parentSeries.Id)
            if name |> String.IsNullOrEmpty |> not then season.SeriesName <- name
            if number <> -1 then season.IndexNumber <- number
            season
        Ok(parentSeries)
        |> Result.bind (fun _ -> seasonMatch |> isMatchValidate)
        |> Result.map mapSeason
    let seasonToString (season: Season) =
        let sb = new StringBuilder()
        sb.AppendLine($"SeriesName: {season.SeriesName}") |> ignore
        sb.Append($"SeasonNumber: {season.IndexNumber}") |> ignore
        sb.ToString() 
    args |> resloveBase validator bindSeason logger seasonToString
let resolveEpisode logger args =
    let regexs = createEpisodeRegexs()
    let ssRegex = createSeasonRegexs()
    let validator = 
        [
            isTvShows, $"CollectionType is not {CollectionType.TvShows}."
            isDirectory >> not, "Is not file."
            isVideoFile, "Is not video file."
        ] |> mapValidateFunction |> reduceReasonResult
    let bindEpisode (args:Args) = 
        let parentSeries, parentSeason = 
            let rec findSeriesAndSeason folder =
                match box folder with
                | :? Series as si -> Some si, None
                | :? Season as ss -> Some ss.Series, Some ss
                | :? Folder as folder -> findSeriesAndSeason folder.Parent
                | _ -> None, None
            findSeriesAndSeason args.Parent

        let path = args |> getPath
        let episodeMatch = path |> getFileName |> matchInput regexs
        let mapEpisode episodeMatch = 
            let name = episodeMatch |> getSeriesNameGroupValue
            let epNumber = episodeMatch |> getEpisodeNumberGroupValue
            let epName = episodeMatch |> getEpisodeNameGroupName

            let ssNumber = episodeMatch |> getSeasonNumberGroupValue
            let ssNumber = if Decimal.Truncate(epNumber) <> epNumber then 0 else ssNumber

            let subtitleOrganization = episodeMatch |> getSubtitleOrganizationGroupValue

            let ep = new Episode()
            parentSeries
            |> Option.map Option.ofObj |> Option.flatten 
            |> Option.iter (fun series -> ep.SeriesId <- series.Id)
            if name |> (String.IsNullOrEmpty) |> not then ep.SeriesName <- name
            if ssNumber <> -1 then 
                ep.ParentIndexNumber <- ssNumber
            else if parentSeason |> Option.isSome then
                 ep.SeasonId <- parentSeason.Value.Id
            else if parentSeries |> Option.isSome then
                let parentFileName = parentSeries.Value.Path |> getFileName

                let ssMatch = parentFileName |> matchInput ssRegex
                ssMatch |> isMatchValidate
                |> Result.map (
                    fun ssMatch -> 
                        let ssNumber = ssMatch |> getSeasonNumberGroupValue
                        if ssNumber <> -1 then ep.ParentIndexNumber <- ssNumber
                ) |> ignore

            if epNumber <> -1m then ep.IndexNumber <- int32 epNumber
            if epName |> (String.IsNullOrEmpty) |> not then ep.Name <- epName
            if subtitleOrganization |> (String.IsNullOrEmpty) |> not then
                ep.AddTag($"字幕组:{subtitleOrganization}")
            ep

        parentSeries |> validate Option.isSome $"Has not {nameof Series} or {nameof Season} Parent."
        |> Result.bind (fun _ -> episodeMatch |> isMatchValidate)
        |> Result.map mapEpisode
    let episodeToString (episode: Episode) =
        let sb = new StringBuilder()
        sb.AppendLine($"SeriesName: {episode.SeriesName}") |> ignore
        sb.AppendLine($"Number: {episode.IndexNumber}") |> ignore
        sb.AppendLine($"Name: {episode.Name}") |> ignore
        sb.Append($"SeasonNumber: {episode.ParentIndexNumber}") |> ignore
        sb.ToString()
    args |> resloveBase validator bindEpisode logger episodeToString
    

