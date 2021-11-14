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
/// 空文件夹。
| Empty = -1
/// 未知。
| UnKnown = 0
/// 动画疯，季名会在标点符号（例如 '：'）前携带特殊符号 '‛'。
/// 季名( [Tag])?\【動畫瘋】季名( [Tag])?[集号|"電影"][清晰度].mp4
| AniGamer = 1
/// Nekomoe kissaten 字幕组，双层并带坑的结构。
/// 季名\[Nekomoe kissaten][季全名][数字][1080p][JPSC].mp4
/// 坑：单独把 D_CIDE TRAUMEREI 第 13 集放进了专门的文件夹里，需要设计手动处理。
/// 坑：最后一集会发布合集。导致里面还有一个合集文件夹又把所有剧集复制了一份。
| NekomoeKissaten = 3
/// UHA-WINGS 字幕组，双层结构。
/// 不要欺负我，长瀞同学\[UHA-WINGS] Ijiranaide Nagatoro-san - 01 [x264 1080p][CHS].mp4
| UHA_WINGS = 4
/// Airota 字幕组，双层结构。
/// 小林家的女仆龙S\[Airota][Kobayashi-san Chi no Maid Dragon S2][01][1080p AVC AAC][CHS].mp4
| Airota = 6
/// 双层结构。
/// 战栗杀机\EP01.mp4
| _TwoEP = 13
/// 魔法科高校的劣等生(也是B源）
/// 魔法科高校的劣等生\TV\01.入学篇Ⅰ.mp4
/// 魔法科高校的劣等生\剧场版\呼唤星星的少女.mp4
| _HybridTv魔法科高校的劣等生 = 304
/// 时光代理人
/// 时光代理人\11-带着光的人.flv (而且只有第 11 集）
| _Two时光代理人 = 14
/// 文件后缀名携带 bnd 的双层结构，还有重复的 backup。
/// backup 是B站源，替换成字幕组源后蛤蛤蛤懒得改站里的链接。
/// 线上的老婆不可能是女生\01 终于有老婆了.bhd.mp4
/// 线上的老婆不可能是女生-backup\01 终于有老婆了.bhd.mp4
| _TwoBhd = 15
/// BDRIP
/// 路人女主的养成方法\[BDRIP][路人女主的养成方法 Fine][Movie][1080P][简体内嵌].mkv
| TwoBdRip = 16
/// 鳄鱼小顽皮
/// 鳄鱼小顽皮\鳄鱼小顽皮历险记_01 (480P)_baofeng.mp4
| _Two鳄鱼小顽皮 = 18
/// DMG 字幕组，三层结构。
/// 从零开始的异世界生活\[DMG] Reゼロから始める異世界生活 [BDRip][1080P][CHS][MP4]\[DMG] Reゼロから始める異世界生活 第01話「始まりの終わりと終わりの始まり」 [BDRip][AVC_AAC][1080P][CHS](D87D2939).mp4
| _DMG = 5
/// 双层结构。
/// 季名\季名 数字.mp4
| _Two1 = 10
/// 双层结构，但多带了 MV 文件夹。
/// 境界的彼方\00.mp4
/// 境界的彼方\MV\约定的羁绊.mp4
/// 坑：境界的彼方有 00 集。
| _TwoMv = 11
/// 双层结构，视频是 flv 格式的（可能不是B站片源？）, 只有《轻羽飞扬！》第 12 集是 mp4 
/// 天狼 Sirius the Jaeger\01.flv
/// 轻羽飞扬！\12.mp4
| _TwoFlv = 12
/// 露蒂的玩具。
/// 露蒂的玩具\720P\01.mp4
/// 露蒂的玩具\1080P\01.mp4
| _Two露蒂的玩具 = 17
/// 三层结构。
/// 系列名\季名\数字.mp4
/// 命运石之门\命运石之门0\数字.mp4
| _Three = 200
/// 三层结构。
/// 系列名\第一季\数字（带小数点）.mp4
/// 系列名\第一季OVA\数字.mp4
/// 新妹魔王的契约者\OVA\第一季.mp4
| _SimpleThree = 201
/// 三层结构
/// 系列名\系列名\数字.flv
/// 系列名\系列名 第二季\数字.flv
/// 系列名\系列名 OVA\数字.flv
| _SimpleThreeWithSeriesName = 202
/// 三层结构，识别时注意不能紧跟0，否则会和《命运石之门0》弄混。
/// psycho-pass\psycho-pass1\01.mp4
/// psycho-pass\psycho-pass2\01.mp4
/// psycho-pass\psycho-pass-new\01.mp4
/// psycho-pass\剧场版\【漫锋网】Psycho-Pass The Movie 简体.mp4
| _SimpleThreeWithSeriesNameAndNumber = 203
/// 物语系列，虽然特征和 Three 相同，但是很多篇幅较短的物语实际上在 TmDB 算第 0 季特别篇，导致对不上。
/// 系列名\季名\数字.mp4
| _Three物语系列 = 204
/// 魔卡少女樱
/// 魔卡少女樱\第一季1998\SP.mp4
/// 魔卡少女樱\【剧场版】被封印的卡片\01.mp4
| _Three魔卡少女樱 = 205
/// 魔法少女小圆。
/// 魔法少女小圆\第一季\01.mp4
/// 魔法少女小圆\叛逆的故事\叛逆的故事.mp4
| _Three魔法少女小圆 = 206
/// 混合结构，特征是含有唯一的 TV 文件夹，通常是只有一季的系列。
/// 系列名\TV\数字.mp4
/// 系列名\剧场版或特别篇.mp4
/// 日在校园\日在校园：魔法之心\日在校园：魔法之心.mp4
| _HybridTv = 300
/// 奈亚子
/// 奈亚子\奈亚子-1\01.mp4
/// 奈亚子\奈亚子F.mp4
/// 奈亚子\潜行吧！奈亚子 OVA 温柔地解决敌人的方法 .flv
| _Hybrid奈亚子 = 301
/// 约会大作战，唯一需要在系列名处刮削的文件夹，带有 Extras 文件夹存放个季的 NCOP 与 NCED。
/// 约会大作战 全三季 Date A Live S1+S2+S3+Movie [BD 1920x1080 HEVC-10bit OPUS][简繁内封字幕]\Date A Live S1\Date A Live 01 [BD 1920x1080 HEVC-10bit OPUS ASSx2].mkv
/// 约会大作战 全三季 Date A Live S1+S2+S3+Movie [BD 1920x1080 HEVC-10bit OPUS][简繁内封字幕]\Date A Live The Movie：Mayuri Judgement [BD 1920x1080 HEVC-10bit OPUS ASSx2].mkv
| _Hybrid约会大作战 = 302
/// 终结的炽天使。
/// 终结的炽天使\1.mp4
/// 终结的炽天使\2\1.mp4
/// 终结的炽天使\2\OVA.mp4
| _Hybrid终结的炽天使 = 303
/// B站，垃圾片源。
/// 《虚拟小姐在看着你》前两集是大写的 MP4
/// 季名\数字.mp4|MP4
| _BiliBili = 2147483647

type Floder with
    member x.FloderType =
        let content = x.Content
        if content.Floders.IsEmpty then
            match content.Files with
            | file :: _ -> 
                if file.StartsWith("【動畫瘋】") then
                    FloderType.AniGamer
                else if Regex.IsMatch(file, "^\d+\.mp4$") then
                    FloderType.BiliBili
                else FloderType.UnKnown
            | [] -> FloderType.Empty
        else 
           FloderType.Three

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
