using System.Reflection;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using Autopatch.Client.Services;
using Autopatch.Server.Models;
using Autopatch.Server.Services;

namespace Autopatch.Core.Tests.Repository;

/// <summary>
/// Checks that the documentation matches the code.
/// </summary>
[TestClass]
public sealed partial class DocumentationTests
{
    private static readonly Assembly[] PackageAssemblies =
    [
        typeof(OperationContainer<>).Assembly,
        typeof(ITrackedCollectionManager).Assembly,
        typeof(IAutoPatchClient).Assembly,
    ];

    [TestMethod]
    [TestCategory("Q4")]
    [DataRow("AddObjectType", AnyMember)]
    [DataRow("KeyProperty", AnyMember)]
    [DataRow("NotifyChanged", AnyMember)]
    [DataRow("NotifyAdded", AnyMember)]
    [DataRow("NotifyBatch", AnyMember)]
    [DataRow("IChangeHandler", AnyType)]
    [DataRow("SubscribeResult", AnyType)]
    [DataRow("StartAsync", nameof(IAutoPatchClient))]
    [DataRow("StopAsync", nameof(IAutoPatchClient))]
    [DataRow("OnError", nameof(IAutoPatchClient))]
    [DataRow("OnConnectionLost", nameof(IAutoPatchClient))]
    [DataRow("OnReconnected", nameof(IAutoPatchClient))]
    [DataRow("RequireConfirmation", nameof(ClientChangePolicy))]
    public void Q4_ApiDescribedInSpec_ExistsInThePackages(string apiName, string location)
    {
        var spec = RepositoryFiles.Read("spec.md");
        if (!Regex.IsMatch(spec, $@"\b{apiName}\b"))
        {
            return;
        }

        var exportedTypes = PackageAssemblies.SelectMany(a => a.GetExportedTypes()).ToArray();
        var names = location switch
        {
            AnyType => exportedTypes.Select(t => t.Name.Split('`')[0]),
            AnyMember => exportedTypes.SelectMany(t => t.GetMembers()).Select(m => m.Name),
            _ => exportedTypes.Single(t => t.Name == location).GetMembers().Select(m => m.Name),
        };

        names.Should().Contain(apiName,
            "spec.md describes '{0}' ({1}), but the AutoPatch packages do not offer it", apiName, location);
    }

    private const string AnyType = "any type";
    private const string AnyMember = "any member";

    [TestMethod]
    [TestCategory("Q5")]
    [DataRow(".github/copilot-instructions.md")]
    [DataRow("Roadmap.md")]
    [DataRow("test-coverage.sh")]
    [DataRow("demo/Autopatch.Demo.WPF/App.xaml.cs")]
    public void Q5_Files_DoNotReferToDotNet9(string relativePath)
    {
        var content = RepositoryFiles.Read(relativePath);

        DotNet9().Matches(content).Select(m => m.Value)
            .Should().BeEmpty("all projects target net10.0");
    }

    [TestMethod]
    [TestCategory("Q5")]
    public void Q5_CopilotInstructions_DoNotStateAnOutdatedTestCount()
    {
        var instructions = RepositoryFiles.Read(".github/copilot-instructions.md");
        var testMethods = RepositoryFiles.SourceFiles("test", "*.cs")
            .Sum(file => TestMethodAttribute().Matches(File.ReadAllText(file)).Count);

        var documentedTotals = DocumentedTotal().Matches(instructions).Select(m => int.Parse(m.Groups[1].Value));

        documentedTotals.Where(total => total < testMethods).Should().BeEmpty(
            "the instructions promise a fixed number of tests, but the test projects contain {0} test methods", testMethods);
    }

    /// <remarks>
    /// Review finding Q5 claims SourceLink is disabled because the package reference is commented out. Since the .NET 8 SDK
    /// SourceLink for GitHub is part of the SDK, so the PDBs contain SourceLink information anyway. Kept as a regression test.
    /// </remarks>
    [TestMethod]
    [TestCategory("Q5")]
    public void Q5_SourceLink_IsEmbeddedInThePackagePdbs()
    {
        var sourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");
        var pdb = Path.ChangeExtension(typeof(OperationContainer<>).Assembly.Location, ".pdb");
        using var provider = MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(pdb));
        var reader = provider.GetMetadataReader();

        reader.CustomDebugInformation.Select(handle => reader.GetGuid(reader.GetCustomDebugInformation(handle).Kind))
            .Should().Contain(sourceLinkKind);
    }

    [GeneratedRegex(@"\.NET 9|net9\.0|channel 9\.0|9\.0\.x")]
    private static partial Regex DotNet9();

    [GeneratedRegex(@"total: (\d+)")]
    private static partial Regex DocumentedTotal();

    [GeneratedRegex(@"\[(Data)?TestMethod\]")]
    private static partial Regex TestMethodAttribute();
}
