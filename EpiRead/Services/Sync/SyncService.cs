using CommonUtilities.Services.Sync;

namespace EpiRead.Services.Sync;

/// <summary>
///     Background sync service that runs scheduled maintenance tasks such as
///     syncing expired login attempts, expired reset tokens and other cleanup jobs.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class SyncService : SyncServiceBase<SyncService>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SyncService(IScheduleConfig<SyncService> config, ILogger<SyncService> logger,
        IServiceScopeFactory scopeFactory) : base(config, logger, scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    protected override async Task ExecuteSyncAsync(CancellationToken cancellationToken)
    {
        LoggerUtilities.Info("[Start] Starting background sync at " +
                             DateTime.Now.ToLocalTime());

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();

            // resolve services that perform database operations
            ILoginService loginService = scope.ServiceProvider.GetRequiredService<ILoginService>();
            IResetMfaService resetMfaService = scope.ServiceProvider.GetRequiredService<IResetMfaService>();
            IResetPasswordService resetPasswordService =
                scope.ServiceProvider.GetRequiredService<IResetPasswordService>();
            IUserInactivityService userInactivityService =
                scope.ServiceProvider.GetRequiredService<IUserInactivityService>();

            // Login attempts cleanup
            LoggerUtilities.Info(
                "[Process] Starting database cleanup for expired login attempts");
            await loginService.SyncExpiredLoginAttemptsAsync();
            LoggerUtilities.Info(
                "[End] Completed database cleanup for expired login attempts");

            // Reset MFA cleanup
            LoggerUtilities.Info(
                "[Process] Starting database cleanup for expired reset MFA entries");
            await resetMfaService.SyncExpiredResetMfaAsync();
            LoggerUtilities.Info(
                "[End] Completed database cleanup for expired reset MFA entries");

            // Reset password cleanup
            LoggerUtilities.Info(
                "[Process] Starting database cleanup for expired reset password entries");
            await resetPasswordService.SyncExpiredResetPasswordAsync();
            LoggerUtilities.Info(
                "[End] Completed database cleanup for expired reset password entries");

            // User inactivity (auto-deactivate)
            LoggerUtilities.Info(
                "[Process] Starting database operation for user inactivity auto-deactivate");
            await userInactivityService.SyncAutoDeactivateUserAsync();
            LoggerUtilities.Info(
                "[End] Completed database operation for user inactivity auto-deactivate");
        }
        catch (OperationCanceledException)
        {
            LoggerUtilities.Warning("[End] Cancelled by token.");
            throw;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] An error occurred while executing sync: " + ex.Message);
            throw;
        }
        finally
        {
            LoggerUtilities.Info("[End] Finished background sync at " + DateTime.Now.ToLocalTime());
        }
    }
}