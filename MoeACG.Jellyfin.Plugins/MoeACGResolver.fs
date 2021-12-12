namespace MoeACG.Jellyfin.Plugins.Resolvers

open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Resolvers
open System.Text.RegularExpressions

[<Struct>]
type EpisodeType =
/// 未知。
| Unknown
/// 動畫瘋，季名会在标点符号（例如 '：'）前携带特殊符号 '‛'。
/// 季名( [Tag])?\【動畫瘋】季名( [Tag])?[集号|"電影"][清晰度].mp4
/// IS Infinite Stratos 第二季 [特別篇]\【動畫瘋】IS Infinite Stratos 第二季 [特別篇][01][1080P].mp4
/// sin 七大罪 [年齡限制版]\【動畫瘋】sin 七大罪 [年齡限制版][01][1080P].mp4
/// Vivy -Fluorite Eye's Song-\【動畫瘋】Vivy -Fluorite Eye's Song-[13.5][1080P].mp4
/// 在地下城寻求邂逅是否搞错了什么 第三季\【動畫瘋】在地下城寻求邂逅是否搞错了什么 第三季 特别篇[01][1080P].mp4
/// EndRO~\01[baha].mp4
| 動畫瘋
/// Nekomoe kissaten 字幕组，双层并带坑的结构。
/// D_CIDE TRAUMEREI\[Nekomoe kissaten][D_CIDE TRAUMEREI THE ANIMATION][01][1080p][JPSC].mp4
/// 坑：最后一集会发布合集。导致里面还有一个合集文件夹又把所有剧集复制了一份，忽略这个合集。
| ``Nekomoe kissaten``
/// Airota 字幕组，双层结构。
/// 小林家的女仆龙S\[Airota][Kobayashi-san Chi no Maid Dragon S2][01][1080p AVC AAC][CHS].mp4
| Airota
/// SSSS.DYNAZENON\[KTXP][SSSS.Dynazenon][01][GB][1080p].mp4
| KTXP

type MoeACGResolver(logger: ILogger<MoeACGResolver>) =
    
    static let toRegex p = new Regex(p, RegexOptions.Compiled ||| RegexOptions.ExplicitCapture)

    static let regexs =
        seq {
            (動畫瘋, "^【動畫瘋】.+(?<!\[特別篇])\[\d+]")
        } |> Map.ofSeq |> Map.map (fun _ -> toRegex)

    static let epRegexs =
        seq {
            "^【動畫瘋】(?<n>.+)\[(?<i>\d+)]\[(?<r>\w+)]"
        } |> Seq.map toRegex |> Seq.toArray

    static member GetEpisodeType (fileName: string) =
        regexs
        |> Map.tryFindKey (fun _ r -> r.IsMatch(fileName))
        |> Option.defaultValue Unknown
    static member CanResolve fileName =
        fileName
        |> MoeACGResolver.GetEpisodeType
        |> function | Unknown -> false | _ -> true

    interface IItemResolver with
        member _.Priority = ResolverPriority.First
        member _.ResolvePath args =
            if args.Parent.IsRoot |> not then
                if args.IsDirectory then
                    if args.ContainsFileSystemEntryByName("tvshow.nfo") |> not then
                        let result = Series(Path = args.Path)
                        result.Name <- args.FileInfo.Name
                        result.IsRoot <- args.Parent |> isNull
                        upcast result
                    else null
                else
                    let result = Episode(Path = args.Path)
                    let tryGetValue (name:string) (m:Match) =
                        let group = m.Groups.[name]
                        if group.Success then ValueSome group.Value else ValueNone
                    let setResult m =
                        m |> tryGetValue "n" |> ValueOption.iter (fun n -> result.Name <- n)
                        m |> tryGetValue "i" |> ValueOption.map int |> ValueOption.iter (fun i -> result.IndexNumber <- i)
                        m |> tryGetValue "r" |> ValueOption.iter (result.AddTag)
                    epRegexs
                    |> Seq.map (fun r -> r.Match(args.FileInfo.Name))
                    |> Seq.tryFind (fun m -> m.Success)
                    |> Option.iter setResult
                    upcast result
            else null
