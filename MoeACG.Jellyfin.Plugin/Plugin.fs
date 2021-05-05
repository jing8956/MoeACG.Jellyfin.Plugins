namespace MoeACG.Jellyfin.Plugin

open System
open MediaBrowser.Common.Plugins
open MediaBrowser.Model.Plugins
open MoeACG.Jellyfin.Plugin.Configuration

type Plugin(paths, serializer) =
    inherit BasePlugin<PluginConfiguration>(paths, serializer)
    
    override _.Id   = "eb5d7894-8eef-4b36-aa6f-5d124e828ce1" |> Guid.Parse
    override _.Name = "MoeACG.Jellyfin.Plugin"

    interface IHasWebPages with
        member this.GetPages() = 
            seq { 
               (new PluginPageInfo(
                   Name = this.Name,
                   EmbeddedResourcePath = $"{this.GetType().Namespace}.Configuration.configPage.html"
               ))
            }
