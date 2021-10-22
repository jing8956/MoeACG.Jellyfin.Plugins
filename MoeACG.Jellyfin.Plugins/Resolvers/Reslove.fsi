module MoeACG.Jellyfin.Plugins.Resolvers.Reslove

open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Library

val resolveSeries  : ILogger<_> -> ItemResolveArgs -> Series
val resolveSeason  : ILogger<_> -> ItemResolveArgs -> Season
val resolveEpisode : ILogger<_> -> ItemResolveArgs -> Episode

val createSeriesRegexs  : unit -> Regex array
val createSeasonRegexs  : unit -> Regex array
val createEpisodeRegexs : unit -> Regex array
