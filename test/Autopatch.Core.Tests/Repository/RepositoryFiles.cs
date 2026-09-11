namespace Autopatch.Core.Tests.Repository;

/// <summary>
/// Access to files of the repository checkout the tests run in.
/// </summary>
internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath));

    public static IEnumerable<string> SourceFiles(string relativeDirectory, string pattern)
        => Directory.EnumerateFiles(Path.Combine(Root, relativeDirectory), pattern, SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "node_modules"));

    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AutoPatch.slnx")))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException("Repository root (AutoPatch.slnx) not found.");
    }
}
