namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open MediaBrowser.Model.Entities
open System.Threading.Tasks

type MoeACGEpisodeImageProvider(httpClientFactory: IHttpClientFactory) =
    interface IHasOrder with member _.Order = 1
    interface IRemoteImageProvider with
        member _.Name = TmdbUtils.ProviderName
        member _.Supports(item) = item :? Episode
        member _.GetSupportedImages(_) = seq { ImageType.Primary }
        member x.GetImages(item, cancellationToken) = 
            Task.FromResult(Seq.empty)
        member this.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
