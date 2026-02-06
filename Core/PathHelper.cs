using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace NovaisFPS.Core;

/// <summary>
/// Utility class for path operations, including Windows 8.3 short path name conversion.
/// This helps resolve issues with PowerShell when paths contain special characters,
/// spaces, or are in OneDrive-synced folders.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PathHelper
{
    private const int MAX_PATH = 260;
    private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetShortPathName(
        [MarshalAs(UnmanagedType.LPTStr)] string lpszLongPath,
        [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszShortPath,
        int cchBuffer);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern uint GetFileAttributes(string lpFileName);

    /// <summary>
    /// Converts a long path to Windows 8.3 short path format.
    /// Returns the original path if conversion fails.
    /// </summary>
    public static string GetShortPath(string longPath)
    {
        if (string.IsNullOrWhiteSpace(longPath))
            return longPath;

        try
        {
            // Check if file/directory exists
            if (!File.Exists(longPath) && !Directory.Exists(longPath))
            {
                // If it doesn't exist, try to get the parent directory's short path
                var parentDir = Path.GetDirectoryName(longPath);
                if (!string.IsNullOrWhiteSpace(parentDir) && Directory.Exists(parentDir))
                {
                    var shortParent = GetShortPath(parentDir);
                    var fileName = Path.GetFileName(longPath);
                    return Path.Combine(shortParent, fileName);
                }
                return longPath; // Return original if we can't resolve
            }

            var shortPath = new StringBuilder(MAX_PATH);
            var result = GetShortPathName(longPath, shortPath, MAX_PATH);

            if (result > 0 && result < MAX_PATH)
            {
                return shortPath.ToString();
            }

            // If GetShortPathName fails, try getting parent directory's short path
            var parent = Path.GetDirectoryName(longPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                var shortParent = GetShortPath(parent);
                var fileName = Path.GetFileName(longPath);
                return Path.Combine(shortParent, fileName);
            }

            return longPath; // Fallback to original path
        }
        catch
        {
            // If anything fails, return the original path
            return longPath;
        }
    }

    /// <summary>
    /// Checks if a path is a reparse point (used by OneDrive and other sync services).
    /// </summary>
    public static bool IsReparsePoint(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
                return false;

            var attributes = GetFileAttributes(path);
            return (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a path is within a OneDrive directory.
    /// </summary>
    public static bool IsOneDrivePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var pathUpper = fullPath.ToUpperInvariant();

            // Check for common OneDrive paths
            if (pathUpper.Contains("ONEDRIVE"))
                return true;

            // Check for OneDrive reparse points
            var currentPath = fullPath;
            while (!string.IsNullOrWhiteSpace(currentPath))
            {
                if (IsReparsePoint(currentPath))
                {
                    // Check if it's OneDrive by examining the path
                    if (currentPath.ToUpperInvariant().Contains("ONEDRIVE"))
                        return true;
                }

                var parent = Path.GetDirectoryName(currentPath);
                if (parent == currentPath || string.IsNullOrWhiteSpace(parent))
                    break;
                currentPath = parent;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the OneDrive root directory if the path is within OneDrive.
    /// Returns null if not found.
    /// </summary>
    public static string? GetOneDriveRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var currentPath = fullPath;

            while (!string.IsNullOrWhiteSpace(currentPath))
            {
                var dirName = Path.GetFileName(currentPath);
                if (dirName.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("OneDrive - Personal", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("OneDrive - Business", StringComparison.OrdinalIgnoreCase))
                {
                    return currentPath;
                }

                var parent = Path.GetDirectoryName(currentPath);
                if (parent == currentPath || string.IsNullOrWhiteSpace(parent))
                    break;
                currentPath = parent;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
