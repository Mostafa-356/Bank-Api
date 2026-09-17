using Bank.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Background service for processing loan-related tasks
/// </summary>
public class LoanBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LoanBackgroundService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromHours(1); // Run every hour

    public LoanBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<LoanBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loan Background Service started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDelinquentLoansAsync();
                await Task.Delay(_processingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in Loan Background Service");
                // Wait a shorter time before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
        }

        _logger.LogInformation("Loan Background Service stopped");
    }

    private async Task ProcessDelinquentLoansAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<ILoanService>();

        try
        {
            var processedCount = await loanService.ProcessDelinquentLoansAsync();
            
            if (processedCount > 0)
            {
                _logger.LogInformation("Processed {Count} delinquent loans", processedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing delinquent loans");
        }
    }
}