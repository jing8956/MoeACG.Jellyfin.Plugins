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
open MediaBrowser.Controller.Library

type SeriesResolver(logger: ILogger<SeriesResolver>) =
    inherit FolderResolver<Series>()
    let regexs = createSeriesRegexs logger |> Array.ofSeq
    override _.Priority = ResolverPriority.Second
    override _.Resolve(args) = resolveSeries logger regexs args
type SeasonResolver(logger: ILogger<SeasonResolver>) =
    inherit FolderResolver<Season>()
    let regexs = createSeasonRegexs logger |> Array.ofSeq
    override _.Priority = ResolverPriority.Second
    override _.Resolve(args) = resolveSeason logger regexs args
type EpisodeResolver(logger: ILogger<EpisodeResolver>) = 
    inherit ItemResolver<Episode>()
    let ssRegexs = createSeasonRegexs logger |> Array.ofSeq
    let regexs = createEpisodeRegexs logger |> Array.ofSeq
    override _.Priority = ResolverPriority.First
    override _.Resolve(args) = resolveEpisode logger regexs ssRegex args
