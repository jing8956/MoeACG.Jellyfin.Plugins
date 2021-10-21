namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System
open System.Linq
open Microsoft.Extensions.Caching.Memory
open TMDbLib.Client
open TMDbLib.Objects.General
open TMDbLib.Objects.TvShows
open MediaBrowser.Controller.Entities
open MediaBrowser.Model.Entities

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
    member _.AsyncGetEpisode(tvShowId, seasonNumber, episodeNumber, language, imageLanguages, cancellationToken) = 
        asyncGetOrRequest $"episode-{tvShowId}-s{seasonNumber}-e{episodeNumber}-{language}"
        <| fun client -> 
               client.GetTvEpisodeAsync(
                   tvShowId,
                   seasonNumber,
                   episodeNumber,
                   language = TmdbUtils.normalizeLanguage language,
                   includeImageLanguage = imageLanguages,
                   extraMethods = (
                       TvEpisodeMethods.Credits |||
                       TvEpisodeMethods.Images |||
                       TvEpisodeMethods.ExternalIds |||
                       TvEpisodeMethods.Videos),
                   cancellationToken = cancellationToken)
        
    member _.GetPosterUrl(posterPath) =
        getImageUrl tmDbClient.Config.Images.PosterSizes.Last posterPath 
    member _.GetBackdropUrl(backdropPath) =
        getImageUrl tmDbClient.Config.Images.BackdropSizes.Last backdropPath
    member _.GetDiscover() = tmDbClient.DiscoverTvShowsAsync()
    member _.GetStillUrl(filePath) =
        getImageUrl tmDbClient.Config.Images.StillSizes.Last filePath

    member inline private this.GetPersonsOfCast (personType) (cast: Cast seq) =
        let toPersonInfo (actor: Cast) =
            let personInfo =
                PersonInfo(
                    Name = actor.Name.Trim(),
                    Role = actor.Character,
                    Type = personType,
                    SortOrder = actor.Order,
                    ImageUrl = this.GetPosterUrl(actor.ProfilePath))
            if actor.Id > 0 then personInfo.SetProviderId(MetadataProvider.Tmdb, actor.Id.ToString("D"))
            personInfo
        cast
        |> Seq.sortBy (fun actor -> actor.Order) 
        |> Seq.truncate TmdbUtils.MaxCastMembers
        |> Seq.map toPersonInfo
    member inline private _.GetPersonsOfCrew(crews: Crew seq) =
        let keepTypes = [| PersonType.Director; PersonType.Writer; PersonType.Producer |]
        let isKeepType personType = keepTypes.Contains(personType, StringComparer.OrdinalIgnoreCase)
        let toPersonInfo (r: struct {| Type: string; Crew: Crew |}) =
            new PersonInfo(
                Name = r.Crew.Name.Trim(),
                Role = r.Crew.Job,
                Type = r.Type)
        crews
        |> Seq.map (fun crew -> struct {| Type = TmdbUtils.mapCrewToPersonType crew; Crew = crew |})
        |> Seq.filter (fun r -> r.Type |> isKeepType || r.Crew.Job |> isKeepType)
        |> Seq.map toPersonInfo
    member inline this.GetPersons(hasCredits) = 
        let toPersons (credits: Credits) =
            seq {
                credits.Cast |> Obj.map (this.GetPersonsOfCast PersonType.Actor)
                credits.Crew |> Obj.map this.GetPersonsOfCrew
            } |> Seq.map (Obj.defaultValue Seq.empty) |> Seq.concat
        (^T: (member Credits: Credits) hasCredits) |> Obj.map toPersons
    member inline this.GetPersonsWithGuestStars(hasCredits) =
        let toPersons (credits: CreditsWithGuestStars) =
            seq {
                credits.Cast |> Obj.map (this.GetPersonsOfCast PersonType.Actor)
                credits.GuestStars |> Obj.map (this.GetPersonsOfCast PersonType.GuestStar)
                credits.Crew |> Obj.map this.GetPersonsOfCrew
            } |> Seq.map (Obj.defaultValue Seq.empty) |> Seq.concat
        (^T: (member Credits: CreditsWithGuestStars) hasCredits) |> Obj.map toPersons

    interface IDisposable with 
        member _.Dispose() =
            memoryCache.Dispose()
            tmDbClient.Dispose()
