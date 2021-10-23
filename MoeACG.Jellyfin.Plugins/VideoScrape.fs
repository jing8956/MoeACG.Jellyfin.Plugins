module MoeACG.Jellyfin.Plugins.VideoScrape

open MediaBrowser.Controller.Entities.TV

type Floder = 
    {
        Name: string
        Content: FloderContent
    }
and FloderContent =
    {
        Floders: Floder list
        Files: string list
    }

let scrape (content: FloderContent) : Series list = []
