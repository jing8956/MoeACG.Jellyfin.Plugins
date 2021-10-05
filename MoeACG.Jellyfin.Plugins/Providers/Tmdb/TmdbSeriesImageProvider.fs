namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System
open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open MediaBrowser.Model.Entities
open MediaBrowser.Model.Extensions
open MediaBrowser.Model.Providers
open TMDbLib.Objects.General
open MediaBrowser.Model.Dto

type TmdbSeriesImageProvider(httpClientFactory: IHttpClientFactory, tmdbClientManager: TmdbClientManager) = 
    // After tvdb and fanart
    interface IHasOrder with member _.Order = 2
    interface IRemoteImageProvider with
        member _.Name = TmdbUtils.ProviderName
        member _.Supports(item) = item :? Series
        member _.GetSupportedImages(_) = seq { ImageType.Primary; ImageType.Backdrop }
        member x.GetImages(item, cancellationToken) =
            async {
                let tmdbId = item.GetProviderId(MetadataProvider.Tmdb)
                if tmdbId |> String.IsNullOrEmpty then 
                    return Seq.empty
                else
                    let language = item.GetPreferredMetadataLanguage()
                    let! series = tmdbClientManager.AsyncGetSeries(int tmdbId, null, null, cancellationToken)
                    let toRemoteImageInfos (images:Images) =
                        let inline ofImageData getUrlThunk imageType (data: ImageData) =
                            new RemoteImageInfo(
                                Url = getUrlThunk data.FilePath,
                                CommunityRating = data.VoteAverage,
                                VoteCount = data.VoteCount,
                                Width = data.Width,
                                Height = data.Height,
                                ProviderName = (x :> IRemoteImageProvider).Name,
                                Type = imageType,
                                RatingType = RatingType.Score)
                        let ofPosters data = 
                            let info = ofImageData tmdbClientManager.GetPosterUrl ImageType.Primary data
                            info.Language <- TmdbUtils.adjustImageLanguage data.Iso_639_1 language
                            info
                        let ofBackdrops = ofImageData tmdbClientManager.GetBackdropUrl ImageType.Backdrop
                        seq {
                            images.Posters |> Seq.map ofPosters
                            images.Backdrops |> Seq.map ofBackdrops
                        } |> Seq.concat
                    return series
                    |> Obj.map (fun series -> series.Images)
                    |> Obj.map toRemoteImageInfos
                    |> Obj.defaultValue Seq.empty
                    |> fun images -> images.OrderByLanguageDescending(language)
            } |> Async.StartAsTask
        member _.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
