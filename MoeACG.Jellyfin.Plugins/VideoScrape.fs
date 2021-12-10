module MoeACG.Jellyfin.Plugins.VideoScrape

open System
open System.Net.Http
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

type SeriesMetaData<'TId> = 
    {
        Id: int
        Name: string
        Link: Uri
    }
type TmdbSeriesMetaData = SeriesMetaData<int>

type Episode =
    {
        /// 名称。
        Name: string
        /// 文件夹栈
        FloderStack: Floder list
        /// 文件名
        FileName: string
        /// 集号。
        Index: Nullable<int>
    }
type Season =
    {
        /// 名称。
        Name: string
        /// 文件夹栈
        FloderStack: Floder list
        /// 季号。
        /// Tmdb 将所有正常季外的剧集一律放在第 0 季特别篇中。
        Index: Nullable<int>
        /// 剧集列表。
        Episodes : Episode list
    }
type Series = 
    {
        /// 名称
        Name: string
        /// 文件夹栈
        FloderStack: Floder list
        /// 从影视数据库网站获取元数据
        MetaData: {| Tmdb: TmdbSeriesMetaData voption |}
        /// 季列表。
        Seasons : Season list
    }

module FloderStack =
    open System.IO

    let inline getName x = (^a: (member Name: string) (x))
    let inline folder list x = getName x :: list
    let inline toPath stack state = stack |> List.fold folder state |> List.toArray |> Path.Combine

    type Episode with
        /// 路径
        member x.Path = toPath x.FloderStack [ x.FileName ]
    type Season with 
        /// 路径
        member x.Path = toPath x.FloderStack []
    type Series with 
        /// 路径
        member x.Path = toPath x.FloderStack

[<Struct>]
type FloderType =
/// 未知。
| UnKnown = 0
/// 動畫瘋，季名会在标点符号（例如 '：'）前携带特殊符号 '‛'。
/// 季名( [Tag])?\【動畫瘋】季名( [Tag])?[集号|"電影"][清晰度].mp4
| 動畫瘋 = 1
/// Nekomoe kissaten 字幕组，双层并带坑的结构。
/// 季名\[Nekomoe kissaten][季全名][数字][1080p][JPSC].mp4
/// 坑：最后一集会发布合集。导致里面还有一个合集文件夹又把所有剧集复制了一份，忽略这个合集。
| ``Nekomoe kissaten`` = 3
/// UHA-WINGS 字幕组，双层结构。
/// 不要欺负我，长瀞同学\[UHA-WINGS] Ijiranaide Nagatoro-san - 01 [x264 1080p][CHS].mp4
| ``UHA-WINGS`` = 4
/// Airota 字幕组，双层结构。
/// 小林家的女仆龙S\[Airota][Kobayashi-san Chi no Maid Dragon S2][01][1080p AVC AAC][CHS].mp4
| Airota = 6

type Floder with
    member x.FloderType =
        let content = x.Content
        if content.Floders.IsEmpty then
            match content.Files with
            | file :: _ -> 
                if file.StartsWith("【動畫瘋】") then
                    FloderType.動畫瘋
                else if Regex.IsMatch(file, "^\d+\.mp4$") then
                    FloderType.UnKnown
                else FloderType.UnKnown
            | [] -> FloderType.UnKnown
        else FloderType.UnKnown

let scrape (httpClient: HttpClient) (content: FloderContent) : Series list = 
    content.Floders
    |> List.map (
        fun f -> 
            {
                Name = f.Name
                FloderStack = [ f ]
                MetaData = {| Tmdb = ValueNone |}
                Seasons = []
            })
