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

type TmdbSeasonImageProvider(httpClientFactory: IHttpClientFactory, tmdbClientManager: TmdbClientManager) =
    interface IHasOrder with member _.Order = 2
    interface IRemoteImageProvider with
        member _.Name = TmdbUtils.ProviderName
        member _.Supports(item) = item :? Season
        member _.GetSupportedImages(_) = seq { ImageType.Primary }
        member x.GetImages(item, cancellationToken) =
            async {
                let season = item :?> Season
                let series = season |> Obj.map (fun season -> season.Series)
                let seriesTmdbId = 
                    series 
                    |> Obj.map (fun series -> series.GetProviderId(MetadataProvider.Tmdb))
                    |> Obj.defaultValue "0"
                    |> int

                if seriesTmdbId <= 0 || season |> Obj.map (fun season -> season.IndexNumber) |> Nullable.hasValue |> not then
                    return Seq.empty
                else
                    let language = item.GetPreferredMetadataLanguage()
                    let! seasonResult = 
                        tmdbClientManager.AsyncGetSeason(seriesTmdbId, season.IndexNumber.Value, null, null, cancellationToken)

                    let toRemoteImageInfos (images: PosterImages) =
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
                        images.Posters |> Seq.map ofPosters
                    return seasonResult
                    |> Obj.map (fun seasonResult -> seasonResult.Images)
                    |> Obj.map toRemoteImageInfos
                    |> Obj.defaultValue Seq.empty
                    |> fun images -> images.OrderByLanguageDescending(language)
            } |> Async.StartAsTask
        member _.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
