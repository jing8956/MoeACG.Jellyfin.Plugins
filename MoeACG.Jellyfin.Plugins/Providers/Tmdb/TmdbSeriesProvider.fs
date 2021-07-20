namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System
open System.Collections.Generic
open System.Globalization
open System.Linq
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Library
open MediaBrowser.Controller.Providers
open MediaBrowser.Model.Entities
open MediaBrowser.Model.Providers
open MediaBrowser.Controller.Providers

type TmdbSeriesProvider
    (
        libraryManager: ILibraryManager,
        httpClientFactory: IHttpClientFactory
    ) =
    // After TheTVDB
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Series, ItemLookupInfo> with 
       member _.Name = TmdbUtils.ProviderName
       member _.GetSearchResults(searchInfo, cancellationToken) = 
           async { 
               let mutable id: string = null;
               if searchInfo.TryGetProviderId(MetadataProvider.Tmdb, &id) then
                   ()
               return Seq.empty<RemoteSearchResult> 
           } |> Async.StartAsTask
       member _.GetMetadata(info, cancellationToken) = null
       member _.GetImageResponse(url, cancellationToken) = null :> Task<HttpResponseMessage>
