namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open System.Linq
open Microsoft.Extensions.Caching.Memory
open TMDbLib.Client
open TMDbLib.Objects.General
open TMDbLib.Objects.TvShows
open MediaBrowser.Controller.Entities
open MediaBrowser.Model.Entities
open System.Net.Http
open System.Net.Http.Json

[<AllowNullLiteral>]
type AlternativeTitle(iso_3166_1: string, title: string, ``type``: string) =
    member _.Iso_3166_1 = iso_3166_1
    member _.Title = title
    member _.Type = ``type``

[<AllowNullLiteral>]
type AlternativeTitles(id: int, results: AlternativeTitle[]) =
    member _.Id = id
    member _.Results = results

type TmdbClientManager(client: HttpClient, memoryCache: IMemoryCache) = 
    let [<Literal>] CacheDurationInHours = 1.0
    let tmDbClient = new TMDbClient(TmdbUtils.ApiKey)
    // Not really interested in NotFoundException
    do tmDbClient.ThrowApiExceptions <- false

    let asyncEnsureClientConfig = 
        async {
            if not tmDbClient.HasConfig then
                do! tmDbClient.GetConfigAsync() |> Async.AwaitTask |> Async.Ignore
        }
    let asyncGetOrRequestCore key factory =
        async {
            let mutable value = Unchecked.defaultof<'T>
            if memoryCache.TryGetValue<'T>(key, &value) then return value
            else
                do! asyncEnsureClientConfig
                let! value = factory() |> Async.AwaitTask
                if value |> isNull |> not then 
                    memoryCache.Set(key, value, TimeSpan.FromHours(CacheDurationInHours)) |> ignore
                return value
        }
    let asyncGetOrRequest key factory = 
        asyncGetOrRequestCore key (fun() -> tmDbClient |> factory)

    member _.AsyncSearchSeries(name, language, year, cancellationToken) =
        asyncGetOrRequest $"searchseries-{name}-{language}"
        <| fun client ->
                client.SearchTvShowAsync(
                    query = name,
                    language = language,
                    firstAirDateYear = year,
                    cancellationToken = cancellationToken) 
        |> (fun computation -> async { let! searchResults = computation in return searchResults.Results })
    member _.AsyncFindByExternalId(externalId, source, language, cancellationToken) = 
        asyncGetOrRequest $"find-{source}-{externalId}-{language}"
        <| fun client ->
               client.FindAsync(
                   source = source,
                   id = externalId,
                   language = TmdbUtils.normalizeLanguage language,
                   cancellationToken = cancellationToken)

    member _.AsyncGetTvShowAlternativeTitles(id, cancellationToken) =
        asyncGetOrRequestCore $"tv-{id}-alternative-titles"
        <| fun() -> 
            client.GetFromJsonAsync<AlternativeTitles>(
                $"https://api.themoviedb.org/3/tv/{id}/alternative_titles?api_key={TmdbUtils.ApiKey}", 
                cancellationToken)

    interface IDisposable with 
        member _.Dispose() =
            memoryCache.Dispose()
            tmDbClient.Dispose()
