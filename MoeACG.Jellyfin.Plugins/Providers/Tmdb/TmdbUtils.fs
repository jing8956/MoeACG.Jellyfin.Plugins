module MoeACG.Jellyfin.Plugins.Providers.Tmdb.TmdbUtils

let [<Literal>] ProviderName = "MoeACG.TheMovieDb"
let [<Literal>] ApiKey = "4219e299c89411838049ab0dab19ebd5"

let [<Literal>] MaxCastMembers = 15

open System
open MediaBrowser.Model.Entities
open TMDbLib.Objects.General

let normalizeLanguage language =
    if language |> String.IsNullOrEmpty then 
        language
    else 
        // They require this to be uppercase
        // Everything after the hyphen must be written in uppercase due to a way TMDB wrote their api.
        // See here: https://www.themoviedb.org/talk/5119221d760ee36c642af4ad?page=3#56e372a0c3a3685a9e0019ab
        let parts = language.Split('-')
        if parts.Length = 2 then
            $"{parts.[0]}-{parts.[1].ToUpperInvariant()}"
        else language
let getImageLanguagesParam (preferredLanguage) =
    let languages = 
        seq { 
            let mutable preferredLanguage = preferredLanguage
            if preferredLanguage |> String.IsNullOrEmpty |> not then
                preferredLanguage <- normalizeLanguage preferredLanguage
                yield preferredLanguage
                if preferredLanguage.Length = 5 then yield preferredLanguage.[0 .. 1]
            yield "null"
            if preferredLanguage <> "en" then yield "en"
        }
    String.Join(',', languages)
let isTrailerType (video: Video) =
    video.Site.Equals("youtube", StringComparison.OrdinalIgnoreCase)
    && (
        (video.Type.Equals("trailer", StringComparison.OrdinalIgnoreCase) |> not)
        || (video.Type.Equals("teaser", StringComparison.OrdinalIgnoreCase) |> not)
       )
let mapCrewToPersonType (crew: Crew) =
    if crew.Department.Equals("production", StringComparison.OrdinalIgnoreCase) then 
        if crew.Job.Contains("director", StringComparison.OrdinalIgnoreCase) then PersonType.Director
        else if crew.Job.Contains("producer", StringComparison.OrdinalIgnoreCase) then PersonType.Producer
        else String.Empty
    else if crew.Department.Equals("writing", StringComparison.OrdinalIgnoreCase) then PersonType.Writer
    else String.Empty
let adjustImageLanguage imageLanguage requestLanguage =
    if String.IsNullOrEmpty imageLanguage
       || String.IsNullOrEmpty requestLanguage
       || requestLanguage.Length <= 2
       || imageLanguage.Length <> 2
       || (requestLanguage.StartsWith(imageLanguage, StringComparison.OrdinalIgnoreCase) |> not)
    then imageLanguage
    else requestLanguage
