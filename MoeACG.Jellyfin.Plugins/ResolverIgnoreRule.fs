// Jellyfin 自带的 Resolver 需要用户自行将媒体文件整理成三层结构，
// 不适用于无法整理的媒体文件夹。
namespace MoeACG.Jellyfin.Plugins

open System
open System.IO
open MediaBrowser.Controller.Resolvers
open MediaBrowser.Controller.Entities.TV
open MoeACG.Jellyfin.Plugins.Resolvers
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Library
open MediaBrowser.Model.Entities

type ResolverIgnoreRule(libraryManager:ILibraryManager) =
    interface IResolverIgnoreRule with 
        member _.ShouldIgnore(fileInfo, parent) =
            match parent with
            | :? Season | :? Series ->
                if fileInfo.IsDirectory then true
                else if fileInfo.Extension <> ".mp4" then true
                else if fileInfo.Name |> MoeACGResolver.CanResolve |> not then true
                else false
            | :? AggregateFolder -> false
            | :? UserRootFolder -> false
            | :? Folder ->
                let collectionType = libraryManager.GetContentType(parent)
                let isTvShows = String.Equals(collectionType, CollectionType.TvShows, StringComparison.OrdinalIgnoreCase)
                if isTvShows then
                    let isMp4 (path:string) = Path.GetExtension(path) = ".mp4"
                    if fileInfo.IsDirectory |> not then true
                    else
                        let files = Directory.EnumerateFiles(fileInfo.FullName)
                        let canResolve (path:string) =
                            let fileName = Path.GetFileNameWithoutExtension path
                            isMp4 path && MoeACGResolver.CanResolve fileName
                        let exists = files |> Seq.exists canResolve
                        exists |> not
                else false
            | _ -> true
