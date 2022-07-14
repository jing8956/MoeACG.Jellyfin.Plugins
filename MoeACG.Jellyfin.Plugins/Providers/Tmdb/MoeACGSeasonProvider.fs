namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open System.Net.Http
open System.Threading.Tasks
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Providers
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Model.Entities
open TMDbLib.Objects.TvShows

type MoeACGSeasonProvider(httpClientFactory: IHttpClientFactory, tmdbClientManager: TmdbClientManager) =
    interface IRemoteMetadataProvider<Season, ItemLookupInfo> with
        member _.Name = TmdbUtils.ProviderName
        member _.GetSearchResults(_searchInfo, _cancellationToken) = Seq.empty |> Task.FromResult
        member _.GetMetadata(info, cancellationToken) =
            task {
                let result = new MetadataResult<Season>(HasMetadata = false)

                // if info.IndexNumber.HasValue |> not then
                //     let info = info :?> SeasonInfo
                //     let ofTry (b, v) = if b then Some(v) else None
                // 
                //     info.SeriesProviderIds.TryGetValue(TmdbUtils.SeasonNumber)
                //     |> ofTry
                //     |> Option.map (fun s -> Int32.TryParse(s))
                //     |> Option.bind ofTry
                //     |> Option.iter (fun ss -> 
                //         result.HasMetadata <- true
                //         result.Item <- new Season()
                //         result.Item.IndexNumber <- ss)

                return result
            }
        member _.GetImageResponse(url, cancellationToken) =
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
