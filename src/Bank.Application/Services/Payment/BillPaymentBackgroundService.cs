using Bank.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Background service for processing scheduled bill payments
/// </summary>
public class BillPaymentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BillPaymentBackgroundService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(30); // Run every 30 minutes

    public BillPaymentBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BillPaymentBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bill Payment Background Service started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledPayments();
                await Task.Delay(_processingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Bill Payment Background Service");
                // Continue running even if there's an error
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken); // Wait 5 minutes before retrying
            }
        }

        _logger.LogInformation("Bill Payment Background Service stopped");
    }

    private async Task ProcessScheduledPayments()
    {
        using var scope = _serviceProvider.CreateScope();
        var billPaymentService = scope.ServiceProvider.GetRequiredService<IBillPaymentService>();

        try
        {
            _logger.LogDebug("Processing scheduled bill payments");
            
            var results = await billPaymentService.ProcessBillPaymentAsync();
            
            if (results.Any())
            {
                var successCount = results.Count(r => r.Success);
                var failureCount = results.Count(r => !r.Success);
                
                _logger.LogInformation("Processed {TotalCount} bill payments: {SuccessCount} successful, {FailureCount} failed",
                    results.Count, successCount, failureCount);
            }
            else
            {
                _logger.LogDebug("No scheduled bill payments to process");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled bill payments");
        }
    }
}