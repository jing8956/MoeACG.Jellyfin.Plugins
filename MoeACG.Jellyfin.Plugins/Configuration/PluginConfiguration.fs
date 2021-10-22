namespace MoeACG.Jellyfin.Plugin.Configuration

open System.Text.RegularExpressions
open MediaBrowser.Model.Plugins

type PluginConfiguration() =
    inherit BasePluginConfiguration()
    let mutable regexOptions = RegexOptions.IgnoreCase ||| RegexOptions.Compiled
    let createRegexBase = Seq.map (fun pattern -> new Regex(pattern, regexOptions)) >> Array.ofSeq
    let mutable seriesPatterns = 
        [|
            @"(?<seriesname>.*?) [全最].季.*"
            @"(?:\[Nekomoe kissaten])\[(?<seriesname>.*?) S(?<seasonnumber>[1-9])].*|(?:【[^】]*】)? ?(?:巴哈 )? ?(?<seriesname>.*?) ?(?:第(?<seasonnumber>[一二三四五六七八九十1-9])季[ ]?|(?<=[ \-\p{IsCJKUnifiedIdeographs}]|^)(?<seasonnumber>[1-9])$|(?<seasonnumber>[Ⅰ-Ⅹ]))?(?:(?<specialSeason>SPs?|OAD|OVA|剧场版|\[特[别別]篇]|Extras)|第(?<seasonnumber>一)季1998|\[[^\]]*].*|[TM]V|(?:1080|720)P|$)" 
            @"(?:【[^】]*】)? ?(?:巴哈 )? ?(?<seriesname>.*?) ?(?:[第全最].季.*|\[[^\]]*].*|(?<=[ \p{IsCJKUnifiedIdeographs}])[1-9]$|SP.*|[Ⅰ-Ⅹ]|OAD|$)" 
        |]
    let mutable seriesRegexs = seriesPatterns |> createRegexBase
    let mutable seasonPatterns =
        [| 
            @"(?:\[Nekomoe kissaten])\[(?<seriesname>.*?) S(?<seasonnumber>[1-9])].*|(?:【[^】]*】)? ?(?<seriesname>.*?) ?(?:第(?<seasonnumber>[一二三四五六七八九十1-9])季[ ]?|(?<=[ \-\p{IsCJKUnifiedIdeographs}]|^)(?<seasonnumber>[1-9])$|(?<seasonnumber>[Ⅰ-Ⅹ]))?(?:(?<specialSeason>SPs?|OAD|OVA|剧场版|\[特[别別]篇]|Extras)|第(?<seasonnumber>一)季1998|\[[^\]]*].*|[TM]V|(?:1080|720)P|$)" 
        |]
    let mutable seasonRegexs = seasonPatterns |> createRegexBase
    let mutable episodePatterns =
        [| 
            @"^(?:(?:EP|(?<specialSeason>OVA|SP))?(?<epnumber>[0-9.]+)?Y?(?:\[baha])?(?:[\. ](?<episodename>.*?))?\.[A-z1-9]+)$"
            @"^(?:[[【](?<header>Nekomoe kissaten|LKSUB|UHA-WINGS|YG&Neo.sub|KTXP|動畫瘋|HYSUB|UHA-WINGS|Nekomoe kissaten&VCB-Studio|BDRIP)[\] 】]\[?\ ?(?:巴哈 )?(?<seriesname>.+?)[\ \]]?(?:第(?<seasonnumber>[一二三四五六七八九十1-9])季[ ]?|[Ss](?<seasonnumber>[1-9]))?(?<specialSeason>\[特別篇])?(?:\[年齡限制版])?(?:\ (?<epnumber>[0-9.]+)\ |]?\[(?<epnumber>[0-9.]+)])?(?:\[[^\]]*])*\.[A-z1-9]+)$"
            @"^(?<seriesname>.+?)_(?<epnumber>[0-9.]+) \(480P\)_baofeng\.mp4$"
            @"^(?<seriesname>.+?)(?: S(?<seasonnumber>[1-9]))?(?: (?<epnumber>[0-9.]+))?(?: (?<specialSeason>OVA))?(?: END)? \[BD 1920x1080 HEVC-10bit OPUS ASSx2]\.mkv$"
            @"^\[(?<seriesname>.+?)]\[(?<specialSeason>OVA)]\[简日]\[720P]\.mp4$"
            @"^【漫锋网】(?<seriesname>.+?) 简体\.mp4$"
            @"^(?<episodename>.+?)\.[A-z1-9]+$"
        |]
    let mutable episodeRegexs = episodePatterns |> createRegexBase

    member _.Version = typeof<PluginConfiguration>.Assembly.GetName().Version.ToString()

    member _.RegexOptions with get() = regexOptions and set v = regexOptions <- v

    member val SeriesNameGroupName = "seriesname" with get, set
    member val SeasonNumberGroupName = "seasonnumber" with get, set
    member val EpisodeNumberGroupName = "epnumber" with get, set
    member val SpecialSeasonGroupName = "specialSeason" with get, set
    member val EpisodeNameGroupName = "episodename" with get, set
    member val SubtitleOrganizationGroupName = "header" with get, set

    member _.SeriesPatterns 
        with get() = seriesPatterns 
        and set v = 
            seriesRegexs <- v |> createRegexBase
            seriesPatterns <- v
    member _.SeriesRegexs = seriesRegexs
    member _.SeasonPatterns 
        with get() = seasonPatterns 
        and set v = 
            seasonRegexs <- v |> createRegexBase
            seasonPatterns <- v
    member _.SeasonRegexs = seasonRegexs
    member _.EpisodePatterns 
        with get() = episodePatterns 
        and set v = 
            episodeRegexs <- v |> createRegexBase
            episodePatterns <- v
    member _.EpisodeRegexs = episodeRegexs

    member val VideoExtensions = 
        [|
            ".m4v";
            ".3gp";
            ".nsv";
            ".ts";
            ".ty";
            ".strm";
            ".rm";
            ".rmvb";
            ".ifo";
            ".mov";
            ".qt";
            ".divx";
            ".xvid";
            ".bivx";
            ".vob";
            ".nrg";
            ".img";
            ".iso";
            ".pva";
            ".wmv";
            ".asf";
            ".asx";
            ".ogm";
            ".m2v";
            ".avi";
            ".bin";
            ".dvr-ms";
            ".mpg";
            ".mpeg";
            ".mp4";
            ".mkv";
            ".avc";
            ".vp3";
            ".svq3";
            ".nuv";
            ".viv";
            ".dv";
            ".fli";
            ".flv";
            ".001";
            ".tp"
        |] with get, set
