namespace SpaceWay.Core.Content;

/// <summary>What the launcher is doing while the player waits.</summary>
public enum ContentStage
{
    AskingServer,
    OpeningBundle,
    CheckingVersion,
    FetchingManifest,
    DownloadingFiles,
    DownloadingZip,
    StoringFiles,
    DownloadingEngine,
    DownloadingModules,
    Committing,
    Culling,
    StartingGame,
    Done,
}

/// <summary>
/// Update progress to show to the player.
/// </summary>
/// <param name="Stage">Current stage. The UI picks the label.</param>
/// <param name="Done">Amount done, in files or bytes.</param>
/// <param name="Total">Total amount, or null if unknown (indeterminate progress bar).</param>
/// <param name="InBytes">Whether the count is in bytes, which affects formatting.</param>
public readonly record struct ContentProgress(
    ContentStage Stage,
    long Done = 0,
    long? Total = null,
    bool InBytes = false);
