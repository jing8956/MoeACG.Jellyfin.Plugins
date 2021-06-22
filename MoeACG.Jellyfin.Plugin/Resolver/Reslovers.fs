namespace MoeACG.Jellyfin.Plugin.Resolver

open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Resolvers

[<AbstractClass>]
type FolderResolver<'TFolder
    when 'TFolder :> Folder 
     and 'TFolder :  (new : unit -> 'TFolder) 
     and 'TFolder :  null>() = 
    inherit ItemResolver<'TFolder>()

    override _.SetInitialItemValues(item, args) =
        base.SetInitialItemValues(item, args)
        item.IsRoot <- args.Parent |> isNull

open Reslove
type SeriesResolver(logger: ILogger<SeriesResolver>) =
    inherit FolderResolver<Series>()
    let regex = createSeriesRegex()
    override _.Priority = ResolverPriority.First
    override _.Resolve(args) = resolveSeries logger regex args
type SeasonResolver(logger: ILogger<SeasonResolver>) =
    inherit FolderResolver<Season>()
    let regex = createSeasonRegex()
    override _.Priority = ResolverPriority.Second
    override _.Resolve(args) = resolveSeason logger regex args
type EpisodeResolver(logger: ILogger<EpisodeResolver>) = 
    inherit ItemResolver<Episode>()
    let regex = createEpisodeRegex()
    override _.Priority = ResolverPriority.Third
    override _.Resolve(args) = resolveEpisode logger regex args
