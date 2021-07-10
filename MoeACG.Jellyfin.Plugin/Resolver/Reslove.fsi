module MoeACG.Jellyfin.Plugin.Resolver.Reslove

open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Library

val resolveSeries  : ILogger<_> -> Regex seq -> ItemResolveArgs -> Series
val resolveSeason  : ILogger<_> -> Regex seq -> ItemResolveArgs -> Season
val resolveEpisode : ILogger<_> -> Regex seq -> Regex seq -> ItemResolveArgs -> Episode

val createSeriesRegexs  : unit -> Regex array
val createSeasonRegexs  : unit -> Regex array
val createEpisodeRegexs : unit -> Regex array
