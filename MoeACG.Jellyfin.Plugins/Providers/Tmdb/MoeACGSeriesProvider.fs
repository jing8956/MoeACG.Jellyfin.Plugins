namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open System.Threading.Tasks
open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers

type MoeACGSeriesProvider(httpClientFactory: IHttpClientFactory) =

    // After TheTVDB
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Series, ItemLookupInfo> with 
        member _.Name = TmdbUtils.ProviderName
        member _.GetSearchResults(searchInfo, cancellationToken) = 
            Task.FromResult(Seq.empty)
        member _.GetMetadata(info, cancellationToken) =
            Task.FromResult(new MetadataResult<Series>(HasMetadata = false))
        member _.GetImageResponse(url, cancellationToken) =
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
