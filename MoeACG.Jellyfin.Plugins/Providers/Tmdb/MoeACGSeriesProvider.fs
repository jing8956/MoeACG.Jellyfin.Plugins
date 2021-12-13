namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open System.Threading.Tasks
open System.Net.Http
open System.Text.RegularExpressions
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open MediaBrowser.Model.Entities
open Microsoft.Extensions.Logging
open TMDbLib.Objects.Find

type MoeACGSeriesProvider(
    httpClientFactory: IHttpClientFactory, 
    tmdbClientManager: TmdbClientManager,
    logger: ILogger<MoeACGSeriesProvider>) =

    // After TheTVDB
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Series, ItemLookupInfo> with 
        member _.Name = TmdbUtils.ProviderName
        member _.GetSearchResults(searchInfo, cancellationToken) = 
            Task.FromResult(Seq.empty)
        member _.GetMetadata(info, cancellationToken) =
            async {
                let result = new MetadataResult<Series>(HasMetadata = false, Item = new Series())
                let id = info.GetProviderId(MetadataProvider.Tmdb)
                if String.IsNullOrEmpty(id) then
                    let! findResult =
                        let tryFindByExternalId (provider:MetadataProvider) (source) =
                            async {
                                let succeed, id = info.TryGetProviderId(provider)
                                if succeed then
                                    let! searchResult = tmdbClientManager.AsyncFindByExternalId(
                                        id, source, info.MetadataLanguage, cancellationToken)
                                    return searchResult
                                    |> Option.ofObj
                                    |> Option.filter (fun r -> r.TvResults.Count > 0)
                                    |> Option.map (fun r -> r.TvResults.[0].Id.ToString("D"))
                                else return None
                            }
                        let trySearchByTmdb =
                            async {
                                let year = info.Year |> ValueOption.ofNullable |> ValueOption.defaultValue 0
                                let! searchResults = tmdbClientManager.AsyncSearchSeries(
                                    info.Name, info.MetadataLanguage, year, cancellationToken)

                                let id = 
                                    searchResults
                                    |> Seq.filter (fun tv -> tv.GenreIds.Contains(16))
                                    |> Seq.tryHead
                                    |> Option.map (fun tv -> tv.Id)

                                match id with
                                | Some id ->
                                    let! titles = tmdbClientManager.AsyncGetTvShowAlternativeTitles(id, cancellationToken)
                                    let ``type`` =
                                        titles.Results
                                        |> Seq.filter (fun t -> t.Title = info.Name)
                                        |> Seq.map (fun t -> t.Type)
                                        |> Seq.tryHead
                                    let ssNumber =
                                        let tryOneWhenNullOrWhiteSpace t =
                                            if String.IsNullOrWhiteSpace(t) then Some(1) else None

                                        let tryCastZhHansNumber (s:string) =
                                            let numberZhHansTable = "一二三四五六七八九十"
                                            numberZhHansTable.IndexOf(s)
                                            |> function | -1 -> None | i -> Some(i + 1)
                                        let tryGetZhHansNumber s =
                                            let g = Regex.Match(s, "第(?<s>.)季").Groups.["s"]
                                            if g.Success then Some(g.Value) else None

                                        let tryGetEnNumber s =
                                            let g = Regex.Match(s, "[Ss](?<s>\d+)").Groups.["s"]
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

                                    match ssNumber, ``type`` with
                                    | None, Some(t) ->
                                        logger.LogWarning("Can not parse '{Type}' to season number. SeriesName: {SeriesName}", t, info.Name)
                                    | _, None ->
                                        logger.LogWarning("Can not find type to season number. SeriesName: {SeriesName}", info.Name)
                                    | _ -> ()

                                    ssNumber
                                    |> Option.map (fun n -> n.ToString("D"))
                                    |> Option.iter (fun n -> result.Item.SetProviderId(TmdbUtils.SeasonNumber, n))
                                | None -> ()

                                return id |> Option.map (fun id -> id.ToString("D"))
                            }
                        let orElseWithAsync optionComputation ifNoneComputation =
                            async {
                                let! option = optionComputation
                                if option |> Option.isSome
                                then return option
                                else return! ifNoneComputation
                            }

                        seq {
                            tryFindByExternalId MetadataProvider.Imdb FindExternalSource.Imdb
                            tryFindByExternalId MetadataProvider.Tvdb FindExternalSource.TvDb
                            trySearchByTmdb
                        } |> Seq.fold orElseWithAsync (async.Return(None))

                    let setTmdbId id =
                        result.HasMetadata <- true
                        result.Item.SetProviderId(MetadataProvider.Tmdb, id)
                    findResult |> Option.iter setTmdbId

                return result
            } |> Async.StartAsTask
        member _.GetImageResponse(url, cancellationToken) =
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
