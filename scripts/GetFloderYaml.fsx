// 将文件夹结构输出成 YAML 以方便查看并作为发现器测试数据源
#r "PresentationFramework"
#r "nuget:Ookii.Dialogs.Wpf"
#r "nuget:YamlDotNet"

open System
open System.IO
open System.Text
open Ookii.Dialogs.Wpf

let openDialog = 
    new VistaFolderBrowserDialog(
        UseDescriptionForTitle = true,
        Description = "选择要导出的文件夹",
        RootFolder = System.Environment.SpecialFolder.MyComputer,
        ShowNewFolderButton = false)
if openDialog.ShowDialog() = Nullable(true) then
    let rootFloder = openDialog.SelectedPath |> DirectoryInfo
    let saveDialog = 
        new VistaSaveFileDialog(
            Title = "选择保存的位置",
            Filter = "YAML files (*.yaml)|*.yaml|All files (*.*)|*.*",
            DefaultExt = "yaml",
            FileName = $"{rootFloder.Name}.yaml")
    if saveDialog.ShowDialog() = Nullable(true) then
        let serializer = new YamlDotNet.Serialization.Serializer()
        let rec loop (floder: DirectoryInfo) =
            seq {
                floder.GetDirectories()
                |> Seq.map (fun f -> Map.empty.Add(f.Name, loop f))
                |> Seq.cast<obj>
                floder.GetFiles()
                |> Seq.map (fun f -> f.Name)
                |> Seq.cast<obj>
            } |> Seq.concat
        use writter = 
            new StreamWriter(
                saveDialog.FileName, 
                false, new UTF8Encoding(false))
        serializer.Serialize(writter, loop rootFloder)
        writter.Flush()
