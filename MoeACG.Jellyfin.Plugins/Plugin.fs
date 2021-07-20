namespace MoeACG.Jellyfin.Plugin

open System
open MediaBrowser.Common.Plugins
open MediaBrowser.Model.Plugins
open MoeACG.Jellyfin.Plugin.Configuration

[<AllowNullLiteral>]
type Plugin(paths, serializer) as this =
    inherit BasePlugin<PluginConfiguration>(paths, serializer)
    
    static let mutable instance: Plugin = null
    do instance <- this
    static member Instance = instance
    static member Configuration = (instance :> BasePlugin<_>).Configuration

    override _.Id   = "38b7c2e3-9924-4b50-a808-541753db15e0" |> Guid.Parse
    override _.Name = "MoeACG.Jellyfin.Plugin"

    interface IHasWebPages with
        member this.GetPages() = 
            seq { 
               (new PluginPageInfo(
                   Name = this.Name,
                   EmbeddedResourcePath = $"{this.GetType().Namespace}.Configuration.configPage.html"
               ))
            }
