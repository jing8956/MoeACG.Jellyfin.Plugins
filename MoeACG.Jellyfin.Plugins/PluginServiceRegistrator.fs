namespace MoeACG.Jellyfin.Plugins

open MediaBrowser.Common.Plugins
open Microsoft.Extensions.DependencyInjection
open MoeACG.Jellyfin.Plugins.Providers.Tmdb

type PluginServiceRegistrator() =
    interface IPluginServiceRegistrator with
        member _.RegisterServices(serviceCollection) = 
            serviceCollection.AddSingleton<TmdbClientManager>() |> ignore
