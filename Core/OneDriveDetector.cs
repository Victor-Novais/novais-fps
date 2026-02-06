using System.Runtime.Versioning;

namespace NovaisFPS.Core;

/// <summary>
/// Detects if the workspace is located within a OneDrive-synced directory.
/// OneDrive can cause issues with PowerShell script execution due to file locking,
/// reparse points, and synchronization delays.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OneDriveDetector
{
    private readonly Logger _log;

    public OneDriveDetector(Logger log)
    {
        _log = log;
    }

    /// <summary>
    /// Checks if the workspace root is within a OneDrive directory.
    /// </summary>
    public OneDriveDetectionResult DetectOneDrive(string workspaceRoot)
    {
        try
        {
            var isOneDrive = PathHelper.IsOneDrivePath(workspaceRoot);
            var oneDriveRoot = PathHelper.GetOneDriveRoot(workspaceRoot);
            var isReparsePoint = PathHelper.IsReparsePoint(workspaceRoot);

            return new OneDriveDetectionResult
            {
                IsOneDrivePath = isOneDrive,
                OneDriveRoot = oneDriveRoot,
                IsReparsePoint = isReparsePoint,
                WorkspaceRoot = workspaceRoot
            };
        }
        catch (Exception ex)
        {
            _log.Warn($"OneDrive detection failed: {ex.Message}");
            return new OneDriveDetectionResult
            {
                IsOneDrivePath = false,
                WorkspaceRoot = workspaceRoot
            };
        }
    }

    /// <summary>
    /// Logs a warning if OneDrive is detected, recommending moving to a local directory.
    /// </summary>
    public void LogOneDriveWarning(OneDriveDetectionResult result)
    {
        if (!result.IsOneDrivePath)
            return;

        _log.Warn("═══════════════════════════════════════════════════════════════════════════════");
        _log.Warn("WARNING: OneDrive Detected");
        _log.Warn("═══════════════════════════════════════════════════════════════════════════════");
        _log.Warn($"The NOVAIS FPS workspace is located in a OneDrive-synced directory:");
        _log.Warn($"  Workspace: {result.WorkspaceRoot}");
        if (!string.IsNullOrWhiteSpace(result.OneDriveRoot))
        {
            _log.Warn($"  OneDrive Root: {result.OneDriveRoot}");
        }
        _log.Warn("");
        _log.Warn("OneDrive can cause issues with PowerShell script execution:");
        _log.Warn("  • File locking during synchronization");
        _log.Warn("  • Reparse points that may confuse PowerShell");
        _log.Warn("  • Synchronization delays affecting script execution");
        _log.Warn("  • Potential permission issues");
        _log.Warn("");
        _log.Warn("RECOMMENDATION:");
        _log.Warn("  Move NOVAIS FPS to a local directory for maximum stability:");
        _log.Warn("    • C:\\NovaisFPS\\");
        _log.Warn("    • C:\\Program Files\\NovaisFPS\\");
        _log.Warn("    • C:\\Tools\\NovaisFPS\\");
        _log.Warn("    • Any local directory outside OneDrive");
        _log.Warn("");
        _log.Warn("The tool will attempt to work around OneDrive issues using 8.3 short paths,");
        _log.Warn("but for best results, use a local directory.");
        _log.Warn("═══════════════════════════════════════════════════════════════════════════════");
    }
}

public sealed class OneDriveDetectionResult
{
    public bool IsOneDrivePath { get; set; }
    public string? OneDriveRoot { get; set; }
    public bool IsReparsePoint { get; set; }
    public string WorkspaceRoot { get; set; } = "";
}
