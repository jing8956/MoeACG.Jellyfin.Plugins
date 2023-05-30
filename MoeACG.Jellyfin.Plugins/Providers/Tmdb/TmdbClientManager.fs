namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open Microsoft.Extensions.Caching.Memory
open TMDbLib.Client
open System.Threading.Tasks
open Microsoft.Extensions.Logging

type TmdbClientManager(memoryCache: IMemoryCache, logger: ILogger<TmdbClientManager>) = 
    let [<Literal>] CacheDurationInHours = 1.0
    let tmDbClient = new TMDbClient(TmdbUtils.ApiKey)
    // Not really interested in NotFoundException
    do tmDbClient.ThrowApiExceptions <- true

    let ensureClientConfigAsync =
        task {
            if not tmDbClient.HasConfig then do! tmDbClient.GetConfigAsync() :> Task
        } |> ValueTask
    let getOrRequestCoreAsync key factory =
        task {
            match memoryCache.TryGetValue<'T>(key) with
            | true, value -> return value
            | _ ->
                do! ensureClientConfigAsync
                match! factory() with
                | null -> return null
                | value ->
                    memoryCache.Set(key, value, TimeSpan.FromHours(CacheDurationInHours)) |> ignore
                    return value
        }
    let getOrRequestAsync key factory = getOrRequestCoreAsync key (fun() -> tmDbClient |> factory) 

    member _.SearchSeriesAsync(name, language, year, cancellationToken) =
        task {
           logger.LogDebug("Enter AsyncSearchSeries: name '{Name}', language: '{Language}', year: '{Year}'.", name, language, year)
           let! result = getOrRequestAsync $"searchseries-{name}-{language}" (
               fun client ->
                   client.SearchTvShowAsync(
                       query = name,
                       language = language,
                       includeAdult = true,
                       firstAirDateYear = year,
                       cancellationToken = cancellationToken))
           return result.Results
        }
    member _.FindByExternalIdAsync(externalId, source, language, cancellationToken) =
        task {
            logger.LogDebug("Enter AsyncFindByExternalId: externalId '{ExternalId}', source '{Source}', language '{Language}'.", externalId, source, language)
            return! getOrRequestAsync $"find-{source}-{externalId}-{language}" (
                fun client ->
                    client.FindAsync(
                       source = source,
                       id = externalId,
                       language = TmdbUtils.normalizeLanguage language,
                       cancellationToken = cancellationToken))
        }
    member _.GetTvShowAlternativeTitlesAsync(id, cancellationToken) =
        task {
            logger.LogDebug("Enter AsyncGetTvShowAlternativeTitles: id '{Id}'.", id :> obj)
            return! getOrRequestAsync $"tv-{id}-alternative-titles" (
                fun client -> client.GetTvShowAlternativeTitlesAsync(id, cancellationToken))
        }


    member _.GetTvShowsAsync(id, method, cancellationToken) =
        task {
            logger.LogDebug("Enter AsyncGetTvShows: id '{Id}', method '{Method}'.", id :> obj, method)
            return! getOrRequestAsync $"tv-{id}-{method}" (
                fun client -> client.GetTvShowAsync(id, method, cancellationToken = cancellationToken)
            )
        }
    member _.GetTvEpisodeGroupsAsync(id, language, cancellationToken) =
        task {
            logger.LogDebug("Enter AsyncGetTvEpisodeGroups: id '{Id}'.", id :> obj)
            return! getOrRequestAsync $"tv-{id}-episode-groups " (
                fun client -> client.GetTvEpisodeGroupsAsync(id, language, cancellationToken)
            )
        }

    interface IDisposable with 
        member _.Dispose() =
            memoryCache.Dispose()
            tmDbClient.Dispose()
