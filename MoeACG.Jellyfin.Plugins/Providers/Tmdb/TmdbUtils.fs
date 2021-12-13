module MoeACG.Jellyfin.Plugins.Providers.Tmdb.TmdbUtils

let [<Literal>] ProviderName = "MoeACG.TheMovieDb"
let [<Literal>] ApiKey = "4219e299c89411838049ab0dab19ebd5"

let [<Literal>] MaxCastMembers = 15
let [<Literal>] SeasonNumber = "MoeACG-SeasonNumber"

open System

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

