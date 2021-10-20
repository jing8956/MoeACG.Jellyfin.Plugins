namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open MediaBrowser.Model.Dto
open MediaBrowser.Model.Entities
open MediaBrowser.Model.Extensions
open MediaBrowser.Model.Providers
open TMDbLib.Objects.General

type TmdbEpisodeImageProvider(httpClientFactory: IHttpClientFactory, tmdbClientManager: TmdbClientManager) =
    interface IHasOrder with member _.Order = 1
    interface IRemoteImageProvider with
        member _.Name = TmdbUtils.ProviderName
        member _.Supports(item) = item :? Episode
        member _.GetSupportedImages(_) = seq { ImageType.Primary }
        member x.GetImages(item, cancellationToken) = 
            async {
                let episode = item :?> Episode
                let series = episode |> Obj.map (fun season -> season.Series)
                let seriesTmdbId = 
                    series 
                    |> Obj.map (fun series -> series.GetProviderId(MetadataProvider.Tmdb))
                    |> Obj.defaultValue "0"
                    |> int

                if seriesTmdbId <= 0 || 
                   episode |> Obj.map (fun season -> season.ParentIndexNumber) |> Nullable.hasValue |> not ||
                   episode |> Obj.map (fun season -> season.IndexNumber) |> Nullable.hasValue |> not then
                    return Seq.empty
                else
                    let language = item.GetPreferredMetadataLanguage()
                    let! epResult = 
                        tmdbClientManager.AsyncGetEpisode(
                            seriesTmdbId, 
                            episode.ParentIndexNumber.Value, 
                            episode.IndexNumber.Value, null, null, cancellationToken)

                    let toRemoteImageInfos (images: StillImages) =
                        let inline ofImageData getUrlThunk imageType (data: ImageData) =
                            new RemoteImageInfo(
                                Url = tmdbClientManager.GetStillUrl(data.FilePath),
                                CommunityRating = data.VoteAverage,
                                VoteCount = data.VoteCount,
                                Width = data.Width,
                                Height = data.Height,
                                Language = (TmdbUtils.adjustImageLanguage (data.Iso_639_1) language),
                                ProviderName = (x :> IRemoteImageProvider).Name,
                                Type = imageType,
                                RatingType = RatingType.Score)
                        let ofPosters data = 
                            let info = ofImageData tmdbClientManager.GetPosterUrl ImageType.Primary data
                            info.Language <- TmdbUtils.adjustImageLanguage data.Iso_639_1 language
                            info
                        images.Stills |> Seq.map ofPosters
                    return epResult
                    |> Obj.map (fun seasonResult -> seasonResult.Images)
                    |> Obj.map toRemoteImageInfos
                    |> Obj.defaultValue Seq.empty
                    |> fun images -> images.OrderByLanguageDescending(language)
            } |> Async.StartAsTask
        member this.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
