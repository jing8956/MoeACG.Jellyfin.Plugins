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

type MoeACGSeriesProvider(
    httpClientFactory: IHttpClientFactory, 
    tmdbClientManager: TmdbClientManager,
    logger: ILogger<MoeACGSeriesProvider>) =

    static let [<Literal>] regexOptions = RegexOptions.Compiled ||| RegexOptions.ExplicitCapture

    // After TheTVDB
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Series, ItemLookupInfo> with 
        member _.Name = TmdbUtils.ProviderName
        member _.GetSearchResults(searchInfo, cancellationToken) = Task.FromResult(Seq.empty)
        member _.GetMetadata(info, cancellationToken) =
            task {
                let result = new MetadataResult<Series>(HasMetadata = false, Item = new Series())
                let id = info.GetProviderId(MetadataProvider.Tmdb)
                if String.IsNullOrEmpty(id) |> not then return result else

                let year = info.Year |> ValueOption.ofNullable |> ValueOption.defaultValue 0
                let tryCastZhHansNumber (s:string) =
                    let numberZhHansTable = "一二三四五六七八九十"
                    numberZhHansTable.IndexOf(s)
                    |> function | -1 -> None | i -> Some(i + 1)
                let mutable name, ssNumber =
                    match Regex.Match(info.Name, "(?<n>.+?)第(?<s>.)季", regexOptions) with
                    | m when m.Success ->
                        let n = m.Groups.["n"].Value.TrimEnd(' ')
                        let s = m.Groups.["s"].Value
                        n, tryCastZhHansNumber s
                    | _ -> info.Name, None
                        
                let! searchResults = tmdbClientManager.SearchSeriesAsync(name, info.MetadataLanguage, year, cancellationToken)

                let mutable id = 
                    searchResults
                    |> Seq.filter (fun tv -> tv.GenreIds.Contains(16))
                    |> Seq.tryHead
                    |> Option.map (fun tv -> tv.Id)

                if id.IsNone then // 例：星期一的丰满-周一的奶子
                    match Regex.Match(name, ".+(?=-)", regexOptions) with
                    | m when m.Success ->
                        name <- m.Value
                        let! searchResults = tmdbClientManager.SearchSeriesAsync(name, info.MetadataLanguage, year, cancellationToken)
                        id <-
                            searchResults
                            |> Seq.filter (fun tv -> tv.GenreIds.Contains(16))
                            |> Seq.tryHead
                            |> Option.map (fun tv -> tv.Id)
                    | _ -> ()

                if id.IsNone then // 例：Sound!Euphonium2
                   match Regex.Match(name, "(?<n>.*)(?<s>\d+)", regexOptions) with
                   | m when m.Success ->
                       name <- m.Groups.["n"].Value
                       let! searchResults = tmdbClientManager.SearchSeriesAsync(name, info.MetadataLanguage, year, cancellationToken)
                       id <-
                           searchResults
                           |> Seq.filter (fun tv -> tv.GenreIds.Contains(16))
                           |> Seq.tryHead
                           |> Option.map (fun tv -> tv.Id)
                       ssNumber <- Some(Int32.Parse(m.Groups.["s"].Value))
                   | _ -> ()

                match id with
                | Some id ->
                    let! titles = tmdbClientManager.GetTvShowAlternativeTitlesAsync(id, cancellationToken)
                    let ``type`` =
                        titles.Results
                        |> Seq.filter (fun t -> String.Equals(t.Title, name, StringComparison.InvariantCultureIgnoreCase))
                        |> Seq.map (fun t -> t.Type)
                        |> Seq.tryHead
                    let ssNumber =
                        match ssNumber with
                        | Some _ -> ssNumber
                        | None ->
                            let tryOneWhenNullOrWhiteSpace t =
                                if String.IsNullOrWhiteSpace(t) then Some(1) else None
            
                            let tryGetZhHansNumber s =
                                let g = Regex.Match(s, "第(?<s>.)季", regexOptions).Groups.["s"]
                                if g.Success then Some(g.Value) else None
            
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
                            
                    match ssNumber, ``type`` with
                    | None, Some(t) ->
                        logger.LogWarning("Can not parse '{Type}' to season number. SeriesName: {SeriesName}", t, info.Name)
                    | _, None ->
                        logger.LogWarning("Can not find type to season number. SeriesName: {SeriesName}", info.Name)
                    | _ -> ()
            
                    ssNumber
                    |> Option.map (fun n -> n.ToString("D"))
                    |> Option.iter (fun n -> result.Item.SetProviderId(TmdbUtils.SeasonNumber, n))

                    result.HasMetadata <- true
                    result.Item.SetProviderId(MetadataProvider.Tmdb, id.ToString("D"))
                | None -> ()

                return result
            }
        member _.GetImageResponse(url, cancellationToken) =
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
