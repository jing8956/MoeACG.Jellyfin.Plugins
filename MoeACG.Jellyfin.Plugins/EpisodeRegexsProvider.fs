namespace MoeACG.Jellyfin.Plugins

open System
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging

type EpisodeRegexsProvider(logger: ILogger<EpisodeRegexsProvider>) =

    member _.EpisodeRegexs =
        Plugin.Configuration.EpisodeRegexs
        |> fun s -> s.Split('\n', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
        |> Seq.choose (fun p ->
            try
                Regex(p, RegexOptions.ExplicitCapture) |> Some
            with
            | :? ArgumentException as e ->
                logger.LogWarning(e, "Create regex failed.")
                None
        )
    member x.CanResolve (fileName: ReadOnlyMemory<char>) = x.EpisodeRegexs |> Seq.exists (fun r -> r.IsMatch(fileName.Span)) 
