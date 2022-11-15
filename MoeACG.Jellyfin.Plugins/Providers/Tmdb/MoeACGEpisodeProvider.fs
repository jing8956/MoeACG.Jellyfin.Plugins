namespace MoeACG.Jellyfin.Plugins.Providers.Tmdb

open System
open System.Net.Http
open System.Text.RegularExpressions
open System.Threading.Tasks
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Providers
open Microsoft.FSharp.Linq.NullableOperators
open MoeACG.Jellyfin.Plugins

type MoeACGEpisodeProvider(episodeRegexsProvider: EpisodeRegexsProvider, httpClientFactory: IHttpClientFactory) =
    interface IHasOrder with member _.Order = 1
    interface IRemoteMetadataProvider<Episode, ItemLookupInfo> with
        member _.Name = TmdbUtils.ProviderName
        member this.GetSearchResults(searchInfo, cancellationToken) = 
            Task.FromResult(Seq.empty)
        member _.GetMetadata(info, cancellationToken) =
            task {
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

                let fileName = info.Path |> System.IO.Path.GetFileName
                let tryGetValue (name:string) (m:Match) =
                    let group = m.Groups.[name]
                    if group.Success then ValueSome group.Value else ValueNone
                let setResult m =
                    // 集数
                    tryGetValue "i" m
                    |> ValueOption.map int
                    |> ValueOption.iter (fun i -> info.IndexNumber <- i)

                    let numberZhHansTable = "一二三四五六七八九十"
                    let tryCastNumber (s:string) = 
                        numberZhHansTable.IndexOf(s)
                        |> function | -1 -> ValueNone | i -> ValueSome(i + 1)
                        |> ValueOption.orElseWith (fun() ->
                            match Int32.TryParse(s) with
                            | (true, v) -> ValueSome(v)
                            | _ -> ValueNone)

                    // 季数
                    tryGetValue "s" m
                    |> ValueOption.bind tryCastNumber
                    |> ValueOption.iter (fun i -> info.ParentIndexNumber <- i)
                episodeRegexsProvider.EpisodeRegexs
                |> Seq.map (fun r -> r.Match(fileName))
                |> Seq.tryFind (fun m -> m.Success)
                |> Option.iter setResult

                return result
            }
        member _.GetImageResponse(url, cancellationToken) = 
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
