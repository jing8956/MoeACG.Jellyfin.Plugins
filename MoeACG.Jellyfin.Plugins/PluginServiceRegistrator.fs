namespace MoeACG.Jellyfin.Plugins

open Microsoft.Extensions.DependencyInjection
open MoeACG.Jellyfin.Plugins.Providers.Tmdb
open MediaBrowser.Controller.Plugins

type PluginServiceRegistrator() =
    interface IPluginServiceRegistrator with
        member _.RegisterServices(serviceCollection, _) =
            serviceCollection.AddTransient<EpisodeRegexsProvider>() |> ignore
            serviceCollection.AddSingleton<TmdbClientManager>() |> ignore
