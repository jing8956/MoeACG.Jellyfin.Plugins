namespace MoeACG.Jellyfin.Plugin.Configuration

open MediaBrowser.Model.Plugins

type SomeOptions =
    | OneOption = 0
    | AnotherOption = 1

type PluginConfiguration() =
    inherit BasePluginConfiguration()
    // store configurable settings your plugin might need
    member val TrueFalseSetting = true                      with get, set
    member val AnInteger        = 2                         with get, set
    member val AString          = "string"                  with get, set
    member val Options          = SomeOptions.AnotherOption with get, set

