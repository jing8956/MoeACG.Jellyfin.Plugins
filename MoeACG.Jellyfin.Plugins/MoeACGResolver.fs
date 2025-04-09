namespace MoeACG.Jellyfin.Plugins.Resolvers

open System
open Microsoft.Extensions.Logging
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Resolvers
open System.Text.RegularExpressions
open MediaBrowser.Controller.Library
open MediaBrowser.Model.Entities
open MoeACG.Jellyfin.Plugins
open System
open Jellyfin.Data.Enums

// [<Struct>]
// type EpisodeType =
// /// 未知。
// | Unknown
// /// 動畫瘋，季名会在标点符号（例如 '：'）前携带特殊符号 '‛'。
// /// 季名( [Tag])?\【動畫瘋】季名( [Tag])?[集号|"電影"][清晰度].mp4
// /// IS Infinite Stratos 第二季 [特別篇]\【動畫瘋】IS Infinite Stratos 第二季 [特別篇][01][1080P].mp4
// /// sin 七大罪 [年齡限制版]\【動畫瘋】sin 七大罪 [年齡限制版][01][1080P].mp4
// /// Vivy -Fluorite Eye's Song-\【動畫瘋】Vivy -Fluorite Eye's Song-[13.5][1080P].mp4
// /// 在地下城寻求邂逅是否搞错了什么 第三季\【動畫瘋】在地下城寻求邂逅是否搞错了什么 第三季 特别篇[01][1080P].mp4
// /// EndRO~\01[baha].mp4
// | 動畫瘋
// /// Nekomoe kissaten 字幕组，双层并带坑的结构。
// /// D_CIDE TRAUMEREI\[Nekomoe kissaten][D_CIDE TRAUMEREI THE ANIMATION][01][1080p][JPSC].mp4
// /// 坑：最后一集会发布合集。导致里面还有一个合集文件夹又把所有剧集复制了一份，忽略这个合集。
// | ``Nekomoe kissaten``
// /// Airota 字幕组，双层结构。
// /// 小林家的女仆龙S\[Airota][Kobayashi-san Chi no Maid Dragon S2][01][1080p AVC AAC][CHS].mp4
// | Airota
// /// SSSS.DYNAZENON\[KTXP][SSSS.Dynazenon][01][GB][1080p].mp4
// | KTXP
// /// [UHA-WINGS] Ijiranaide Nagatoro-san - 01 [x264 1080p][CHS].mp4
// | ``UHA-WINGS``

type MoeACGResolver(episodeRegexsProvider: EpisodeRegexsProvider, libraryManager: ILibraryManager, logger: ILogger<MoeACGResolver>) =
    
    let [<Literal>] regexOptions = RegexOptions.ExplicitCapture
    let isBaha (fileName: ReadOnlySpan<char>) = Regex.IsMatch(fileName, "^【動畫瘋】", regexOptions)
    let tryGetValue (name:string) (m:Match) =
        let group = m.Groups.[name]
        if group.Success then ValueSome group.Value else ValueNone
    let numberZhHansTable = "一二三四五六七八九十"
    let tryCastNumber (s:string) = 
        numberZhHansTable.IndexOf(s)
        |> function | -1 -> ValueNone | i -> ValueSome(i + 1)
        |> ValueOption.orElseWith (fun() ->
            match Int32.TryParse(s) with
            | (true, v) -> ValueSome(v)
            | _ -> ValueNone)

    interface IItemResolver with
        member _.Priority = ResolverPriority.First
        member _.ResolvePath args =
            if args.Parent = null then null else
            if args.Parent :? AggregateFolder then null else
            if args.Parent :? UserRootFolder  then null else

            let collectionType = libraryManager.GetContentType(args.Parent)
            
            if collectionType.HasValue |> not then null else
            if collectionType.Value <> Jellyfin.Data.Enums.CollectionType.tvshows then null else
            if args.ContainsFileSystemEntryByName("tvshow.nfo") then null else
            if args.ContainsFileSystemEntryByName("season.nfo") then null else
            
            let result = Series(Path = args.Path)
            let name, year =
                query {
                    for f in args.FileSystemChildren do
                    where (f.IsDirectory |> not)
                    for r in episodeRegexsProvider.EpisodeRegexs do
                    let m = r.Match(f.Name)
                    where m.Success
                    let n =
                        match m.Groups.["n"].Value with
                        | n when m.Groups.ContainsKey("baha") -> n.Replace("‛", "")
                        | n when m.Groups.ContainsKey("low_line") -> n.Replace('_', ' ')
                        | n when m.Groups.ContainsKey("period") -> n.Replace('.', ' ')
                        | n -> n
                    let year =
                        tryGetValue "year" m
                        |> ValueOption.bind tryCastNumber
                    select (n, year)
                    head
                }

            result.Name <- name.Trim()
            year |> ValueOption.iter (fun year -> result.ProductionYear <-  year)   
            result.IsRoot <- args.Parent |> isNull
            upcast result

    interface IMultiItemResolver with
        member _.ResolveMultiple(parent, files, collectionType, directoryService) =

            if parent :? AggregateFolder then null else
            if parent :? UserRootFolder  then null else
            if collectionType.HasValue |> not then null else
            if collectionType.Value <> CollectionType.tvshows then null else
            if files |> Seq.exists (fun f -> String.Equals(f.Name, "tvshow.nfo", StringComparison.OrdinalIgnoreCase)) then null else
            if files |> Seq.exists (fun f -> String.Equals(f.Name, "season.nfo", StringComparison.OrdinalIgnoreCase)) then null else
            if parent :? Series |> not then null else

            let files = files |> Seq.where (fun f -> not f.IsDirectory)
            let result = new MultiItemResolverResult()
            for file in files do
                let ep = Episode(Path = file.FullName)
                let foundAction m =
                    // 名称
                    tryGetValue "n" m
                    |> ValueOption.map (fun n -> if m.Groups.ContainsKey("baha") then n.Replace("‛", "") else n)
                    |> ValueOption.iter (fun n -> ep.Name <- n)
                        
                    // 集数
                    tryGetValue "i" m
                    |> ValueOption.map int
                    |> ValueOption.iter (fun i -> ep.IndexNumber <- i)

                    // 季数
                    tryGetValue "s" m
                    |> ValueOption.bind tryCastNumber
                    |> ValueOption.iter (fun i -> ep.ParentIndexNumber <- i)

                    // 年份
                    tryGetValue "year" m
                    |> ValueOption.bind tryCastNumber
                    |> ValueOption.iter (fun i -> ep.ProductionYear <- i)

                    result.Items.Add(ep)

                episodeRegexsProvider.EpisodeRegexs
                |> Seq.map (fun r -> r.Match(file.Name))
                |> Seq.tryFind (fun m -> m.Success)
                |> Option.iter (foundAction)
             
            result
