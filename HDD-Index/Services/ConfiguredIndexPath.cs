using System;
using System.IO;

namespace HDD_Index.Services;

// Index paths in configuration accept both separators for cross-platform migration.
// Native data directories and local folder names retain their platform semantics.
internal static class ConfiguredIndexPath
{
    public static string Combine(string dataDirectory, string indexPath)
    {
        return Path.Combine(dataDirectory, NormalizeSeparators(indexPath));
    }

    public static string GetFileNameWithoutExtension(string indexPath)
    {
        return Path.GetFileNameWithoutExtension(NormalizeSeparators(indexPath));
    }

    public static bool AreEqual(string firstPath, string secondPath)
    {
        return string.Equals(
            NormalizeSeparators(firstPath),
            NormalizeSeparators(secondPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSeparators(string indexPath)
    {
        return indexPath.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }
}
