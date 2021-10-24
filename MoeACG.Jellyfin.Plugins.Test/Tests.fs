namespace MoeACG.Jellyfin.Plugins.Test

open System
open System.IO
open System.Text
open Microsoft.VisualStudio.TestTools.UnitTesting
open MoeACG.Jellyfin.Plugins.VideoScrape

type IMap<'Key, 'Value> = 
    System.Collections.Generic.IReadOnlyDictionary<'Key, 'Value>

[<TestClass>]
type TestClass () =
    
    let serializer = new YamlDotNet.Serialization.Serializer()
    let deserializer = new YamlDotNet.Serialization.Deserializer()

    let toYaml (graph: obj) = serializer.Serialize(graph)
    let ofYaml (input: string) = deserializer.Deserialize<obj>(input)

    let rec toFloder (graph: IMap<obj, obj>) =
        Assert.AreEqual<int>(1, graph.Count)
        let pair = graph |> Seq.exactlyOne
        Assert.IsInstanceOfType(pair.Key, typeof<string>)
        Assert.IsInstanceOfType(pair.Value, typeof<obj seq>)
        { 
            Name = pair.Key :?> string; 
            Content = pair.Value :?> obj seq |> toContent  
        }
    and toContent (graph: obj seq) = 
        let empty = {  Floders = List.empty; Files = List.empty }
        graph 
        |> Seq.fold (
            fun c i -> 
                match i with
                | :? IMap<obj, obj> as graph -> 
                    { c with Floders = c.Floders @ [ toFloder graph ] }
                | :? string as file ->
                    { c with Files = c.Files @ [ file ] }
                | _ -> 
                    $"<{i.GetType()}> is not <{typeof<IMap<obj, obj>>}> or <{typeof<string>}>." 
                    |> Assert.Fail
                    empty
            ) empty
        
    let scrape = toContent >> scrape

    [<TestMethod>]
    [<DataRow("data\\Tv.yaml", "data\\Tv.expected.yaml", DisplayName = "Tv.yaml")>]
    member _.ScrapeTest(data: string, expected: string) =
        let readData = File.ReadAllText >> ofYaml
        let testData = readData data
        let expectedData = readData expected

        Assert.IsInstanceOfType(testData, typeof<obj seq>)
        let result = testData :?> obj seq |> scrape
        let validMap = Map.empty<string, string>

        // TODO: 断言刮削结果是否相同

        if validMap.Count > 0 then
            let errorYaml = validMap |> toYaml
            $"Scrape result assert failed.\r\n{errorYaml}" |> Assert.Fail
