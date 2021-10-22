// 路径复杂，不使用强制三层结构低效的 Resolver
namespace MoeACG.Jellyfin.Plugins

open MediaBrowser.Controller.Resolvers

type ResolverIgnoreAllRule() = 
    interface IResolverIgnoreRule with member _.ShouldIgnore(_, _) = true
