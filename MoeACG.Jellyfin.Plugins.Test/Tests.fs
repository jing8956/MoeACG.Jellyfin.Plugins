namespace MoeACG.Jellyfin.Plugins.Test

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type TestClass () =

    [<TestMethod>]
    [<DataRow("data\\Tv.yaml")>]
    member this.ResolveTest(filePath: string) =
        Assert.IsTrue(true);
