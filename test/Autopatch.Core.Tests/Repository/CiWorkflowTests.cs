using System.Text.RegularExpressions;

namespace Autopatch.Core.Tests.Repository;

/// <summary>
/// Checks of <c>.github/workflows/nuget.yml</c>.
/// </summary>
[TestClass]
public sealed partial class CiWorkflowTests
{
    private static readonly string Workflow = RepositoryFiles.Read(".github/workflows/nuget.yml").ReplaceLineEndings("\n");

    [TestMethod]
    [TestCategory("Q2")]
    public void Q2_FailingTests_StopThePipelineBeforePublishing()
    {
        var testStep = Steps().Single(step => step.Name.Contains("Run tests", StringComparison.OrdinalIgnoreCase));

        testStep.Body.Should().NotContain("continue-on-error: true",
            "the test step may fail and the packages are still packed and pushed to nuget.org");
    }

    [TestMethod]
    [TestCategory("Q3")]
    public void Q3_VersionBumpStep_HasTheWorkflowDispatchTriggerItReads()
    {
        Workflow.Should().Contain("github.event.inputs.increment_version");

        TriggerBlock().Should().Contain("workflow_dispatch",
            "the version bump step reads github.event.inputs.increment_version, but workflow_dispatch is not a trigger, so the step never runs");
    }

    [TestMethod]
    [TestCategory("Q3")]
    public void Q3_TagPush_DeclaresContentsWritePermission()
    {
        Workflow.Should().Contain("git push");

        ContentsWritePermission().IsMatch(Workflow).Should().BeTrue(
            "the job pushes a tag, but does not declare 'permissions: contents: write' and depends on the repository default");
    }

    [TestMethod]
    [TestCategory("Q3")]
    public void Q3_Publishing_DoesNotSilentlySkipUnchangedVersions()
    {
        var publishesOnEveryPushToMain = TriggerBlock().Contains("push:") && TriggerBlock().Contains("main");
        var skipsDuplicates = Workflow.Contains("--skip-duplicate");

        (publishesOnEveryPushToMain && skipsDuplicates).Should().BeFalse(
            "every push to main publishes the fixed version from Directory.Build.props with --skip-duplicate, so a push without a version bump publishes nothing and nobody notices");
    }

    private static string TriggerBlock()
    {
        var lines = Workflow.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith("on:", StringComparison.Ordinal));
        return string.Join('\n', lines.Skip(start + 1).TakeWhile(line => line.Length == 0 || char.IsWhiteSpace(line[0])));
    }

    private static IEnumerable<(string Name, string Body)> Steps()
        => StepStart().Split(Workflow).Skip(1).Select(step => (step.Split('\n')[0], step));

    [GeneratedRegex(@"\n\s*- name:\s*")]
    private static partial Regex StepStart();

    [GeneratedRegex(@"permissions:\s*\n(?:[ \t]+.*\n)*?[ \t]+contents:\s*write")]
    private static partial Regex ContentsWritePermission();
}
