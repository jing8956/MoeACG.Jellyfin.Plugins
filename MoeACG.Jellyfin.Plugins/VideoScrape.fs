module MoeACG.Jellyfin.Plugins.VideoScrape

open System
open System.Text.RegularExpressions

type Floder = 
    {
        Name: string
        Content: FloderContent
    }
and FloderContent =
    {
        Floders: Floder list
        Files: string list
    }

type Episode =
    {
        /// 名称。
        Name: string
        /// 路径。
        Path: string
        /// 集号。
        Index: Nullable<int>
    }
type Season =
    {
        /// 名称。
        Name: string
        /// 路径。
        Path: string
        /// 季号。
        /// Tmdb 将所有正常季外的剧集一律放在第 0 季特别篇中。
        Index: Nullable<int>
        /// 剧集列表。
        Episodes : Episode list
    }
type SeriesMetaData<'TId> = 
    {
        Id: int
        Name: string
        Link: Uri
    }
type Series = 
    {
        /// 名称
        Name: string
        /// 路径
        Path: string
        /// 从影视数据库网站获取元数据
        MetaData: 
            {|
                Tmdb: SeriesMetaData<int> option
            |}
        /// 季列表。
        Seasons : Season list
    }

type FloderType =
    /// 动画疯类型。
    /// 季名( [Tag])?/【動畫瘋】季名( [Tag])?[集号/"電影"][清晰度].mp4
    | AniGamer
    /// 纯数字类型（B站片源）。
    /// 季名/数字.mp4
    | BiliBili
    /// 三层结构。
    /// 季名/第X季/数字.mp4
    | Three
    | UnKnown

let (
    /// AAAAA
    |AniGamer|BiliBili|Three|UnKnown|) (floder: Floder) =
    if floder.Content.Floders.IsEmpty then
        let file = floder.Content.Files.Head
        if file.StartsWith("【動畫瘋】") then
            AniGamer
        else if Regex.IsMatch(file, "$\d+\.^") then
            BiliBili
        else
            UnKnown

    else
        Three

let scrape (content: FloderContent) : Series list = 
    
    []
