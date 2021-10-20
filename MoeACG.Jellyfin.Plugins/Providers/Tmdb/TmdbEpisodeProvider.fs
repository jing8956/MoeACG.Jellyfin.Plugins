namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System
open System.Globalization
open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open MediaBrowser.Model.Entities
open MediaBrowser.Model.Providers
open TMDbLib.Objects.General
open TMDbLib.Objects.TvShows

type TmdbEpisodeProvider(httpClientFactory: IHttpClientFactory, tmdbClientManager: TmdbClientManager) =
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Episode, ItemLookupInfo> with
        member _.Name = TmdbUtils.ProviderName
        member this.GetSearchResults(searchInfo, cancellationToken) = 
            async {
                if searchInfo.IndexNumber.HasValue && searchInfo.ParentIndexNumber.HasValue then
                    let! metaResult = (this :> IRemoteMetadataProvider<Episode, ItemLookupInfo>).GetMetadata(searchInfo, cancellationToken) |> Async.AwaitTask
                    if metaResult.HasMetadata then
                        let item = metaResult.Item
                        let result =
                            new RemoteSearchResult(
                                IndexNumber = item.IndexNumber,
                                Name = item.Name,
                                ParentIndexNumber = item.ParentIndexNumber,
                                PremiereDate = item.PremiereDate,
                                ProductionYear = item.ProductionYear,
                                ProviderIds = item.ProviderIds,
                                SearchProviderName = (this :> IRemoteMetadataProvider<Episode, ItemLookupInfo>).Name,
                                IndexNumberEnd = item.IndexNumberEnd)
                        return Seq.singleton result
                    else return Seq.empty
                else return Seq.empty
            } |> Async.StartAsTask
        member _.GetMetadata(info, cancellationToken) = 
            async {
                let info = info :?> EpisodeInfo
                let result = new MetadataResult<Episode>()
                
                if info.IsMissingEpisode |> not then
                    let tmdbId = info.SeriesProviderIds.TryGetValue(MetadataProvider.Tmdb.ToString()) |> snd
                    let seriesTmdbId = System.Convert.ToInt32(tmdbId, CultureInfo.InvariantCulture)
                    if seriesTmdbId > 0 then
                        let seasonNumber = info.ParentIndexNumber
                        let episodeNumber = info.IndexNumber
                        if seasonNumber.HasValue && episodeNumber.HasValue then
                            let! epResult = 
                                tmdbClientManager.AsyncGetEpisode(
                                    seriesTmdbId, seasonNumber.Value, episodeNumber.Value, 
                                    info.MetadataLanguage, TmdbUtils.getImageLanguagesParam info.MetadataLanguage,
                                    cancellationToken)
                            
                            let writeResult (result: MetadataResult<Episode>) (epResult: TvEpisode) =
                                result.HasMetadata <- true
                                result.QueriedById <- true
                                if epResult.Overview |> String.IsNullOrEmpty |> not then
                                    result.ResultLanguage <- info.MetadataLanguage
                                let item = 
                                    new Episode(
                                        IndexNumber = info.IndexNumber,
                                        ParentIndexNumber = info.ParentIndexNumber,
                                        IndexNumberEnd = info.IndexNumberEnd,
                                        Name = epResult.Name,
                                        PremiereDate = epResult.AirDate,
                                        ProductionYear = (epResult.AirDate |> Nullable.map (fun d -> d.Year |> Nullable)),
                                        Overview = epResult.Overview,
                                        CommunityRating = Convert.ToSingle(epResult.VoteAverage)
                                    )

                                let writeIds (ids: ExternalIdsTvEpisode) =
                                    if ids.TvdbId |> String.IsNullOrEmpty |> not then
                                        item.SetProviderId(MetadataProvider.Tvdb, ids.TvdbId)
                                    if ids.ImdbId |> String.IsNullOrEmpty |> not then
                                        item.SetProviderId(MetadataProvider.Imdb, ids.TvdbId)
                                    if ids.TvrageId |> String.IsNullOrEmpty |> not then
                                        item.SetProviderId(MetadataProvider.TvRage, ids.TvdbId)
                                epResult.ExternalIds |> Obj.iter writeIds

                                epResult.Videos 
                                |> Obj.map (fun v -> v.Results)
                                |> Obj.iter (
                                    Seq.filter TmdbUtils.isTrailerType >> 
                                    Seq.map (fun v -> v.Key) >>
                                    Seq.map ((+) "https://www.youtube.com/watch?v=") >>
                                    Seq.iter item.AddTrailerUrl)

                                epResult |> tmdbClientManager.GetPersonsWithGuestStars |> Seq.iter result.AddPerson
                                result.Item <- item
                            epResult |> Obj.iter (writeResult result)

                return result
            } |> Async.StartAsTask
        member _.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
