namespace MoeACG.Jellyfin.Plugin

open MediaBrowser.Common.Plugins
open Microsoft.Extensions.DependencyInjection
open MoeACG.Jellyfin.Plugin.Providers.Tmdb

type PluginServiceRegistrator() =
    interface IPluginServiceRegistrator with
        member _.RegisterServices(serviceCollection) = 
            serviceCollection.AddSingleton<TmdbClientManager>() |> ignore
