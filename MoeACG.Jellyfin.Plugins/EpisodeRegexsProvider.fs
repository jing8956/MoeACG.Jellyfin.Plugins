namespace MoeACG.Jellyfin.Plugins

open System
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging

type EpisodeRegexsProvider(logger: ILogger<EpisodeRegexsProvider>) =
    let toRegex p =
        try
            Regex(p, RegexOptions.ExplicitCapture) |> Some
        with
        | :? ArgumentException as e ->
            logger.LogWarning(e, "Create regex failed.")
            None
    let toRegex = Seq.choose toRegex

    member _.EpisodeRegexs =
        logger.LogTrace("Create episode regexs")
        Plugin.Configuration.EpisodeRegexs
        |> fun s -> s.Split('\n', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
        |> toRegex
        // seq {
        //     "^【(?<baha>動畫瘋)】(?<n>.+?) ?(第(?<s>.)季)?(?<!\[特別篇])\[(?<i>\d+)]"
        // 
        //     "^\[Nekomoe kissaten]\[(?<n>.+)]\[(?<i>\d+)]"
        //     "^\[Airota]\[(?<n>.+)]\[(?<i>\d+)]"
        //     "^\[KTXP]\[(?<n>.+)]\[(?<i>\d+)]"
        //     "^\[XKsub]\[(?<n>.+)]\[(?<i>\d+)]"
        //     "^\[Mmch.sub]\[(?<n>.+)]\[(?<i>\d+)]"
        // 
        //     "^\[UHA-WINGS] (?<n>.+) - (?<i>\d+)"
        //     "^\[Snow-Raws] (?<n>.+) 第(?<i>\d+)話"
        //     "^\[HYSUB](?<n>.+)\[(?<i>\d+)]"
        //     "^\[Sakurato] (?<n>.+) \[(?<i>\d+)]"
        //     "^\[NGA&Sakurato] (?<n>.+) \[(?<i>\d+)]"
        // 
        //     "^(?<i>\d+)\[baha]"
        // } |> toRegexArray

    member x.CanResolve fileName = x.EpisodeRegexs |> Seq.exists (fun r -> r.IsMatch(fileName)) 
