// Jellyfin 自带的 Resolver 需要用户自行将媒体文件整理成三层结构，
// 不适用于无法整理的媒体文件夹。
namespace MoeACG.Jellyfin.Plugins

open System
open MediaBrowser.Controller.Library
open MediaBrowser.Controller.Resolvers
open MediaBrowser.Model.Entities

type ResolverIgnoreRule(manager: ILibraryManager) = 
    interface IResolverIgnoreRule with 
        member _.ShouldIgnore(_, parent) = 
            let collectionType = manager.GetContentType(parent)
            String.Equals(collectionType, CollectionType.TvShows, StringComparison.OrdinalIgnoreCase)
