namespace MoeACG.Jellyfin.Plugins.Configuration

open System.Text.RegularExpressions
open MediaBrowser.Model.Plugins

type PluginConfiguration() =
    inherit BasePluginConfiguration()

    member _.Version = typeof<PluginConfiguration>.Assembly.GetName().Version.ToString()

    member val VideoExtensions = 
        [|
            ".m4v";
            ".3gp";
            ".nsv";
            ".ts";
            ".ty";
            ".strm";
            ".rm";
            ".rmvb";
            ".ifo";
            ".mov";
            ".qt";
            ".divx";
            ".xvid";
            ".bivx";
            ".vob";
            ".nrg";
            ".img";
            ".iso";
            ".pva";
            ".wmv";
            ".asf";
            ".asx";
            ".ogm";
            ".m2v";
            ".avi";
            ".bin";
            ".dvr-ms";
            ".mpg";
            ".mpeg";
            ".mp4";
            ".mkv";
            ".avc";
            ".vp3";
            ".svq3";
            ".nuv";
            ".viv";
            ".dv";
            ".fli";
            ".flv";
            ".001";
            ".tp"
        |] with get, set
