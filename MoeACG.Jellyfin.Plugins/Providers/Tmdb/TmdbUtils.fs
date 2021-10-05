module MoeACG.Jellyfin.Plugin.Providers.Tmdb.TmdbUtils

let [<Literal>] ProviderName = "MoeACG.TheMovieDb"
let [<Literal>] ApiKey = "4219e299c89411838049ab0dab19ebd5"

let [<Literal>] MaxCastMembers = 15

open System
open System.Linq
open MediaBrowser.Controller.Entities
open MediaBrowser.Model.Entities
open TMDbLib.Objects.General
open TMDbLib.Objects.TvShows

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

let inline getPersons (tmdbClientManager: TmdbClientManager) hasCredits =
    let ofCasts (cast: Cast seq) =
        let toPersonInfo (actor: Cast) =
            let personInfo =
                PersonInfo(
                    Name = actor.Name.Trim(),
                    Role = actor.Character,
                    Type = PersonType.Actor,
                    SortOrder = actor.Order,
                    ImageUrl = tmdbClientManager.GetPosterUrl(actor.ProfilePath))
            if actor.Id > 0 then personInfo.SetProviderId(MetadataProvider.Tmdb, actor.Id.ToString("D"))
            personInfo
        cast
        |> Seq.sortBy (fun actor -> actor.Order) 
        |> Seq.take MaxCastMembers
        |> Seq.map toPersonInfo
    let ofCrews (crews: Crew seq) =
        let keepTypes = [| PersonType.Director; PersonType.Writer; PersonType.Producer |]
        let isKeepType personType = keepTypes.Contains(personType, StringComparer.OrdinalIgnoreCase)
        let toPersonInfo (r: struct {| Type: string; Crew: Crew |}) =
            new PersonInfo(
                Name = r.Crew.Name.Trim(),
                Role = r.Crew.Job,
                Type = r.Type)
        crews
        |> Seq.map (fun crew -> struct {| Type = mapCrewToPersonType crew; Crew = crew |})
        |> Seq.filter (fun r -> r.Type |> isKeepType || r.Crew.Job |> isKeepType)
        |> Seq.map toPersonInfo
    let toPersons (credits: Credits) =
        seq {
            credits.Cast |> Obj.map ofCasts
            credits.Crew |> Obj.map ofCrews
        } |> Seq.map (Obj.defaultValue Seq.empty) |> Seq.concat
    (^T: (member Credits: Credits) hasCredits)
    |> Obj.map toPersons
            