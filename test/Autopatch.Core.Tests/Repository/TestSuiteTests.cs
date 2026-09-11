using System.Text.RegularExpressions;

namespace Autopatch.Core.Tests.Repository;

[TestClass]
public sealed partial class TestSuiteTests
{
    [TestMethod]
    [TestCategory("Q1")]
    public void Q1_TestProjects_ContainNoPlaceholderTests()
    {
        var placeholders = RepositoryFiles.SourceFiles("test", "*.cs")
            .Where(file => PlaceholderTest().IsMatch(File.ReadAllText(file)))
            .Select(RepositoryFiles.Relative);

        placeholders.Should().BeEmpty("placeholder tests (always-true assertions, empty test methods) only pretend coverage");
    }

    [GeneratedRegex(@"Assert\.IsTrue\(true\)|void TestMethod1\(\)\s*\{\s*\}")]
    private static partial Regex PlaceholderTest();
}
