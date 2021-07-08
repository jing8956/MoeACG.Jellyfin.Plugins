module MoeACG.Jellyfin.Plugin.Resolver.Reslove

open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Library

val resolveSeries  : ILogger<_> -> Regex seq -> ItemResolveArgs -> Series
val resolveSeason  : ILogger<_> -> Regex seq -> ItemResolveArgs -> Season
val resolveEpisode : ILogger<_> -> Regex seq -> Regex seq -> ItemResolveArgs -> Episode

val createSeriesRegexs  : ILogger<_> -> Regex seq
val createSeasonRegexs  : ILogger<_> -> Regex seq
val createEpisodeRegexs : ILogger<_> -> Regex seq
