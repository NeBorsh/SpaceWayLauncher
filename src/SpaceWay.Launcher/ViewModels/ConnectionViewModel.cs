using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SpaceWay.Core.Connecting;
using SpaceWay.Core.Content;
using SpaceWay.Core.Localization;

namespace SpaceWay.Launcher.ViewModels;

/// <summary>
/// Connection window: shows what the launcher is doing while the player waits.
/// </summary>
public sealed partial class ConnectionViewModel
    : DialogViewModel<bool>, IProgress<ContentProgress>, IPrivacyPolicyPrompt
{
    /// <summary>
    /// How often the progress bar updates.
    /// </summary>
    private static readonly TimeSpan RedrawInterval = TimeSpan.FromMilliseconds(80);

    /// <summary>
    /// How long to wait before considering the launch successful.
    /// </summary>
    private static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(3);

    private readonly GameConnector _connector;
    private readonly DialogService _dialogs;
    private readonly string _address;
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>Sign-in wizard. True if an account was added.</summary>
    private readonly Func<Task<bool>>? _signIn;

    /// <summary>Whether mods were used; a retry after sign-in uses the same setting.</summary>
    private bool _useMods = true;

    /// <summary>
    /// Launching a bundle from a file rather than connecting; <c>_address</c> is then
    /// the file path. One window serves both: stages, progress, errors and
    /// retry without mods are shared.
    /// </summary>
    private readonly bool _isBundle;

    private DateTimeOffset _lastRedraw = DateTimeOffset.MinValue;
    private ContentProgress _latest;

    [ObservableProperty]
    private string _stageText = string.Empty;

    [ObservableProperty]
    private string _detailText = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private bool _isFinished;

    /// <summary>
    /// Whether to offer a retry without mods.
    /// </summary>
    [ObservableProperty]
    private bool _canRetryWithoutMods;

    /// <summary>
    /// Whether to offer signing in.
    /// </summary>
    [ObservableProperty]
    private bool _canSignIn;

    public ConnectionViewModel(
        GameConnector connector,
        DialogService dialogs,
        string address,
        string serverName,
        int modCount = 0,
        Func<Task<bool>>? signIn = null,
        bool isBundle = false)
    {
        ModCount = modCount;
        _signIn = signIn;
        _isBundle = isBundle;
        _connector = connector;
        _dialogs = dialogs;
        _address = address;
        ServerName = serverName;
        StageText = FirstStage();
    }

    public string ServerName { get; }

    public override string Title => Loc.T(_isBundle ? "bundle-title" : "connect-title");

    /// <summary>Number of mods sent to the game. Zero means a regular launch.</summary>
    public int ModCount { get; private set; }

    public bool HasMods => ModCount > 0;

    public string ModsText => Loc.T("connect-mods", ("count", ModCount));

    public bool HasError => ErrorText != null;

    /// <summary>
    /// This window does not close on backdrop click.
    /// </summary>
    public override bool DismissOnBackgroundClick => false;

    /// <summary>Starts the connection and drives it to completion.</summary>
    public async Task Run(bool useMods = true)
    {
        _useMods = useMods;

        try
        {
            using var session = _isBundle
                ? await _connector.LaunchBundle(_address, this, useMods, _cancellation.Token)
                : await _connector.Connect(_address, this, this, useMods, _cancellation.Token);

            Apply(new ContentProgress(ContentStage.StartingGame));

            if (await session.DiedOnStartup(StartupGrace, _cancellation.Token))
            {
                Log.Warning("Game exited right after launch with code {Code}", session.ExitCode);
                Fail(DescribeStartupFailure(session));
                return;
            }

            Log.Information("Game running, process {Pid}", session.ProcessId);
            Close(true);
        }
        catch (OperationCanceledException)
        {
            Fail(Loc.T("connect-cancelled"));
        }
        catch (ConnectException e) when (e.Key == "error-auth-no-account" && _signIn != null)
        {
            CanSignIn = true;
            Fail(e.Message);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to connect to {Address}", _address);
            Fail(e.Message);
        }
    }

    /// <summary>
    /// Explains a startup crash.
    /// </summary>
    private string DescribeStartupFailure(GameSession session)
    {
        var failure = session.DiagnoseFailure();

        if (failure.Kind != StartupFailureKind.ModRejected)
            return Loc.T("connect-failed");

        CanRetryWithoutMods = session.HasMods;

        Log.Warning(
            "Engine sandbox rejected assembly {Assembly} due to type {Type}",
            failure.Assembly, failure.Type);

        if (failure.Type != null && failure.Assembly != null)
            return Loc.T("connect-mod-rejected", ("assembly", failure.Assembly), ("type", failure.Type));

        return failure.Type != null
            ? Loc.T("connect-mod-rejected-type", ("type", failure.Type))
            : Loc.T("connect-mod-rejected-assembly", ("assembly", failure.Assembly ?? string.Empty));
    }

    /// <summary>Retries the connection with mods disabled.</summary>
    [RelayCommand]
    private async Task RetryWithoutMods()
    {
        CanRetryWithoutMods = false;

        ModCount = 0;
        OnPropertyChanged(nameof(HasMods));

        ResetForRetry();
        await Run(useMods: false);
    }

    /// <summary>Sign-in over the window and, if successful, a connection retry.</summary>
    [RelayCommand]
    private async Task SignIn()
    {
        if (_signIn == null || !await _signIn())
            return;

        CanSignIn = false;
        ResetForRetry();
        await Run(_useMods);
    }

    private void ResetForRetry()
    {
        IsFinished = false;
        ErrorText = null;
        OnPropertyChanged(nameof(HasError));
        IsIndeterminate = true;
        StageText = FirstStage();
    }

    private string FirstStage() => Loc.T(_isBundle ? "stage-opening-bundle" : "stage-asking-server");

    /// <summary>
    /// Shows the server's privacy policy over the connection window.
    /// </summary>
    public async Task<bool> Ask(
        ServerPrivacyPolicy policy,
        bool versionChanged,
        CancellationToken cancel = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new PrivacyPolicyViewModel(ServerName, policy, versionChanged);

            await using var registration = cancel.Register(dialog.Cancel);

            return await _dialogs.ShowAsync(dialog);
        });
    }

    /// <summary>
    /// Receives progress messages from background threads.
    /// </summary>
    public void Report(ContentProgress value)
    {
        _latest = value;

        var now = DateTimeOffset.UtcNow;
        var important = value.Stage is ContentStage.StartingGame or ContentStage.Done;

        if (!important && now - _lastRedraw < RedrawInterval)
            return;

        _lastRedraw = now;
        Dispatcher.UIThread.Post(() => Apply(_latest));
    }

    /// <summary>Escape cancels the connection rather than just hiding the window.</summary>
    public override void Cancel()
    {
        _cancellation.Cancel();
        base.Cancel();
    }

    [RelayCommand]
    private void CancelConnection()
    {
        _cancellation.Cancel();
        StageText = Loc.T("connect-cancelled");
    }

    [RelayCommand]
    private void Dismiss() => Close(false);

    private void Apply(ContentProgress progress)
    {
        if (IsFinished)
            return;

        StageText = StageName(progress.Stage);

        if (progress.Total is not { } total || total <= 0)
        {
            IsIndeterminate = true;
            DetailText = string.Empty;
            return;
        }

        IsIndeterminate = false;
        ProgressValue = Math.Clamp(progress.Done / (double)total * 100, 0, 100);

        DetailText = progress.InBytes
            ? Loc.T("connect-progress-bytes",
                ("done", ByteFormat.Format(progress.Done)), ("total", ByteFormat.Format(total)))
            : Loc.T("connect-progress-files", ("done", progress.Done), ("total", total));
    }

    private void Fail(string message)
    {
        IsFinished = true;
        ErrorText = message;
        IsIndeterminate = false;
        ProgressValue = 0;
        DetailText = string.Empty;
        OnPropertyChanged(nameof(HasError));
    }

    private static string StageName(ContentStage stage) => stage switch
    {
        ContentStage.AskingServer => Loc.T("stage-asking-server"),
        ContentStage.OpeningBundle => Loc.T("stage-opening-bundle"),
        ContentStage.CheckingVersion => Loc.T("stage-checking-version"),
        ContentStage.FetchingManifest => Loc.T("stage-fetching-manifest"),
        ContentStage.DownloadingFiles => Loc.T("stage-downloading-files"),
        ContentStage.DownloadingZip => Loc.T("stage-downloading-zip"),
        ContentStage.StoringFiles => Loc.T("stage-storing-files"),
        ContentStage.DownloadingEngine => Loc.T("stage-downloading-engine"),
        ContentStage.DownloadingModules => Loc.T("stage-downloading-modules"),
        ContentStage.Committing => Loc.T("stage-committing"),
        ContentStage.Culling => Loc.T("stage-culling"),
        ContentStage.StartingGame => Loc.T("stage-starting-game"),
        ContentStage.Done => Loc.T("stage-done"),
        _ => string.Empty,
    };
}
