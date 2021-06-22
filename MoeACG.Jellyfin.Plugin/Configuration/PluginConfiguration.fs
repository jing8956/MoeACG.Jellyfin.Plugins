namespace MoeACG.Jellyfin.Plugin.Configuration

open System.Text.RegularExpressions
open MediaBrowser.Model.Plugins

type SomeOptions =
    | OneOption = 0
    | AnotherOption = 1

type PluginConfiguration() =
    inherit BasePluginConfiguration()
    member _.Version = typeof<PluginConfiguration>.Assembly.GetName().Version.ToString()
    // store configurable settings your plugin might need
    // member val TrueFalseSetting = true                      with get, set
    // member val AnInteger        = 2                         with get, set
    // member val AString          = "string"                  with get, set
    // member val Options          = SomeOptions.AnotherOption with get, set

    member val DefaultRegexOptions =  RegexOptions.IgnoreCase ||| RegexOptions.Compiled

    member val SeriesNameGroupName = "seriesname"
    member val SeasonNumberGroupName = "seasonnumber"
    member val EpisodeNumberGroupName = "epnumber"
    member val SpecialSeasonGroupName = "specialSeason"

    member val SeriesPattern = @"(?:【[^】]*】)? ?(?<seriesname>.*?) ?(?:[第全最].季.*|\[[^\]]*].*|(?<=[ \p{IsCJKUnifiedIdeographs}])[1-9]$|SP.*|[Ⅰ-Ⅹ]|OAD|$)" with get, set
    member val SeasonPattern = @"(?:\[Nekomoe kissaten])\[(?<seriesname>.*?) S(?<seasonnumber>[1-9])].*|(?:【[^】]*】)? ?(?<seriesname>.*?) ?(?:第(?<seasonnumber>[一二三四五六七八九十1-9])季[ ]?|(?<=[ \-\p{IsCJKUnifiedIdeographs}]|^)(?<seasonnumber>[1-9])$|(?<seasonnumber>[Ⅰ-Ⅹ]))?(?:(?<specialSeason>SPs?|OAD|OVA|剧场版|\[特[别別]篇]|Extras)|第(?<seasonnumber>一)季1998|\[[^\]]*].*|[TM]V|(?:1080|720)P|$)" with get, set
    member val EpisodePattern = "" with get, set
