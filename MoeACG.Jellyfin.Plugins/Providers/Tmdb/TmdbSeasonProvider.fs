namespace MoeACG.Jellyfin.Plugin.Providers.Tmdb

open System
open System.Net.Http
open MediaBrowser.Common.Net
open MediaBrowser.Controller.Providers
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Model.Entities

type TmdbSeasonProvider(httpClientFactory: IHttpClientFactory, tmdbClientManager: TmdbClientManager) =

    interface IRemoteMetadataProvider<Season, ItemLookupInfo> with
        member _.Name = TmdbUtils.ProviderName
        member _.GetSearchResults(searchInfo, cancellationToken) = null
        member _.GetMetadata(info, cancellationToken) =
            async {
                let info = info :?> SeasonInfo
                let result = new MetadataResult<Season>()

                let seriesTmdbId = info.SeriesProviderIds.TryGetValue(MetadataProvider.Tmdb.ToString()) |> snd
                let seasonNumber = info.IndexNumber
                if String.IsNullOrWhiteSpace(seriesTmdbId) || (seasonNumber.HasValue |> not) then
                    return result
                else
                    let! seasonResult = 
                        tmdbClientManager.AsyncGetSeason(
                            int seriesTmdbId,
                            seasonNumber.Value,
                            info.MetadataLanguage,
                            TmdbUtils.getImageLanguagesParam info.MetadataLanguage,
                            cancellationToken)
                    
                    if seasonResult |> isNull then 
                        return result
                    else
                        result.HasMetadata <- true
                        result.Item <- new Season(
                            IndexNumber = seasonNumber,
                            Overview = seasonResult.Overview,
                            PremiereDate = seasonResult.AirDate,
                            ProductionYear = (seasonResult.AirDate |> Nullable.map (fun date -> date.Year |> Nullable)))

                        let tvdbId = seasonResult.ExternalIds |> Obj.map (fun ids -> ids.TvdbId)
                        if tvdbId |> String.IsNullOrEmpty |> not then
                            result.Item.SetProviderId(MetadataProvider.Tvdb, tvdbId)

                        seasonResult |> TmdbUtils.getPersons tmdbClientManager |> Seq.iter result.AddPerson
                        return result
            } |> Async.StartAsTask
        member _.GetImageResponse(url, cancellationToken) =
            httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken)
        