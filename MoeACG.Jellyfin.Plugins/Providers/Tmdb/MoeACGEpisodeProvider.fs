namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open System.Net.Http
open System.Text.RegularExpressions
open System.Threading.Tasks
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open Microsoft.FSharp.Linq.NullableOperators
open MoeACG.Jellyfin.Plugins
open MediaBrowser.Model.Entities

type MoeACGEpisodeProvider(
    episodeRegexsProvider: EpisodeRegexsProvider, 
    tmdbClientManager: TmdbClientManager,
    httpClientFactory: IHttpClientFactory) =

    static let [<Literal>] regexOptions = RegexOptions.Compiled ||| RegexOptions.ExplicitCapture

    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Episode, ItemLookupInfo> with
        member _.Name = TmdbUtils.ProviderName
        member _.GetSearchResults(_searchInfo, _cancellationToken) = Seq.empty |> Task.FromResult
        member _.GetMetadata(info, cancellationToken) =
            task {
                let info = info :?> EpisodeInfo
                let result = new MetadataResult<Episode>(HasMetadata = false)

                let fileName = info.Path |> System.IO.Path.GetFileName
                match Regex.Match(fileName, "^【動畫瘋】(?<n>.*)\[(?<i>\d+)]", regexOptions) with
                | m when m.Success -> // 针对动画疯特别设置的剧集组，解决一切奇葩问题，例：東京喰種：re 第二季
                    let name = m.Groups.["n"].Value
                    let i = m.Groups.["i"].Value
                    match info.SeriesProviderIds.TryGetValue(MetadataProvider.Tmdb.ToString()) with
                    | (true, id) ->
                        let! tvShows = tmdbClientManager.GetTvShowsAsync(int id, TMDbLib.Objects.TvShows.TvShowMethods.EpisodeGroups, cancellationToken)
                        let tvGroupId =
                            tvShows.EpisodeGroups.Results
                            |> Seq.where (fun g -> g.Name = "巴哈姆特動畫瘋")
                            |> Seq.map (fun g -> g.Id)
                            |> Seq.tryHead
                        match tvGroupId with
                        | Some groupId ->
                            let! epGroup = tmdbClientManager.GetTvEpisodeGroupsAsync(groupId, info.MetadataLanguage, cancellationToken)
                            let group = epGroup.Groups |> Seq.tryFind (fun g -> g.Name.StartsWith(name))
                            match group with
                            | Some g -> 
                                let order = int i - 1
                                let ep = g.Episodes |> Seq.tryFind (fun e -> e.Order = order)
                                match ep with
                                | Some ep ->
                                    result.HasMetadata <- true
                                    info.ParentIndexNumber <- ep.SeasonNumber
                                    info.IndexNumber <- ep.EpisodeNumber
                                | _ -> ()
                            | _ ->
                                let! alternatives = tmdbClientManager.GetTvShowAlternativeTitlesAsync(int id, cancellationToken)
                                let ``type`` = alternatives.Results |> Seq.where (fun a -> a.Title = name) |> Seq.map (fun a -> a.Type) |> Seq.tryHead
                                let index =
                                    let tryOneWhenNullOrWhiteSpace t = if String.IsNullOrWhiteSpace(t) then Some(1) else None
            
                                    let tryGetZhHansNumber s =
                                        let g = Regex.Match(s, "第(?<s>.)季", regexOptions).Groups.["s"]
                                        if g.Success then Some(g.Value) else None

                                    let tryCastZhHansNumber (s:string) =
                                        let numberZhHansTable = "一二三四五六七八九十"
                                        numberZhHansTable.IndexOf(s)
                                        |> function | -1 -> None | i -> Some(i + 1)
            
                                    let tryGetEnNumber s =
                                        let g = Regex.Match(s, "[Ss](eason ?)?(?<s>\d+)", regexOptions).Groups.["s"]
                                        if g.Success then Some(g.Value) else None
            
                                    let tryValueWhenContains (test:string) v (s:string) =
                                        if s.Contains(test) then Some(v) else None

                                    seq {
                                        ``type``
                                        |> Option.bind tryOneWhenNullOrWhiteSpace
            
                                        ``type``
                                        |> Option.bind tryGetZhHansNumber
                                        |> Option.bind tryCastZhHansNumber
            
                                        ``type``
                                        |> Option.bind tryGetEnNumber
                                        |> Option.map int
            
                                        ``type``
                                        |> Option.bind (tryValueWhenContains "third" 3)
                                    } |> Seq.reduce (fun t1 t2 -> t1 |> Option.orElse t2)
                                match index with
                                | Some order ->
                                    let group = epGroup.Groups |> Seq.tryFind (fun g -> g.Order = order)
                                    match group with
                                    | Some g ->
                                        let order = int i - 1
                                        let ep = g.Episodes |> Seq.tryFind (fun e -> e.Order = order)
                                        match ep with
                                        | Some ep ->
                                            result.HasMetadata <- true
                                            info.ParentIndexNumber <- ep.SeasonNumber
                                            info.IndexNumber <- ep.EpisodeNumber
                                        | _ -> ()
                                    | _ -> ()
                                | _ -> ()
                        | _ -> ()
                    | _ -> ()
                    ()
                | _ -> ()

                if not result.HasMetadata && info.ParentIndexNumber ?= 1 then
                    let ofTry (b, v) = if b then Some(v) else None

                    info.SeriesProviderIds.TryGetValue(TmdbUtils.SeasonNumber)
                    |> ofTry
                    |> Option.map (fun s -> Int32.TryParse(s))
                    |> Option.bind ofTry
                    |> Option.iter (fun ss -> 
                        result.HasMetadata <- true
                        result.Item <- new Episode()
                        result.Item.ParentIndexNumber <- ss)

                if not result.HasMetadata then
                    let tryGetValue (name:string) (m:Match) =
                        let group = m.Groups.[name]
                        if group.Success then ValueSome group.Value else ValueNone
                    let setResult m =
                        // 集数
                        tryGetValue "i" m
                        |> ValueOption.map int
                        |> ValueOption.iter (fun i -> info.IndexNumber <- i)

                        let numberZhHansTable = "一二三四五六七八九十"
                        let tryCastNumber (s:string) = 
                            numberZhHansTable.IndexOf(s)
                            |> function | -1 -> ValueNone | i -> ValueSome(i + 1)
                            |> ValueOption.orElseWith (fun() ->
                                match Int32.TryParse(s) with
                                | (true, v) -> ValueSome(v)
                                | _ -> ValueNone)

                        // 季数
                        tryGetValue "s" m
                        |> ValueOption.bind tryCastNumber
                        |> ValueOption.iter (fun i -> info.ParentIndexNumber <- i)
                    episodeRegexsProvider.EpisodeRegexs
                    |> Seq.map (fun r -> r.Match(fileName))
                    |> Seq.tryFind (fun m -> m.Success)
                    |> Option.iter setResult

                return result
            }
        member _.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
