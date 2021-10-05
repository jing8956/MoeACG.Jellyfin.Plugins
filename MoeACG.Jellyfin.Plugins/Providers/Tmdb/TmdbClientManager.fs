namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System
open System.Collections.Generic
open System.Globalization
open System.Threading
open System.Linq
open Microsoft.Extensions.Caching.Memory
open TMDbLib.Client
open TMDbLib.Objects.Find
open TMDbLib.Objects.General
open TMDbLib.Objects.Search
open TMDbLib.Objects.TvShows
open System.Threading.Tasks

type TmdbClientManager(memoryCache: IMemoryCache) = 
    let [<Literal>] CacheDurationInHours = 1.0
    let tmDbClient = new TMDbClient(TmdbUtils.ApiKey)
    // Not really interested in NotFoundException
    do tmDbClient.ThrowApiExceptions <- false

    let asyncEnsureClientConfig = 
        async {
            if not tmDbClient.HasConfig then
                do! tmDbClient.GetConfigAsync() |> Async.AwaitTask |> Async.Ignore
        }
    let asyncGetOrRequest key factory = 
        async {
            let mutable value = Unchecked.defaultof<'T>
            if memoryCache.TryGetValue<'T>(key, &value) then return value
            else
                do! asyncEnsureClientConfig
                let! value = tmDbClient |> factory |> Async.AwaitTask
                if value |> isNull |> not then 
                    memoryCache.Set(key, value, TimeSpan.FromHours(CacheDurationInHours)) |> ignore
                return value
        }
    let getImageUrl sizeThunk path = 
        if path |> String.IsNullOrEmpty then null
        else tmDbClient.GetImageUrl(sizeThunk(), path).ToString()

    member _.AsyncGetSeries(tmdbId, language, imageLanguages, cancellationToken) =
        asyncGetOrRequest $"series-{tmdbId}-{language}"
        <| fun client -> 
               client.GetTvShowAsync(
                   id = tmdbId,
                   language = TmdbUtils.normalizeLanguage language,
                   includeImageLanguage = imageLanguages,
                   extraMethods = (
                       TvShowMethods.Credits |||
                       TvShowMethods.Images |||
                       TvShowMethods.ExternalIds |||
                       TvShowMethods.ContentRatings |||
                       TvShowMethods.Keywords |||
                       TvShowMethods.Videos |||
                       TvShowMethods.EpisodeGroups),
                   cancellationToken = cancellationToken)
    member _.AsyncFindByExternalId(externalId, source, language, cancellationToken) = 
        asyncGetOrRequest $"find-{source}-{externalId}-{language}"
        <| fun client ->
               client.FindAsync(
                   source = source,
                   id = externalId,
                   language = TmdbUtils.normalizeLanguage language,
                   cancellationToken = cancellationToken)
    member _.AsyncSearchSeries(name, language, year, cancellationToken) =
        asyncGetOrRequest $"searchseries-{name}-{language}"
        <| fun client ->
                client.SearchTvShowAsync(
                    query = name,
                    language = language,
                    firstAirDateYear = year,
                    cancellationToken = cancellationToken) 
        |> (fun computation -> async { let! searchResults = computation in return searchResults.Results })

    member _.AsyncGetSeason(tvShowId, seasonNumber, language, imageLanguages, cancellationToken) =
        asyncGetOrRequest $"season-{tvShowId}-s{seasonNumber}-{language}"
        <| fun client ->
               client.GetTvSeasonAsync(
                   tvShowId,
                   seasonNumber,
                   language = TmdbUtils.normalizeLanguage language,
                   includeImageLanguage = imageLanguages,
                   extraMethods = (
                       TvSeasonMethods.Credits |||
                       TvSeasonMethods.Images |||
                       TvSeasonMethods.ExternalIds |||
                       TvSeasonMethods.Videos ),
                   cancellationToken = cancellationToken)
        
    member _.GetPosterUrl(posterPath) =
        getImageUrl tmDbClient.Config.Images.PosterSizes.Last posterPath 
    member _.GetBackdropUrl(backdropPath) =
        getImageUrl tmDbClient.Config.Images.BackdropSizes.Last backdropPath
    member _.GetDiscover() = tmDbClient.DiscoverTvShowsAsync()

    interface IDisposable with 
        member _.Dispose() =
            memoryCache.Dispose()
            tmDbClient.Dispose()
