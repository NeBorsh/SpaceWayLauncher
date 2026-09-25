using NUnit.Framework;

namespace SpaceWay.Core.Tests;

public static class TestPaths
{
    /// <summary>A fresh directory under temp, isolated from others.</summary>
    public static string CreateTempRoot(string label) =>
        Path.Combine(Path.GetTempPath(), $"spaceway-{label}-{Guid.NewGuid():N}");

    /// <summary>
    /// Cleans up after a test without failing it.
    /// </summary>
    public static void DeleteQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            TestContext.Out.WriteLine($"Failed to delete {path}: {e.Message}");
        }
    }
}
