namespace MoeACG.Jellyfin.Plugins.Resolvers

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
    override _.Priority = ResolverPriority.Second
    override this.Resolve(args) = resolveSeries logger args
type SeasonResolver(logger: ILogger<SeasonResolver>) =
    inherit FolderResolver<Season>()
    let regexs = createSeasonRegexs()
    override _.Priority = ResolverPriority.First
    override this.Resolve(args) = resolveSeason logger args
type EpisodeResolver(logger: ILogger<EpisodeResolver>) = 
    inherit ItemResolver<Episode>()
    override _.Priority = ResolverPriority.First
    override this.Resolve(args) = resolveEpisode logger args
