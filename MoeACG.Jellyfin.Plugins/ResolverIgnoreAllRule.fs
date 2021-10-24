// 路径复杂，不使用强制三层结构低效的 Resolver
namespace MoeACG.Jellyfin.Plugins

open System
open MediaBrowser.Controller.Library
open MediaBrowser.Controller.Resolvers
open MediaBrowser.Model.Entities

type ResolverIgnoreAllRule(manager: ILibraryManager) = 
    interface IResolverIgnoreRule with 
        member _.ShouldIgnore(_, parent) = 
            let collectionType = manager.GetContentType(parent)
            String.Equals(
                collectionType, 
                CollectionType.TvShows, 
                StringComparison.OrdinalIgnoreCase)
