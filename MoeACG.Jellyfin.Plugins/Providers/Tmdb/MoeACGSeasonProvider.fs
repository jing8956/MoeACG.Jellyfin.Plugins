namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open System.Net.Http
open System.Threading.Tasks
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Providers
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Model.Entities
open TMDbLib.Objects.TvShows

type MoeACGSeasonProvider(httpClientFactory: IHttpClientFactory) =
    interface IRemoteMetadataProvider<Season, ItemLookupInfo> with
        member _.Name = TmdbUtils.ProviderName
        member _.GetSearchResults(_searchInfo, _cancellationToken) = Seq.empty |> Task.FromResult
        member _.GetMetadata(info, cancellationToken) =
            Task.FromResult(new MetadataResult<Season>(HasMetadata = false))
        member _.GetImageResponse(url, cancellationToken) =
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
