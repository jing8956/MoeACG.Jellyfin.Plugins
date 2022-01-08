namespace MoeACG.Jellyfin.Plugins.Resolvers

open System
open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Resolvers
open System.Text.RegularExpressions
open MediaBrowser.Controller.Library
open MediaBrowser.Model.Entities

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
/// [UHA-WINGS] Ijiranaide Nagatoro-san - 01 [x264 1080p][CHS].mp4
| ``UHA-WINGS``

type MoeACGResolver(libraryManager: ILibraryManager, logger: ILogger<MoeACGResolver>) =
    
    static let [<Literal>] regexOptions = RegexOptions.Compiled ||| RegexOptions.ExplicitCapture
    static let toRegex p = new Regex(p, regexOptions)
    static let toRegexArray = Seq.map toRegex >> Seq.toArray

    static let canResolveRegexs =
        seq {
            "^【動畫瘋】.+(?<!\[特別篇])\[\d+]"
            "^\[Nekomoe kissaten]\[.+]\[\d+]"
            "^\[Airota]\[.+]\[\d+]"
            "^\[KTXP]\[.+]\[\d+]"
            "^\[UHA-WINGS] .+ - \d+"
            "^\d+\[baha]"
        } |> toRegexArray

    static let epRegexs =
        seq {
            "^【(?<baha>動畫瘋)】(?<n>.+?) ?(第(?<s>.)季)?\[(?<i>\d+)]"
            "^\[Nekomoe kissaten]\[(?<n>.+)]\[(?<i>\d+)]"
            "^\[Airota]\[(?<n>.+)]\[(?<i>\d+)]"
            "^\[KTXP]\[(?<n>.+)]\[(?<i>\d+)]"
            "^\[UHA-WINGS] (?<n>.+) - (?<i>\d+)"
            "^(?<i>\d+)\[baha]"
        } |> Seq.map toRegex |> Seq.toArray

    static let isBaha fileName = Regex.IsMatch(fileName, "^【動畫瘋】", regexOptions)

    static member CanResolve fileName =
        canResolveRegexs
        |> Seq.exists (fun r -> r.IsMatch(fileName)) 

    interface IItemResolver with
        member _.Priority = ResolverPriority.First
        member _.ResolvePath args =
            if (args.Parent :? AggregateFolder || args.Parent :? UserRootFolder) |> not then
                let collectionType = libraryManager.GetContentType(args.Parent)
                let isTvShows = String.Equals(collectionType, CollectionType.TvShows, StringComparison.OrdinalIgnoreCase)
                if isTvShows then
                    if args.IsDirectory then
                        if args.ContainsFileSystemEntryByName("tvshow.nfo") |> not then
                            let result = Series(Path = args.Path)
                            let name =
                                let name = args.FileInfo.Name
                                let hasBaha = 
                                    args.FileSystemChildren 
                                    |> Seq.filter (fun f -> f.IsDirectory |> not)
                                    |> Seq.exists (fun f -> f.Name |> isBaha)
                                if hasBaha then
                                    let name = 
                                        Regex.Match(name, ".+(?= 巴哈|第\w季)", regexOptions)
                                        |> fun m -> if m.Success then m.Value else name
                                    name.Replace("‛", "")
                                else name
                            result.Name <- name
                            result.IsRoot <- args.Parent |> isNull
                            upcast result
                        else null
                    else
                        let result = Episode(Path = args.Path)
                        let tryGetValue (name:string) (m:Match) =
                            let group = m.Groups.[name]
                            if group.Success then ValueSome group.Value else ValueNone
                        let setResult m =
                            // 名称
                            tryGetValue "n" m
                            |> ValueOption.map (fun n -> if m.Groups.ContainsKey("baha") then n.Replace("‛", "") else n)
                            |> ValueOption.iter (fun n -> result.Name <- n)
                        
                            // 集数
                            tryGetValue "i" m
                            |> ValueOption.map int
                            |> ValueOption.iter (fun i -> result.IndexNumber <- i)

                            let numberZhHansTable = "一二三四五六七八九十"
                            let tryCastNumber (s:string) = 
                                numberZhHansTable.IndexOf(s)
                                |> function | -1 -> ValueNone | i -> ValueSome(i + 1)

                            // 季数
                            tryGetValue "s" m
                            |> ValueOption.bind tryCastNumber
                            |> ValueOption.iter (fun i -> result.ParentIndexNumber <- i)
                        epRegexs
                        |> Seq.map (fun r -> r.Match(args.FileInfo.Name))
                        |> Seq.tryFind (fun m -> m.Success)
                        |> Option.iter setResult
                        upcast result
                else null
            else null
