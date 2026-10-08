using FileIndexer.Services;
using FileIndexer.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileIndexer.UI.Services;

// Per-session access to the single <Dialogs> and <Toast> rendered by AppShell, plus the
// plumbing every file operation shares (activity log entry, error dialog, success toast).
public sealed class UiFeedback(IServiceProvider services, ILogger<UiFeedback> logger)
{
    internal Dialogs? Dialogs { get; set; }
    internal Toast? Toast { get; set; }

    private Dialogs RequireDialogs =>
        Dialogs ?? throw new InvalidOperationException("No <Dialogs> rendered (AppShell renders one).");

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel, bool danger = false) =>
        RequireDialogs.ConfirmAsync(title, message, confirmLabel, danger);

    // Matches the onConflict callback of FileOperationsService.
    public Task<ConflictResolution> ResolveConflictAsync(string name, string destination) =>
        RequireDialogs.ResolveConflictAsync(name, destination);

    public Task ShowErrorAsync(string message) => RequireDialogs.ShowErrorAsync(message);

    // Uses the host's native picker when it has one, otherwise the in-page folder browser.
    public Task<string?> PickFolderAsync(string? initialPath = null) =>
        services.GetService<INativeFolderPicker>()?.PickFolderAsync() ?? RequireDialogs.PickFolderAsync(initialPath);

    public void ShowToast(string message, bool error = false) => Toast?.Show(message, error);

    // Runs an operation as an activity-log entry with progress. Failures (returned or thrown)
    // end in an error dialog, success in a toast; the result is returned for the caller's
    // follow-up (refresh, selection...).
    public async Task<OperationResult> RunOperationAsync(string description,
        Func<Action<int, int, string>, Task<OperationResult>> operation, Func<string> successMessage)
    {
        var activityLog = services.GetRequiredService<ActivityLogService>();
        var activity = activityLog.StartActivity(description);
        OperationResult result;
        try
        {
            result = await operation((current, total, name) => activityLog.ReportProgress(activity.Id, current, total, name));
        }
        catch (OperationCanceledException)
        {
            result = OperationResult.Cancelled();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Operation} failed", description);
            result = OperationResult.Failure(ex.Message);
        }

        if (result.IsSuccess)
        {
            var message = successMessage();
            activityLog.CompleteActivity(activity.Id, message);
            ShowToast(message);
        }
        else if (result.IsCancelled)
        {
            activityLog.FailActivity(activity.Id, "Cancelled");
        }
        else
        {
            activityLog.FailActivity(activity.Id, result.ErrorMessage ?? "Error");
            await ShowErrorAsync(result.ErrorMessage ?? "The operation failed");
        }
        return result;
    }
}
