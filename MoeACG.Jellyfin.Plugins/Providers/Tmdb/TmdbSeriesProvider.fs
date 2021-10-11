namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System
open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open MediaBrowser.Model.Entities
open MediaBrowser.Model.Providers
open TMDbLib.Objects.Find
open TMDbLib.Objects.Search
open TMDbLib.Objects.TvShows

module private TmdbSeriesProvider =
    let inline mapToRemoteSearchResult (tmdbClientManager: TmdbClientManager) series =
        let remoteResult = 
            let name = (^T: (member Name: string) series)
            new RemoteSearchResult(
                Name = (if name |> isNull then (^T: (member OriginalName: string) series) else name),
                SearchProviderName = TmdbUtils.ProviderName,
                ImageUrl = ((^T: (member PosterPath: string) series) |> tmdbClientManager.GetPosterUrl),
                Overview = (^T: (member Overview: string) series)
            )
        let id = (^T: (member Id: int) series)
        remoteResult.SetProviderId(MetadataProvider.Tmdb, $"{id:D}")
        let mutable premiereDate = (^T: (member FirstAirDate: DateTime Nullable) series)
        if premiereDate.HasValue then
            premiereDate <- premiereDate.Value.ToUniversalTime() |> Nullable
        remoteResult.PremiereDate <- premiereDate
        remoteResult

open TmdbSeriesProvider

type TmdbSeriesProvider(httpClientFactory: IHttpClientFactory, tmdbClientManager: TmdbClientManager) =
    let mapTvShowToRemoteSearchResult (series: TvShow) = 
        let remoteResult = mapToRemoteSearchResult tmdbClientManager series
        if series.ExternalIds |> isNull |> not then
            let inline setIdIfNotNull (provider:MetadataProvider) value =
                if value |> String.IsNullOrEmpty |> not then
                    remoteResult.SetProviderId(provider, value)
            setIdIfNotNull MetadataProvider.Imdb series.ExternalIds.ImdbId
            setIdIfNotNull MetadataProvider.Tvdb series.ExternalIds.TvdbId
        remoteResult
    let mapTvShowToSeries (seriesResult: TvShow) (preferredCountryCode: string) =
        let series =
            new Series(
                Name = seriesResult.Name,
                OriginalTitle = seriesResult.OriginalName,
                Overview = seriesResult.Overview,
                CommunityRating = float32 seriesResult.VoteAverage,
                HomePageUrl = seriesResult.Homepage,
                RunTimeTicks = (
                    seriesResult.EpisodeRunTime 
                    |> Seq.map (int64 >> (*) TimeSpan.TicksPerMinute) 
                    |> Seq.tryHead
                    |> Option.map Nullable
                    |> Option.defaultValue (Nullable())),
                PremiereDate = seriesResult.FirstAirDate)
        series.SetProviderId(MetadataProvider.Tmdb, seriesResult.Id.ToString("D"))

        let inline getName item = (^T: (member Name: string) item)
        seriesResult.Networks
        |> Obj.map (Seq.map getName)
        |> Obj.map Array.ofSeq
        |> Obj.iter (fun v -> series.Studios <- v)
        seriesResult.Genres
        |> Obj.map (Seq.map getName)
        |> Obj.map Array.ofSeq
        |> Obj.iter (fun v -> series.Genres <- v)
        seriesResult.Keywords
        |> Obj.map (fun keywords -> keywords.Results)
        |> Obj.map (Seq.map getName)
        |> Obj.iter (Seq.iter series.AddTag)

        if String.Equals(seriesResult.Status, "Ended", StringComparison.OrdinalIgnoreCase) then
            series.Status <- SeriesStatus.Ended
            series.EndDate <- seriesResult.LastAirDate
        else
            series.Status <- SeriesStatus.Continuing

        seriesResult.ExternalIds
        |> Obj.iter (
            fun ids ->
                let inline setIdIfNotNullOrWhiteSpace (provider: MetadataProvider) value =
                    if String.IsNullOrWhiteSpace(value) |> not then series.SetProviderId(provider, value)
                setIdIfNotNullOrWhiteSpace MetadataProvider.Imdb ids.ImdbId
                setIdIfNotNullOrWhiteSpace MetadataProvider.TvRage ids.TvrageId
                setIdIfNotNullOrWhiteSpace MetadataProvider.Tvdb ids.TvdbId)
            
        let contentRatings = seriesResult.ContentRatings.Results
        let rec getRating i result =
            if i >= contentRatings.Count then result else
            let rating = contentRatings.[i]
            let inline (|Our|Us|Other|) isoStr =
                if String.Equals(isoStr, preferredCountryCode, StringComparison.OrdinalIgnoreCase) then Our else
                if String.Equals(isoStr, "US", StringComparison.OrdinalIgnoreCase) then Us else Other
            match rating.Iso_3166_1 with
            | Our -> Some(rating.Rating)
            | Us -> Some(rating.Rating) |> getRating (i + 1) 
            | Other -> Some(rating.Rating) |> Option.orElse result |> getRating (i + 1) 
        let rating = getRating 0 None
        rating |> Option.iter (fun rating -> series.OfficialRating <- rating)

        let addTrailerUrl results =
            results
            |> Seq.filter TmdbUtils.isTrailerType 
            |> Seq.map (fun video -> $"https://www.youtube.com/watch?v={video.Key}") 
            |> Seq.iter (series.AddTrailerUrl)
        seriesResult.Videos
        |> Obj.map (fun videos -> videos.Results)
        |> Obj.iter addTrailerUrl

        series
    let mapSearchTvToRemoteSearchResult (series: SearchTv) = mapToRemoteSearchResult tmdbClientManager series
    let mapSearchTvToRemoteSearchResultWithProvider (provider:MetadataProvider) value series = 
        let result = mapSearchTvToRemoteSearchResult series
        result.SetProviderId(provider, value)
        result

    // After TheTVDB
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Series, ItemLookupInfo> with 
        member _.Name = TmdbUtils.ProviderName
        member _.GetSearchResults(searchInfo, cancellationToken) = 
            async {
                let mutable id = Unchecked.defaultof<string>
                let inline tryGetProviderId provider = searchInfo.TryGetProviderId(provider = provider, id = &id)
                let tryFindResultsByTmdbId =
                    async {
                        if tryGetProviderId MetadataProvider.Tmdb then
                            let! series = 
                                tmdbClientManager.AsyncGetSeries(
                                    tmdbId = int id,
                                    language = searchInfo.MetadataLanguage,
                                    imageLanguages = searchInfo.MetadataLanguage,
                                    cancellationToken = cancellationToken)
                            if series |> isNull then 
                                return None
                            else 
                                return series |> mapTvShowToRemoteSearchResult |> Seq.singleton |> Some
                        else return None
                    }
                let tryFindResultsByExternalId provider source =
                    async {
                        if tryGetProviderId provider then
                            let! findResult = 
                                tmdbClientManager.AsyncFindByExternalId(
                                    externalId = id,
                                    source = source,
                                    language = searchInfo.MetadataLanguage,
                                    cancellationToken = cancellationToken)
                            if findResult |> isNull then return None
                            else if findResult.TvResults |> isNull then return None
                            else 
                                let mapSearchTvToRemoteSearchResult = mapSearchTvToRemoteSearchResultWithProvider provider id
                                return findResult.TvResults |> Seq.map mapSearchTvToRemoteSearchResult |> Some
                        else return None
                    }
                let searchResults = 
                    async {
                        let! tvSearchResults = 
                            tmdbClientManager.AsyncSearchSeries(
                                name = searchInfo.Name,
                                language = searchInfo.MetadataLanguage,
                                year = 0,
                                cancellationToken = cancellationToken)
                        return tvSearchResults |> Seq.map (mapSearchTvToRemoteSearchResult)
                    }
                return! [
                   tryFindResultsByTmdbId
                   tryFindResultsByExternalId MetadataProvider.Imdb FindExternalSource.Imdb
                   tryFindResultsByExternalId MetadataProvider.Tvdb FindExternalSource.TvDb
                ] |> List.reduce (fun fst snd -> async { match! fst with | Some v -> return Some v | None -> return! snd }) 
                |> fun computation -> async { match! computation with | Some v -> return v | None -> return! searchResults }
            } |> Async.StartAsTask
        member _.GetMetadata(info, cancellationToken) = 
            async {
                let result = new MetadataResult<Series>(QueriedById = true)
                let mutable tmdbId = info.GetProviderId(MetadataProvider.Tmdb)
                let mutable tempId = Unchecked.defaultof<string>
                if tmdbId |> String.IsNullOrEmpty && info.TryGetProviderId(MetadataProvider.Imdb, &tempId) then
                    let! searchResult = tmdbClientManager.AsyncFindByExternalId(tempId, FindExternalSource.Imdb, info.MetadataLanguage, cancellationToken)
                    if searchResult |> isNull |> not && searchResult.TvResults.Count > 0 then
                        tmdbId <- searchResult.TvResults.[0].Id.ToString("D")
                if tmdbId |> String.IsNullOrEmpty && info.TryGetProviderId(MetadataProvider.Tvdb, &tempId) then
                    let! searchResult = tmdbClientManager.AsyncFindByExternalId(tempId, FindExternalSource.TvDb, info.MetadataLanguage, cancellationToken)
                    if searchResult |> isNull |> not && searchResult.TvResults.Count > 0 then
                        tmdbId <- searchResult.TvResults.[0].Id.ToString("D")
                if tmdbId |> String.IsNullOrEmpty then
                    result.QueriedById <- false
                    let! searchResults = tmdbClientManager.AsyncSearchSeries(info.Name, info.MetadataLanguage, info.Year |> Option.ofNullable |> Option.defaultValue 0, cancellationToken)
                    if searchResults.Count > 0 then 
                        tmdbId <- searchResults.[0].Id.ToString("D")

                if tmdbId |> String.IsNullOrEmpty |> not then
                    cancellationToken.ThrowIfCancellationRequested()
                    let! tvShow = 
                        tmdbClientManager.AsyncGetSeries(
                            int tmdbId, 
                            info.MetadataLanguage, 
                            TmdbUtils.getImageLanguagesParam info.MetadataLanguage,
                            cancellationToken)

                    result.Item <- mapTvShowToSeries tvShow info.MetadataCountryCode
                    result.ResultLanguage <- info.MetadataLanguage |> Obj.defaultValue tvShow.OriginalLanguage
                    tvShow |> TmdbUtils.getPersons tmdbClientManager |> Seq.iter result.AddPerson
                    result.HasMetadata <- result.Item |> isNull |> not

                return result
            } |> Async.StartAsTask
        member _.GetImageResponse(url, cancellationToken) =
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)