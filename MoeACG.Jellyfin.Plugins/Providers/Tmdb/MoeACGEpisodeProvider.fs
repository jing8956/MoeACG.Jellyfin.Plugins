namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open System.Threading.Tasks

type MoeACGEpisodeProvider(httpClientFactory: IHttpClientFactory) =
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Episode, ItemLookupInfo> with
        member _.Name = TmdbUtils.ProviderName
        member this.GetSearchResults(searchInfo, cancellationToken) = 
            Task.FromResult(Seq.empty)
        member _.GetMetadata(info, cancellationToken) = 
            Task.FromResult(new MetadataResult<Episode>(HasMetadata = false))
        member _.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
