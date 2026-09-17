using Bank.Application.Interfaces;
using Bank.Application.DTOs;
using Bank.Application.DTOs.Transaction.Fraud;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Fraud detection service with pattern analysis and risk scoring
/// </summary>
public class FraudDetectionService : IFraudDetectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FraudDetectionService> _logger;
    
    // Configurable thresholds
    private const decimal HighRiskTransactionAmount = 10000m;
    private const decimal CriticalRiskTransactionAmount = 50000m;
    private const int MaxTransactionsPerHour = 10;
    private const int MaxTransactionsPerDay = 50;

    public FraudDetectionService(IUnitOfWork unitOfWork, ILogger<FraudDetectionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<FraudAnalysisResult> AnalyzeTransactionAsync(Transaction transaction, User user)
    {
        try
        {
            var result = new FraudAnalysisResult
            {
                RiskScore = 0,
                RiskLevel = FraudRiskLevel.Low
            };

            // Rule 1: High amount transactions
            if (transaction.Amount >= CriticalRiskTransactionAmount)
            {
                result.RiskScore += 80;
                result.TriggeredRules.Add("Critical amount transaction");
            }
            else if (transaction.Amount >= HighRiskTransactionAmount)
            {
                result.RiskScore += 40;
                result.TriggeredRules.Add("High amount transaction");
            }

            // Rule 2: Transaction frequency analysis
            var recentTransactions = await GetRecentTransactionsAsync(user.Id, TimeSpan.FromHours(1));
            if (recentTransactions.Count >= MaxTransactionsPerHour)
            {
                result.RiskScore += 60;
                result.TriggeredRules.Add("High transaction frequency (hourly)");
            }

            var dailyTransactions = await GetRecentTransactionsAsync(user.Id, TimeSpan.FromDays(1));
            if (dailyTransactions.Count >= MaxTransactionsPerDay)
            {
                result.RiskScore += 30;
                result.TriggeredRules.Add("High transaction frequency (daily)");
            }

            // Rule 3: Unusual transaction patterns
            var avgTransactionAmount = await GetAverageTransactionAmountAsync(user.Id, TimeSpan.FromDays(30));
            if (avgTransactionAmount > 0 && transaction.Amount > avgTransactionAmount * 5)
            {
                result.RiskScore += 50;
                result.TriggeredRules.Add("Transaction amount significantly above average");
            }

            // Rule 4: Time-based anomalies
            var currentHour = DateTime.UtcNow.Hour;
            if (currentHour < 6 || currentHour > 23) // Late night/early morning transactions
            {
                result.RiskScore += 20;
                result.TriggeredRules.Add("Unusual transaction time");
            }

            // Rule 5: Account age and transaction history
            var accountAge = DateTime.UtcNow - user.CreatedAt;
            if (accountAge.TotalDays < 7 && transaction.Amount > 1000)
            {
                result.RiskScore += 40;
                result.TriggeredRules.Add("Large transaction from new account");
            }

            // Determine risk level and actions
            result.RiskLevel = result.RiskScore switch
            {
                >= 80 => FraudRiskLevel.Critical,
                >= 60 => FraudRiskLevel.High,
                >= 30 => FraudRiskLevel.Medium,
                _ => FraudRiskLevel.Low
            };

            result.IsSuspicious = result.RiskScore >= 30;
            result.ShouldBlock = result.RiskScore >= 80;
            result.ShouldFreeze = result.RiskScore >= 90;

            result.RecommendedAction = result.RiskLevel switch
            {
                FraudRiskLevel.Critical => "Block transaction and freeze account",
                FraudRiskLevel.High => "Require additional verification",
                FraudRiskLevel.Medium => "Flag for manual review",
                _ => "Allow transaction"
            };

            if (result.IsSuspicious)
            {
                _logger.LogWarning("Suspicious transaction detected for user {UserId}. Risk Score: {RiskScore}, Rules: {Rules}",
                    user.Id, result.RiskScore, string.Join(", ", result.TriggeredRules));

                // Report suspicious activity
                await ReportSuspiciousActivityAsync(new SuspiciousActivityReport
                {
                    UserId = user.Id,
                    ActivityType = "Transaction",
                    Description = $"Suspicious transaction of {transaction.Amount:C}",
                    RiskScore = result.RiskScore,
                    Metadata = new Dictionary<string, object>
                    {
                        ["TransactionId"] = transaction.Id,
                        ["Amount"] = transaction.Amount,
                        ["Type"] = transaction.Type.ToString(),
                        ["TriggeredRules"] = result.TriggeredRules
                    }
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing transaction {TransactionId} for fraud", transaction.Id);
            return new FraudAnalysisResult { RiskLevel = FraudRiskLevel.Low, RiskScore = 0 };
        }
    }

    public async Task<FraudAnalysisResult> AnalyzeLoginAttemptAsync(Guid userId, string ipAddress, string userAgent)
    {
        try
        {
            var result = new FraudAnalysisResult
            {
                RiskScore = 0,
                RiskLevel = FraudRiskLevel.Low
            };

            // Rule 1: Multiple failed login attempts
            var recentFailedAttempts = await GetRecentFailedLoginAttemptsAsync(userId, TimeSpan.FromMinutes(15));
            if (recentFailedAttempts >= 5)
            {
                result.RiskScore += 70;
                result.TriggeredRules.Add("Multiple failed login attempts");
            }

            // Rule 2: Login from new IP address
            var isNewIpAddress = await IsNewIpAddressAsync(userId, ipAddress);
            if (isNewIpAddress)
            {
                result.RiskScore += 30;
                result.TriggeredRules.Add("Login from new IP address");
            }

            // Rule 3: Unusual login time
            var currentHour = DateTime.UtcNow.Hour;
            if (currentHour < 6 || currentHour > 23)
            {
                result.RiskScore += 20;
                result.TriggeredRules.Add("Unusual login time");
            }

            // Rule 4: Multiple IP addresses in short time
            var recentIpAddresses = await GetRecentLoginIpAddressesAsync(userId, TimeSpan.FromHours(1));
            if (recentIpAddresses.Count > 3)
            {
                result.RiskScore += 50;
                result.TriggeredRules.Add("Multiple IP addresses in short time");
            }

            // Determine risk level
            result.RiskLevel = result.RiskScore switch
            {
                >= 80 => FraudRiskLevel.Critical,
                >= 60 => FraudRiskLevel.High,
                >= 30 => FraudRiskLevel.Medium,
                _ => FraudRiskLevel.Low
            };

            result.IsSuspicious = result.RiskScore >= 30;
            result.ShouldBlock = result.RiskScore >= 80;

            if (result.IsSuspicious)
            {
                _logger.LogWarning("Suspicious login attempt for user {UserId} from IP {IpAddress}. Risk Score: {RiskScore}",
                    userId, ipAddress, result.RiskScore);

                await ReportSuspiciousActivityAsync(new SuspiciousActivityReport
                {
                    UserId = userId,
                    ActivityType = "Login",
                    Description = "Suspicious login attempt",
                    RiskScore = result.RiskScore,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Metadata = new Dictionary<string, object>
                    {
                        ["TriggeredRules"] = result.TriggeredRules
                    }
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing login attempt for user {UserId}", userId);
            return new FraudAnalysisResult { RiskLevel = FraudRiskLevel.Low };
        }
    }

    public async Task<bool> ShouldFreezeAccountAsync(Guid userId)
    {
        try
        {
            // Check recent suspicious activities
            var recentActivities = await GetRecentSuspiciousActivitiesAsync(userId, TimeSpan.FromHours(24));
            var totalRiskScore = recentActivities.Sum(a => a.RiskScore);

            // Freeze if total risk score exceeds threshold
            return totalRiskScore >= 150;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if account {UserId} should be frozen", userId);
            return false;
        }
    }

    public async Task<FraudRiskScore> GetUserRiskScoreAsync(Guid userId)
    {
        try
        {
            var recentActivities = await GetRecentSuspiciousActivitiesAsync(userId, TimeSpan.FromDays(7));
            var totalRiskScore = recentActivities.Sum(a => a.RiskScore);
            var averageRiskScore = recentActivities.Any() ? totalRiskScore / recentActivities.Count : 0;

            var riskLevel = averageRiskScore switch
            {
                >= 80 => FraudRiskLevel.Critical,
                >= 60 => FraudRiskLevel.High,
                >= 30 => FraudRiskLevel.Medium,
                _ => FraudRiskLevel.Low
            };

            var riskFactors = new List<string>();
            if (recentActivities.Any(a => a.ActivityType == "Transaction"))
                riskFactors.Add("Suspicious transactions");
            if (recentActivities.Any(a => a.ActivityType == "Login"))
                riskFactors.Add("Suspicious login attempts");

            return new FraudRiskScore
            {
                UserId = userId,
                Score = averageRiskScore,
                Level = riskLevel,
                CalculatedAt = DateTime.UtcNow,
                RiskFactors = riskFactors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk score for user {UserId}", userId);
            return new FraudRiskScore { UserId = userId, Score = 0, Level = FraudRiskLevel.Low };
        }
    }

    public async Task ReportSuspiciousActivityAsync(SuspiciousActivityReport report)
    {
        try
        {
            _logger.LogWarning("Suspicious activity reported: User {UserId}, Type: {ActivityType}, Score: {RiskScore}, Description: {Description}",
                report.UserId, report.ActivityType, report.RiskScore, report.Description);

            // Feature not yet implemented - requires SuspiciousActivity table and alert system
            throw new NotImplementedException("Suspicious activity reporting is not yet implemented. Requires database schema and alert system implementation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting suspicious activity for user {UserId}", report.UserId);
            throw;
        }
    }

    public async Task<List<FraudRule>> GetActiveRulesAsync()
    {
        // Return default fraud detection rules
        // In production, these would be stored in database and configurable
        return await Task.FromResult(new List<FraudRule>
        {
            new() { Id = "high_amount", Name = "High Amount Transaction", Type = FraudRuleType.TransactionAmount, IsActive = true, RiskScore = 40 },
            new() { Id = "critical_amount", Name = "Critical Amount Transaction", Type = FraudRuleType.TransactionAmount, IsActive = true, RiskScore = 80 },
            new() { Id = "high_frequency", Name = "High Transaction Frequency", Type = FraudRuleType.TransactionFrequency, IsActive = true, RiskScore = 60 },
            new() { Id = "unusual_time", Name = "Unusual Transaction Time", Type = FraudRuleType.TimeAnomaly, IsActive = true, RiskScore = 20 },
            new() { Id = "new_account", Name = "Large Transaction from New Account", Type = FraudRuleType.BehaviorAnomaly, IsActive = true, RiskScore = 40 }
        });
    }

    public async Task UpdateRuleAsync(FraudRule rule)
    {
        // Feature not yet implemented - requires database persistence
        throw new NotImplementedException("Fraud rule updates are not yet implemented. Requires database schema and persistence layer implementation.");
    }

    #region Private Helper Methods

    private async Task<List<Transaction>> GetRecentTransactionsAsync(Guid userId, TimeSpan timeSpan)
    {
        var cutoffTime = DateTime.UtcNow - timeSpan;
        return (await _unitOfWork.Repository<Transaction>()
            .FindAsync(t => (t.FromAccount.UserId == userId || t.ToAccount.UserId == userId) && t.CreatedAt >= cutoffTime))
            .ToList();
    }

    private async Task<decimal> GetAverageTransactionAmountAsync(Guid userId, TimeSpan timeSpan)
    {
        var transactions = await GetRecentTransactionsAsync(userId, timeSpan);
        return transactions.Any() ? transactions.Average(t => t.Amount) : 0;
    }

    private async Task<int> GetRecentFailedLoginAttemptsAsync(Guid userId, TimeSpan timeSpan)
    {
        // Feature not yet implemented - requires login attempt tracking
        throw new NotImplementedException("Failed login attempt tracking is not yet implemented. Requires database schema and tracking system implementation.");
    }

    private async Task<bool> IsNewIpAddressAsync(Guid userId, string ipAddress)
    {
        // Feature not yet implemented - requires IP address tracking
        throw new NotImplementedException("IP address tracking is not yet implemented. Requires database schema and tracking system implementation.");
    }

    private async Task<List<string>> GetRecentLoginIpAddressesAsync(Guid userId, TimeSpan timeSpan)
    {
        // Feature not yet implemented - requires IP address tracking
        throw new NotImplementedException("IP address tracking is not yet implemented. Requires database schema and tracking system implementation.");
    }

    private async Task<List<SuspiciousActivityReport>> GetRecentSuspiciousActivitiesAsync(Guid userId, TimeSpan timeSpan)
    {
        // Feature not yet implemented - requires suspicious activity tracking
        throw new NotImplementedException("Suspicious activity tracking is not yet implemented. Requires database schema and tracking system implementation.");
    }

    #endregion
}