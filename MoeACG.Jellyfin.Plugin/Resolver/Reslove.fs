module MoeACG.Jellyfin.Plugin.Resolver.Reslove

open System
open System.IO
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Model.Entities
open MoeACG.Jellyfin.Plugin
open MediaBrowser.Controller.Entities.TV
open MoeACG.Jellyfin.Plugin.Configuration

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
let hasChildDirectory (args:Args) = args.FileSystemChildren |> Array.exists (fun meta -> meta.IsDirectory)
let isChildEmpty (args:Args) = args.FileSystemChildren |> Array.isEmpty

let reduceReasonResult switchList =
    let addReasonResult switch1 switch2 arg = 
        match switch1 arg, switch2 arg with
        | Ok _, Ok value -> Ok value
        | Error reason, Ok _ -> Error reason
        | Ok _, Error reason -> Error reason
        | Error reason1, Error reason2 -> Error(sprintf "%s\r\n%s" reason1 reason2)
    switchList |> List.reduce addReasonResult >> Result.mapError (sprintf "multiple reasons.\r\n%s")

let getPath (args:Args) = args.Path
let getDirectoryName (path:string) = Path.GetDirectoryName path
let getFileName (path:string) = Path.GetFileName path

let matchInput (regex:Regex) = regex.Match
let isSuccess (m:Match) = m.Success
let isSuccessValidate = validate isSuccess "Match is failed."
let getGroupValue (groupname:string) (m:Match) = m.Groups.[groupname].Value

let numberGroupName = Plugin.Configuration.SeasonNumberGroupName
let specialGroupName = Plugin.Configuration.SpecialSeasonGroupName

module Match =
    let private config() = Plugin.Configuration

    let seriesNameGroupName = config().SeriesNameGroupName
    let seasonNumberGroupName = config().SeasonNumberGroupName
    let specialSeasonGroupValue = config().SpecialSeasonGroupName
    let episodeNumberGroupName = config().EpisodeNumberGroupName

    let getSeriesNameGroupValue = getGroupValue (config().SeriesNameGroupName)
    let private hanziNumberTable = "一二三四五六七八九";
    let getSeasonNumberGroupValue ssMatch = 
        let getSpecialSeasonGroupValue = getGroupValue (config().SpecialSeasonGroupName)
        let isSpecal = ssMatch |> getSpecialSeasonGroupValue |> (String.IsNullOrEmpty) |> not
        if isSpecal then 0
        else
            let strValue = ssMatch |> getGroupValue (config().SeasonNumberGroupName)
            strValue |> (String.IsNullOrEmpty)
            |> function 
               | true -> -1
               | false -> 
                   match strValue.[0] with
                   | numChar when numChar >= '一' -> hanziNumberTable.IndexOf(numChar) + 1
                   | numChar when numChar >= 'Ⅰ' -> int numChar - int 'Ⅰ' + 1
                   | numChar -> int numChar - int '1' + 1

    let getEpisodeNumberGroupValue = getGroupValue (config().EpisodeNumberGroupName)
open Match
open System

let logResult (logger: ILogger<_>) result =
    if logger.IsEnabled(LogLevel.Trace) then
        match result with
        | Ok value ->
            let options = new System.Text.Json.JsonSerializerOptions(WriteIndented = true)
            let json = System.Text.Json.JsonSerializer.Serialize(value, options)
            logger.LogTrace("Resolve succeed: {0}", json)
        | Error reasons -> logger.LogTrace("Resolve failed, because: {0}", box reasons)
    result
let getResultValueOrNull = function | Ok value -> value | Error _ -> null

let resloveBase validator binder logger = 
    validator >> Result.bind binder >> logResult logger >> getResultValueOrNull
let resolveSeries logger regex args =
    let validator =
        [
            isTvShows, $"CollectionType is not {CollectionType.TvShows}."
            isDirectory, "Is not Directory."
            hasParent<Series> >> not, $"Has {nameof Series} Parent."
            hasParent<Season> >> not, $"Has {nameof Season} Parent."
            hasChildDirectory,"Has no subfolders."
        ] |> mapValidateFunction |> reduceReasonResult
    let bindSeries (args:Args) =
        let path = args |> getPath
        let seriesMatch = path |> getDirectoryName |> matchInput regex

        seriesMatch |> isSuccessValidate
        |> Result.map getSeriesNameGroupValue
        |> Result.bind (validate (String.IsNullOrEmpty) $"Group \"{seriesNameGroupName}\" Value is null or empty.")
        |> Result.map (fun name -> new Series(Path = path, Name = name))
    args |> resloveBase validator bindSeries logger
let resolveSeason logger regex args =
    let validator = 
        [
            isTvShows, $"CollectionType is not {CollectionType.TvShows}."
            isDirectory, "Is not Directory."
            hasParent<Season> >> not, $"Has {nameof Season} Parent."
            hasChildDirectory >> not, "Has subfolders."
            isChildEmpty, "Has no files."
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
        let seasonMatch = path |> getDirectoryName |> matchInput regex

        let mapSeason _ = 
            let name = seasonMatch |> getSeriesNameGroupValue
            let number = seasonMatch |> getSeasonNumberGroupValue

            let season = new Season(SeriesId = parentSeries.Id)
            if name |> (String.IsNullOrEmpty) |> not then season.Name <- name
            if number <> -1 then season.IndexNumber <- number
            season
        parentSeries |> validate (isNull >> not) $"Has not {nameof Series} Parent."
        |> Result.bind (fun _ -> seasonMatch |> isSuccessValidate)
        |> Result.map mapSeason
        
    args |> resloveBase validator bindSeason logger
let resolveEpisode logger regex args =
    let validator = [] |> mapValidateFunction |> reduceReasonResult
    let bindEpisode (args:Args) = 
        let _ = "" |> matchInput regex
        Ok(new Episode())
    args |> resloveBase validator bindEpisode logger
    
let createRegexBase pattern = new Regex(pattern, Plugin.Configuration.DefaultRegexOptions)
let createSeriesRegex()  = Plugin.Configuration.SeriesPattern  |> createRegexBase
let createSeasonRegex()  = Plugin.Configuration.SeasonPattern  |> createRegexBase
let createEpisodeRegex() = Plugin.Configuration.EpisodePattern |> createRegexBase
