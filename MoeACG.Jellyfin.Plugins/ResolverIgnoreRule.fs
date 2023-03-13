// Jellyfin 自带的 Resolver 需要用户自行将媒体文件整理成三层结构，
// 不适用于无法整理的媒体文件夹。
namespace MoeACG.Jellyfin.Plugins

open System
open System.IO
open MediaBrowser.Controller.Resolvers
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Library
open MediaBrowser.Model.Entities

type ResolverIgnoreRule(episodeRegexsProvider: EpisodeRegexsProvider,libraryManager:ILibraryManager) =
    static let mediaFileExtensions = [| ".mp4"; ".mkv" |]
    let isMediaFileExtension extension = mediaFileExtensions |> Seq.contains extension

    interface IResolverIgnoreRule with 
        member _.ShouldIgnore(fileInfo, parent) =
            match parent with
            | :? Season | :? Series ->
                if fileInfo.IsDirectory then true
                else if fileInfo.Extension |> isMediaFileExtension |> not then true
                else if fileInfo.Name |> episodeRegexsProvider.CanResolve |> not then true
                else false
            | :? AggregateFolder -> false
            | :? UserRootFolder -> false
            | :? Folder ->
                let collectionType = libraryManager.GetContentType(parent)
                let isTvShows = String.Equals(collectionType, CollectionType.TvShows, StringComparison.OrdinalIgnoreCase)
                if isTvShows then
                    if fileInfo.IsDirectory |> not then true
                    else
                        let files = Directory.EnumerateFiles(fileInfo.FullName)
                        let canResolve (path:string) =
                            let fileName = Path.GetFileNameWithoutExtension path
                            let extension = Path.GetExtension(path)
                            extension |> isMediaFileExtension && episodeRegexsProvider.CanResolve fileName
                        let exists = files |> Seq.exists canResolve
                        exists |> not
                else false
            | _ -> false
