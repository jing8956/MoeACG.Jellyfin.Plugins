namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open System.Threading.Tasks
open Microsoft.FSharp.Linq.NullableOperators

type MoeACGEpisodeProvider(httpClientFactory: IHttpClientFactory) =
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Episode, ItemLookupInfo> with
        member _.Name = TmdbUtils.ProviderName
        member this.GetSearchResults(searchInfo, cancellationToken) = 
            Task.FromResult(Seq.empty)
        member _.GetMetadata(info, cancellationToken) =
            async {
                let result = new MetadataResult<Episode>(HasMetadata = false)

                if info.ParentIndexNumber ?= 1 then
                    let info = info :?> EpisodeInfo
                    let ofTry (b, v) = if b then Some(v) else None

                    info.SeriesProviderIds.TryGetValue(TmdbUtils.SeasonNumber)
                    |> ofTry
                    |> Option.map (fun s -> Int32.TryParse(s))
                    |> Option.bind ofTry
                    |> Option.iter (fun ss -> 
                        result.HasMetadata <- true
                        result.Item <- new Episode()
                        result.Item.ParentIndexNumber <- ss)

                return result
            } |> Async.StartAsTask
        member _.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
