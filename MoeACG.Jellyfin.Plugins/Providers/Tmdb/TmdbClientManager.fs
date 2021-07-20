namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System
open System.Globalization
open System.Threading
open Microsoft.Extensions.Caching.Memory
open TMDbLib.Client
open TMDbLib.Objects.TvShows

type TmdbClientManager(memoryCache: IMemoryCache) = 
    let [<Literal>] CacheDurationInHours = 1.0
    let tmDbClient = new TMDbClient(TmdbUtils.ApiKey)
    // Not really interested in NotFoundException
    do tmDbClient.ThrowApiExceptions <- false
    let AsyncEnsureClientConfig = 
        async {
            if not tmDbClient.HasConfig then
                do! tmDbClient.GetConfigAsync() |> Async.AwaitTask |> Async.Ignore
        }

    member _.GetSeriesAsync(tmdbId: int, language: string, imageLanguages: string, cancellationToken: CancellationToken) =
        async {
            let key = $"series-{tmdbId.ToString(CultureInfo.InvariantCulture)}-{language}";
            let mutable series: TvShow = null
            if memoryCache.TryGetValue(key, &series) then 
                return series
            else
                do! AsyncEnsureClientConfig
                let! series = 
                    tmDbClient.GetTvShowAsync(
                        tmdbId, 
                        language = TmdbUtils.normalizeLanguage language, 
                        includeImageLanguage = imageLanguages,
                        extraMethods = (TvShowMethods.Credits ||| TvShowMethods.Images ||| TvShowMethods.Keywords ||| TvShowMethods.ExternalIds ||| TvShowMethods.Videos ||| TvShowMethods.ContentRatings ||| TvShowMethods.EpisodeGroups),
                        cancellationToken = cancellationToken
                    ) |> Async.AwaitTask

                if series <> null then
                    memoryCache.Set(key, series, TimeSpan.FromHours(CacheDurationInHours)) |> ignore

                return series
        } |> Async.StartAsTask

    interface IDisposable with 
        member _.Dispose() =
            memoryCache.Dispose()
            tmDbClient.Dispose();
