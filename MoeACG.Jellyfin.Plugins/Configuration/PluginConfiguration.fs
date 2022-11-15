namespace MoeACG.Jellyfin.Plugins.Configuration

open MediaBrowser.Model.Plugins

type PluginConfiguration() =
    inherit BasePluginConfiguration()

    member _.Version = typeof<PluginConfiguration>.Assembly.GetName().Version.ToString()

    member val EpisodeRegexs = "" with get, set
