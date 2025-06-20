// Jellyfin 自带的 Resolver 需要用户自行将媒体文件整理成三层结构，
// 不适用于无法整理的媒体文件夹。
namespace MoeACG.Jellyfin.Plugins

open System
open System.IO
open MediaBrowser.Controller.Resolvers
open MediaBrowser.Controller.Entities.TV
open MediaBrowser.Controller.Entities
open MediaBrowser.Controller.Library
open Jellyfin.Data.Enums
open MoeACG.Jellyfin.Plugins.Providers.Tmdb

type ResolverIgnoreRule(episodeRegexsProvider: EpisodeRegexsProvider, libraryManager:ILibraryManager) =
    static let mediaFileExtensions = [| ".mp4"; ".mkv" |]
    let isMediaFileExtension extension = mediaFileExtensions |> Seq.contains extension

    interface IResolverIgnoreRule with 
        member _.ShouldIgnore(fileInfo, parent) =
            match parent with
            | :? Season | :? Series ->
                not(fileInfo.IsDirectory)
                && not(fileInfo.Extension |> isMediaFileExtension)
                && not(fileInfo.Name.AsMemory() |> episodeRegexsProvider.CanResolve)
            | :? AggregateFolder -> false
            | :? UserRootFolder -> false
            | :? Folder ->
                let collectionType = libraryManager.GetContentType(parent)
                let isTvShows =
                    collectionType.HasValue &&
                    collectionType.Value = CollectionType.tvshows

                if not isTvShows then false else

                let libraryOptions = libraryManager.GetLibraryOptions(parent)
                let disabledProviders = libraryOptions.DisabledMediaSegmentProviders
                let isDisabled = disabledProviders |> Array.contains TmdbUtils.ProviderName
                if isDisabled then false else

                if not fileInfo.IsDirectory then true else
                Directory.EnumerateFiles(fileInfo.FullName)
                |> Seq.where (fun path -> Path.GetExtension(path) |> isMediaFileExtension)
                |> Seq.where (fun path -> Path.GetFileName(path).AsMemory() |> episodeRegexsProvider.CanResolve)
                |> Seq.isEmpty
            | _ -> false
