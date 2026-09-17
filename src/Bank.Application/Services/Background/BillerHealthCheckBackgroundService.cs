using Bank.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Background service for monitoring biller health and connectivity
/// </summary>
public class BillerHealthCheckBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BillerHealthCheckBackgroundService> _logger;
    private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(15); // Check every 15 minutes

    public BillerHealthCheckBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BillerHealthCheckBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Biller Health Check Background Service started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthChecks();
                await Task.Delay(_healthCheckInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Biller Health Check Background Service");
                // Continue running even if there's an error
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken); // Wait 5 minutes before retrying
            }
        }

        _logger.LogInformation("Biller Health Check Background Service stopped");
    }

    private async Task PerformHealthChecks()
    {
        using var scope = _serviceProvider.CreateScope();
        var billerIntegrationService = scope.ServiceProvider.GetRequiredService<IBillerIntegrationService>();
        var billerRepository = scope.ServiceProvider.GetRequiredService<Bank.Domain.Interfaces.IBillerRepository>();
        var billerHealthCheckRepository = scope.ServiceProvider.GetRequiredService<Bank.Domain.Interfaces.IBillerHealthCheckRepository>();

        try
        {
            _logger.LogDebug("Starting biller health checks");

            // Get billers that are due for health check
            var billersDueForCheck = await billerHealthCheckRepository.GetBillersDueForHealthCheckAsync(_healthCheckInterval);
            
            if (!billersDueForCheck.Any())
            {
                _logger.LogDebug("No billers due for health check");
                return;
            }

            _logger.LogInformation("Performing health checks for {Count} billers", billersDueForCheck.Count);

            var healthCheckTasks = billersDueForCheck.Select(async billerId =>
            {
                try
                {
                    var healthCheck = await billerIntegrationService.CheckBillerHealthAsync(billerId);
                    
                    if (!healthCheck.IsHealthy)
                    {
                        _logger.LogWarning("Biller {BillerId} is unhealthy: {ErrorMessage}", 
                            billerId, healthCheck.ErrorMessage);
                    }
                    else
                    {
                        _logger.LogDebug("Biller {BillerId} is healthy (Response time: {ResponseTime}ms)", 
                            billerId, healthCheck.ResponseTime.TotalMilliseconds);
                    }

                    return healthCheck;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking health for biller {BillerId}", billerId);
                    return null;
                }
            });

            var healthCheckResults = await Task.WhenAll(healthCheckTasks);
            var successfulChecks = healthCheckResults.Count(r => r != null && r.IsHealthy);
            var failedChecks = healthCheckResults.Count(r => r != null && !r.IsHealthy);
            var errorChecks = healthCheckResults.Count(r => r == null);

            _logger.LogInformation("Health check completed: {Successful} healthy, {Failed} unhealthy, {Errors} errors",
                successfulChecks, failedChecks, errorChecks);

            // Check for billers with consecutive failures and potentially disable them
            await HandleConsecutiveFailures();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing biller health checks");
        }
    }

    private async Task HandleConsecutiveFailures()
    {
        using var scope = _serviceProvider.CreateScope();
        var billerHealthCheckRepository = scope.ServiceProvider.GetRequiredService<Bank.Domain.Interfaces.IBillerHealthCheckRepository>();
        var billerRepository = scope.ServiceProvider.GetRequiredService<Bank.Domain.Interfaces.IBillerRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<Bank.Domain.Interfaces.IUnitOfWork>();

        try
        {
            // Get billers with 5 or more consecutive failures
            var billersWithFailures = await billerHealthCheckRepository.GetBillersWithConsecutiveFailuresAsync(5);
            
            foreach (var healthCheck in billersWithFailures)
            {
                var biller = await billerRepository.GetByIdAsync(healthCheck.BillerId);
                if (biller != null && biller.IsActive)
                {
                    _logger.LogWarning("Biller {BillerId} ({BillerName}) has {ConsecutiveFailures} consecutive failures - considering deactivation",
                        biller.Id, biller.Name, healthCheck.ConsecutiveFailures);

                    // In a production system, you might want to:
                    // 1. Send alerts to administrators
                    // 2. Temporarily disable the biller
                    // 3. Notify customers about service issues
                    // 4. Implement escalation procedures

                    // For now, we'll just log the issue
                    // Uncomment the following lines to automatically disable billers with too many failures:
                    /*
                    if (healthCheck.ConsecutiveFailures >= 10)
                    {
                        biller.IsActive = false;
                        billerRepository.Update(biller);
                        await unitOfWork.SaveChangesAsync();
                        
                        _logger.LogWarning("Biller {BillerId} ({BillerName}) has been automatically deactivated due to {ConsecutiveFailures} consecutive failures",
                            biller.Id, biller.Name, healthCheck.ConsecutiveFailures);
                    }
                    */
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling consecutive failures");
        }
    }
}