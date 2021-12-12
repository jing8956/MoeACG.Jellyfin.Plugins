namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open MediaBrowser.Model.Entities
open System.Threading.Tasks

type MoeACGSeriesImageProvider(httpClientFactory: IHttpClientFactory) = 
    // After tvdb and fanart
    interface IHasOrder with member _.Order = 2
    interface IRemoteImageProvider with
        member _.Name = TmdbUtils.ProviderName
        member _.Supports(item) = item :? Series
        member _.GetSupportedImages(_) = seq { ImageType.Primary; ImageType.Backdrop }
        member x.GetImages(item, cancellationToken) =
            Task.FromResult(Seq.empty)
        member _.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
