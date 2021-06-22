module MoeACG.Jellyfin.Plugin.Resolver.Reslove

open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Library

val resolveSeries  : ILogger<_> -> Regex -> ItemResolveArgs -> Series
val resolveSeason  : ILogger<_> -> Regex -> ItemResolveArgs -> Season
val resolveEpisode : ILogger<_> -> Regex -> ItemResolveArgs -> Episode

val createSeriesRegex  : unit -> Regex
val createSeasonRegex  : unit -> Regex
val createEpisodeRegex : unit -> Regex
