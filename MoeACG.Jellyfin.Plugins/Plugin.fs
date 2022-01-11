namespace MoeACG.Jellyfin.Plugins

open System
open MediaBrowser.Common.Plugins
open MediaBrowser.Model.Plugins
open MoeACG.Jellyfin.Plugins.Configuration
open Microsoft.Extensions.Logging

[<AllowNullLiteral>]
type Plugin(paths, serializer, logger: ILogger<Plugin>) as this =
    inherit BasePlugin<PluginConfiguration>(paths, serializer)
 
    static let mutable instance: Plugin = null
    do
        instance <- this;
        seq { LogLevel.Trace; LogLevel.Debug; LogLevel.Information }
        |> Seq.tryFind (logger.IsEnabled)
        |> Option.iter (fun level -> logger.LogInformation("The Loglevel is '{LogLevel}'.", level))

    static member Instance = instance
    static member Configuration = 
        (instance :> BasePlugin<_>).Configuration
        
    override _.Id   = "38b7c2e3-9924-4b50-a808-541753db15e0" |> Guid.Parse
    override _.Name = "MoeACG.Jellyfin.Plugins"

    interface IHasWebPages with
        member this.GetPages() = 
            new PluginPageInfo(
                Name = this.Name,
                EmbeddedResourcePath = $"{this.GetType().Namespace}.Configuration.configPage.html"
            ) |> Seq.singleton
