using Bank.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Background service for processing deposit-related tasks
/// </summary>
public class DepositBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DepositBackgroundService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromHours(1); // Run every hour

    public DepositBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DepositBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deposit Background Service started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDepositTasksAsync();
                await Task.Delay(_processingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Deposit Background Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Deposit Background Service");
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken); // Wait 5 minutes before retrying
            }
        }
    }

    private async Task ProcessDepositTasksAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var depositService = scope.ServiceProvider.GetRequiredService<IDepositService>();

        try
        {
            _logger.LogInformation("Starting deposit background processing");

            // Process daily interest accruals
            await ProcessInterestAccrualsAsync(depositService);

            // Process maturity notices
            await ProcessMaturityNoticesAsync(depositService);

            // Process pending maturity actions
            await ProcessPendingMaturityActionsAsync(depositService);

            // Process auto-renewals
            await ProcessAutoRenewalsAsync(depositService);

            _logger.LogInformation("Completed deposit background processing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during deposit background processing");
        }
    }

    private async Task ProcessInterestAccrualsAsync(IDepositService depositService)
    {
        try
        {
            _logger.LogInformation("Processing interest accruals");
            var success = await depositService.ProcessInterestAccrualsAsync();
            _logger.LogInformation("Interest accruals processing completed: {Success}", success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing interest accruals");
        }
    }

    private async Task ProcessMaturityNoticesAsync(IDepositService depositService)
    {
        try
        {
            _logger.LogInformation("Processing maturity notices");
            var success = await depositService.ProcessMaturityNoticesAsync();
            _logger.LogInformation("Maturity notices processing completed: {Success}", success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing maturity notices");
        }
    }

    private async Task ProcessPendingMaturityActionsAsync(IDepositService depositService)
    {
        try
        {
            _logger.LogInformation("Processing pending maturity actions");
            var success = await depositService.ProcessPendingMaturityActionsAsync();
            _logger.LogInformation("Pending maturity actions processing completed: {Success}", success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending maturity actions");
        }
    }

    private async Task ProcessAutoRenewalsAsync(IDepositService depositService)
    {
        try
        {
            _logger.LogInformation("Processing auto-renewals");
            var success = await depositService.ProcessAutoRenewalsAsync();
            _logger.LogInformation("Auto-renewals processing completed: {Success}", success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing auto-renewals");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deposit Background Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}