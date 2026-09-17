using Bank.Application.DTOs;
using Bank.Application.DTOs.Statement.Core;
using Bank.Application.DTOs.Statement.Delivery;
using Bank.Application.DTOs.Statement.Analytics;
using Bank.Application.Helpers.Statement;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Bank.Application.Services;

/// <summary>
/// Service for generating statement files in various formats
/// </summary>
public class StatementGenerator : IStatementGenerator
{
    private readonly ILogger<StatementGenerator> _logger;

    public StatementGenerator(ILogger<StatementGenerator> logger)
    {
        _logger = logger;
    }

    public async Task<(byte[] Content, string FileName)> GeneratePdfStatementAsync(AccountStatement statement, StatementTemplate? template = null)
    {
        try
        {
            // For now, generate a simple HTML-based PDF content
            // In a real implementation, you'd use a PDF library like iTextSharp or PuppeteerSharp
            var htmlContent = await GenerateHtmlContentAsync(statement, template);
            var pdfContent = Encoding.UTF8.GetBytes($"PDF PLACEHOLDER: {htmlContent}");
            
            var fileName = $"statement_{statement.StatementNumber}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            return (pdfContent, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF statement");
            throw;
        }
    }

    public async Task<(byte[] Content, string FileName)> GenerateCsvStatementAsync(AccountStatement statement)
    {
        try
        {
            var csv = new StringBuilder();
            
            // Header
            csv.AppendLine("Date,Description,Reference,Amount,Balance,Type,Category");
            
            // Transactions
            foreach (var transaction in statement.Transactions.OrderBy(t => t.TransactionDate))
            {
                csv.AppendLine($"{transaction.TransactionDate:yyyy-MM-dd}," +
                              $"\"{transaction.Description}\"," +
                              $"\"{transaction.Reference}\"," +
                              $"{transaction.Amount:F2}," +
                              $"{transaction.RunningBalance:F2}," +
                              $"{transaction.Type}," +
                              $"\"{transaction.Category}\"");
            }
            
            var content = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"statement_{statement.StatementNumber}_{DateTime.UtcNow:yyyyMMdd}.csv";
            
            return (content, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CSV statement");
            throw;
        }
    }

    public async Task<(byte[] Content, string FileName)> GenerateExcelStatementAsync(AccountStatement statement)
    {
        try
        {
            // For now, generate CSV format as Excel placeholder
            // In a real implementation, you'd use EPPlus or ClosedXML
            var (csvContent, _) = await GenerateCsvStatementAsync(statement);
            var fileName = $"statement_{statement.StatementNumber}_{DateTime.UtcNow:yyyyMMdd}.xlsx";
            
            return (csvContent, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Excel statement");
            throw;
        }
    }

    public async Task<(byte[] Content, string FileName)> GenerateHtmlStatementAsync(AccountStatement statement, StatementTemplate? template = null)
    {
        try
        {
            var htmlContent = await GenerateHtmlContentAsync(statement, template);
            var content = Encoding.UTF8.GetBytes(htmlContent);
            var fileName = $"statement_{statement.StatementNumber}_{DateTime.UtcNow:yyyyMMdd}.html";
            
            return (content, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating HTML statement");
            throw;
        }
    }

    public async Task<(byte[] Content, string FileName)> GenerateJsonStatementAsync(AccountStatement statement)
    {
        try
        {
            var statementData = new
            {
                StatementInfo = new
                {
                    statement.StatementNumber,
                    statement.StatementDate,
                    statement.PeriodStartDate,
                    statement.PeriodEndDate,
                    AccountNumber = statement.Account?.AccountNumber,
                    AccountHolderName = statement.Account?.AccountHolderName
                },
                Balances = new
                {
                    statement.OpeningBalance,
                    statement.ClosingBalance,
                    statement.AverageBalance,
                    statement.MinimumBalance,
                    statement.MaximumBalance
                },
                Summary = new
                {
                    statement.TotalTransactions,
                    statement.DebitTransactions,
                    statement.CreditTransactions,
                    statement.TotalDebits,
                    statement.TotalCredits,
                    statement.TotalFees,
                    statement.InterestEarned
                },
                Transactions = statement.Transactions.Select(t => new
                {
                    t.TransactionDate,
                    t.Description,
                    t.Reference,
                    t.Amount,
                    t.RunningBalance,
                    Type = t.Type.ToString(),
                    Status = t.Status.ToString(),
                    t.Category
                }).OrderBy(t => t.TransactionDate)
            };
            
            var json = JsonSerializer.Serialize(statementData, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var content = Encoding.UTF8.GetBytes(json);
            var fileName = $"statement_{statement.StatementNumber}_{DateTime.UtcNow:yyyyMMdd}.json";
            
            return (content, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating JSON statement");
            throw;
        }
    }

    public async Task<(byte[] Content, string FileName)> GenerateConsolidatedStatementAsync(
        List<AccountStatement> statements, 
        ConsolidatedStatementRequest request, 
        StatementTemplate? template = null)
    {
        try
        {
            return request.Format switch
            {
                StatementFormat.PDF => await GenerateConsolidatedPdfAsync(statements, request, template),
                StatementFormat.CSV => await GenerateConsolidatedCsvAsync(statements, request),
                StatementFormat.Excel => await GenerateConsolidatedExcelAsync(statements, request),
                StatementFormat.HTML => await GenerateConsolidatedHtmlAsync(statements, request, template),
                StatementFormat.JSON => await GenerateConsolidatedJsonAsync(statements, request),
                _ => throw new NotSupportedException($"Format {request.Format} not supported for consolidated statements")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating consolidated statement");
            throw;
        }
    }

    public string GetContentType(StatementFormat format)
    {
        return format switch
        {
            StatementFormat.PDF => "application/pdf",
            StatementFormat.CSV => "text/csv",
            StatementFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            StatementFormat.HTML => "text/html",
            StatementFormat.JSON => "application/json",
            _ => "application/octet-stream"
        };
    }

    public string GetFileExtension(StatementFormat format)
    {
        return format switch
        {
            StatementFormat.PDF => ".pdf",
            StatementFormat.CSV => ".csv",
            StatementFormat.Excel => ".xlsx",
            StatementFormat.HTML => ".html",
            StatementFormat.JSON => ".json",
            _ => ".bin"
        };
    }

    #region Private Helper Methods

    private async Task<string> GenerateHtmlContentAsync(AccountStatement statement, StatementTemplate? template = null)
    {
        var html = new StringBuilder();
        
        // HTML Header
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>Account Statement</title>");
        html.AppendLine("<style>");
        html.AppendLine(GetDefaultCssStyles());
        if (!string.IsNullOrEmpty(template?.CssStyles))
        {
            html.AppendLine(template.CssStyles);
        }
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // Bank Header
        if (template?.IncludeBankBranding == true)
        {
            html.AppendLine("<div class='bank-header'>");
            html.AppendLine("<h1>SecureBank</h1>");
            html.AppendLine("<p>Your Trusted Financial Partner</p>");
            html.AppendLine("</div>");
        }
        
        // Statement Header
        html.AppendLine("<div class='statement-header'>");
        html.AppendLine($"<h2>Account Statement</h2>");
        html.AppendLine($"<p><strong>Statement Number:</strong> {statement.StatementNumber}</p>");
        html.AppendLine($"<p><strong>Statement Date:</strong> {statement.StatementDate:yyyy-MM-dd}</p>");
        html.AppendLine($"<p><strong>Period:</strong> {statement.PeriodStartDate:yyyy-MM-dd} to {statement.PeriodEndDate:yyyy-MM-dd}</p>");
        html.AppendLine("</div>");
        
        // Account Information
        html.AppendLine("<div class='account-info'>");
        html.AppendLine("<h3>Account Information</h3>");
        html.AppendLine($"<p><strong>Account Number:</strong> {statement.Account?.AccountNumber}</p>");
        html.AppendLine($"<p><strong>Account Holder:</strong> {statement.Account?.AccountHolderName}</p>");
        html.AppendLine($"<p><strong>Account Type:</strong> {statement.Account?.Type}</p>");
        html.AppendLine("</div>");
        
        // Balance Summary
        html.AppendLine("<div class='balance-summary'>");
        html.AppendLine("<h3>Balance Summary</h3>");
        html.AppendLine("<table>");
        html.AppendLine($"<tr><td>Opening Balance:</td><td>${statement.OpeningBalance:N2}</td></tr>");
        html.AppendLine($"<tr><td>Closing Balance:</td><td>${statement.ClosingBalance:N2}</td></tr>");
        html.AppendLine($"<tr><td>Average Balance:</td><td>${statement.AverageBalance:N2}</td></tr>");
        html.AppendLine($"<tr><td>Minimum Balance:</td><td>${statement.MinimumBalance:N2}</td></tr>");
        html.AppendLine($"<tr><td>Maximum Balance:</td><td>${statement.MaximumBalance:N2}</td></tr>");
        html.AppendLine($"<tr><td>Total Credits:</td><td>${statement.TotalCredits:N2}</td></tr>");
        html.AppendLine($"<tr><td>Total Debits:</td><td>${statement.TotalDebits:N2}</td></tr>");
        html.AppendLine($"<tr><td>Total Fees:</td><td>${statement.TotalFees:N2}</td></tr>");
        html.AppendLine($"<tr><td>Interest Earned:</td><td>${statement.InterestEarned:N2}</td></tr>");
        html.AppendLine($"<tr><td>Interest Charged:</td><td>${statement.InterestCharged:N2}</td></tr>");
        html.AppendLine("</table>");
        html.AppendLine("</div>");
        
        // Account Statistics (Requirement 3.8)
        if (statement.Transactions?.Any() == true)
        {
            var monthlyStats = CalculateMonthlyStatistics(statement);
            html.AppendLine("<div class='monthly-statistics'>");
            html.AppendLine("<h3>Monthly Statistics</h3>");
            html.AppendLine("<table>");
            html.AppendLine($"<tr><td>Average Monthly Credits:</td><td>${monthlyStats.AverageMonthlyCredits:N2}</td></tr>");
            html.AppendLine($"<tr><td>Average Monthly Debits:</td><td>${monthlyStats.AverageMonthlyDebits:N2}</td></tr>");
            html.AppendLine($"<tr><td>Average Transaction Amount:</td><td>${monthlyStats.AverageTransactionAmount:N2}</td></tr>");
            html.AppendLine($"<tr><td>Largest Credit:</td><td>${monthlyStats.LargestCredit:N2}</td></tr>");
            html.AppendLine($"<tr><td>Largest Debit:</td><td>${monthlyStats.LargestDebit:N2}</td></tr>");
            html.AppendLine($"<tr><td>Days with Activity:</td><td>{monthlyStats.DaysWithActivity}</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }
        
        // Fee Summary (Requirement 3.9)
        if (statement.TotalFees > 0)
        {
            var feeSummary = CalculateFeeSummary(statement);
            html.AppendLine("<div class='fee-summary'>");
            html.AppendLine("<h3>Fee Summary</h3>");
            html.AppendLine("<table>");
            foreach (var fee in feeSummary)
            {
                html.AppendLine($"<tr><td>{fee.Key}:</td><td>${fee.Value:N2}</td></tr>");
            }
            html.AppendLine($"<tr class='total-row'><td><strong>Total Fees:</strong></td><td><strong>${statement.TotalFees:N2}</strong></td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }
        
        // Transaction Details
        if (statement.Transactions?.Any() == true)
        {
            html.AppendLine("<div class='transactions'>");
            html.AppendLine("<h3>Transaction Details</h3>");
            html.AppendLine("<table class='transaction-table'>");
            html.AppendLine("<thead>");
            html.AppendLine("<tr><th>Date</th><th>Description</th><th>Reference</th><th>Amount</th><th>Balance</th><th>Category</th></tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody>");
            
            foreach (var transaction in statement.Transactions.OrderBy(t => t.TransactionDate))
            {
                var amountClass = transaction.Amount >= 0 ? "credit" : "debit";
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{transaction.TransactionDate:yyyy-MM-dd}</td>");
                html.AppendLine($"<td>{transaction.Description}</td>");
                html.AppendLine($"<td>{transaction.Reference}</td>");
                html.AppendLine($"<td class='{amountClass}'>${transaction.Amount:N2}</td>");
                html.AppendLine($"<td>${transaction.RunningBalance:N2}</td>");
                html.AppendLine($"<td>{transaction.Category}</td>");
                html.AppendLine("</tr>");
            }
            
            html.AppendLine("</tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }
        
        // Regulatory Disclosures
        if (template?.IncludeRegulatoryDisclosures == true)
        {
            html.AppendLine("<div class='disclosures'>");
            html.AppendLine("<h3>Important Disclosures</h3>");
            html.AppendLine("<div class='disclosure-section'>");
            html.AppendLine("<h4>Account Information</h4>");
            html.AppendLine("<p><small>This statement is provided for informational purposes. Please review all transactions carefully and report any discrepancies within 30 days of the statement date.</small></p>");
            html.AppendLine("<p><small>For questions about your account, please contact customer service at 1-800-BANK-123 or visit our website.</small></p>");
            html.AppendLine("</div>");
            
            html.AppendLine("<div class='disclosure-section'>");
            html.AppendLine("<h4>Regulatory Information</h4>");
            html.AppendLine("<p><small>FDIC Insured - Equal Housing Lender - Member FDIC</small></p>");
            html.AppendLine("<p><small>Your deposits are insured up to $250,000 per depositor, per insured bank, for each account ownership category.</small></p>");
            html.AppendLine("<p><small>This institution is an Equal Opportunity Lender and complies with all applicable federal civil rights laws.</small></p>");
            html.AppendLine("</div>");
            
            html.AppendLine("<div class='disclosure-section'>");
            html.AppendLine("<h4>Privacy Notice</h4>");
            html.AppendLine("<p><small>We are committed to protecting your privacy. We do not sell customer information to third parties.</small></p>");
            html.AppendLine("<p><small>For our complete privacy policy, visit our website or request a copy by calling customer service.</small></p>");
            html.AppendLine("</div>");
            
            html.AppendLine("<div class='disclosure-section'>");
            html.AppendLine("<h4>Electronic Statements</h4>");
            html.AppendLine("<p><small>Electronic statements are available through online banking and mobile app.</small></p>");
            html.AppendLine("<p><small>You may opt out of electronic statements at any time by contacting customer service.</small></p>");
            html.AppendLine("</div>");
            
            if (statement.TotalFees > 0)
            {
                html.AppendLine("<div class='disclosure-section'>");
                html.AppendLine("<h4>Fee Information</h4>");
                html.AppendLine("<p><small>Fees charged during this statement period are detailed in the Fee Summary section above.</small></p>");
                html.AppendLine("<p><small>For a complete fee schedule, please refer to your account agreement or visit our website.</small></p>");
                html.AppendLine("</div>");
            }
            
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }

    private string GetDefaultCssStyles()
    {
        return StatementHtmlHelper.GetDefaultCssStyles();
    }

    private async Task<(byte[] Content, string FileName)> GenerateConsolidatedPdfAsync(
        List<AccountStatement> statements, 
        ConsolidatedStatementRequest request, 
        StatementTemplate? template)
    {
        var htmlContent = await GenerateConsolidatedHtmlContentAsync(statements, request, template);
        var pdfContent = Encoding.UTF8.GetBytes($"PDF PLACEHOLDER: {htmlContent}");
        var fileName = $"consolidated_statement_{DateTime.UtcNow:yyyyMMdd}.pdf";
        
        return (pdfContent, fileName);
    }

    private async Task<(byte[] Content, string FileName)> GenerateConsolidatedCsvAsync(
        List<AccountStatement> statements, 
        ConsolidatedStatementRequest request)
    {
        var csv = new StringBuilder();
        
        // Header
        csv.AppendLine("Account,Date,Description,Reference,Amount,Balance,Type,Category");
        
        // Transactions from all statements
        foreach (var statement in statements)
        {
            foreach (var transaction in statement.Transactions.OrderBy(t => t.TransactionDate))
            {
                csv.AppendLine($"{statement.Account?.AccountNumber}," +
                              $"{transaction.TransactionDate:yyyy-MM-dd}," +
                              $"\"{transaction.Description}\"," +
                              $"\"{transaction.Reference}\"," +
                              $"{transaction.Amount:F2}," +
                              $"{transaction.RunningBalance:F2}," +
                              $"{transaction.Type}," +
                              $"\"{transaction.Category}\"");
            }
        }
        
        var content = Encoding.UTF8.GetBytes(csv.ToString());
        var fileName = $"consolidated_statement_{DateTime.UtcNow:yyyyMMdd}.csv";
        
        return (content, fileName);
    }

    private async Task<(byte[] Content, string FileName)> GenerateConsolidatedExcelAsync(
        List<AccountStatement> statements, 
        ConsolidatedStatementRequest request)
    {
        // For now, use CSV format as Excel placeholder
        return await GenerateConsolidatedCsvAsync(statements, request);
    }

    private async Task<(byte[] Content, string FileName)> GenerateConsolidatedHtmlAsync(
        List<AccountStatement> statements, 
        ConsolidatedStatementRequest request, 
        StatementTemplate? template)
    {
        var htmlContent = await GenerateConsolidatedHtmlContentAsync(statements, request, template);
        var content = Encoding.UTF8.GetBytes(htmlContent);
        var fileName = $"consolidated_statement_{DateTime.UtcNow:yyyyMMdd}.html";
        
        return (content, fileName);
    }

    private async Task<(byte[] Content, string FileName)> GenerateConsolidatedJsonAsync(
        List<AccountStatement> statements, 
        ConsolidatedStatementRequest request)
    {
        var consolidatedData = new
        {
            ConsolidatedStatement = new
            {
                GeneratedDate = DateTime.UtcNow,
                PeriodStart = request.StartDate,
                PeriodEnd = request.EndDate,
                AccountCount = statements.Count
            },
            Accounts = statements.Select(s => new
            {
                AccountNumber = s.Account?.AccountNumber,
                AccountHolderName = s.Account?.AccountHolderName,
                StatementNumber = s.StatementNumber,
                OpeningBalance = s.OpeningBalance,
                ClosingBalance = s.ClosingBalance,
                TotalTransactions = s.TotalTransactions,
                TotalCredits = s.TotalCredits,
                TotalDebits = s.TotalDebits,
                Transactions = s.Transactions.Select(t => new
                {
                    t.TransactionDate,
                    t.Description,
                    t.Reference,
                    t.Amount,
                    t.RunningBalance,
                    Type = t.Type.ToString(),
                    t.Category
                }).OrderBy(t => t.TransactionDate)
            })
        };
        
        var json = JsonSerializer.Serialize(consolidatedData, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        var content = Encoding.UTF8.GetBytes(json);
        var fileName = $"consolidated_statement_{DateTime.UtcNow:yyyyMMdd}.json";
        
        return (content, fileName);
    }

    private async Task<string> GenerateConsolidatedHtmlContentAsync(
        List<AccountStatement> statements, 
        ConsolidatedStatementRequest request, 
        StatementTemplate? template)
    {
        var html = new StringBuilder();
        
        // HTML Header
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>Consolidated Account Statement</title>");
        html.AppendLine("<style>");
        html.AppendLine(GetDefaultCssStyles());
        if (!string.IsNullOrEmpty(template?.CssStyles))
        {
            html.AppendLine(template.CssStyles);
        }
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // Bank Header
        if (template?.IncludeBankBranding == true)
        {
            html.AppendLine("<div class='bank-header'>");
            html.AppendLine("<h1>SecureBank</h1>");
            html.AppendLine("<p>Your Trusted Financial Partner</p>");
            html.AppendLine("</div>");
        }
        
        // Consolidated Statement Header
        html.AppendLine("<div class='statement-header'>");
        html.AppendLine($"<h2>Consolidated Account Statement</h2>");
        html.AppendLine($"<p><strong>Generated Date:</strong> {DateTime.UtcNow:yyyy-MM-dd}</p>");
        html.AppendLine($"<p><strong>Period:</strong> {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}</p>");
        html.AppendLine($"<p><strong>Number of Accounts:</strong> {statements.Count}</p>");
        html.AppendLine("</div>");
        
        // Account Summaries
        if (request.IncludeAccountSummaries)
        {
            html.AppendLine("<div class='account-summaries'>");
            html.AppendLine("<h3>Account Summaries</h3>");
            
            foreach (var statement in statements)
            {
                html.AppendLine("<div class='account-summary'>");
                html.AppendLine($"<h4>Account: {statement.Account?.AccountNumber}</h4>");
                html.AppendLine($"<p><strong>Account Holder:</strong> {statement.Account?.AccountHolderName}</p>");
                html.AppendLine($"<p><strong>Opening Balance:</strong> ${statement.OpeningBalance:N2}</p>");
                html.AppendLine($"<p><strong>Closing Balance:</strong> ${statement.ClosingBalance:N2}</p>");
                html.AppendLine($"<p><strong>Total Transactions:</strong> {statement.TotalTransactions}</p>");
                html.AppendLine("</div>");
            }
            
            html.AppendLine("</div>");
        }
        
        // Consolidated Summary
        if (request.IncludeConsolidatedSummary)
        {
            var totalOpeningBalance = statements.Sum(s => s.OpeningBalance);
            var totalClosingBalance = statements.Sum(s => s.ClosingBalance);
            var totalTransactions = statements.Sum(s => s.TotalTransactions);
            var totalCredits = statements.Sum(s => s.TotalCredits);
            var totalDebits = statements.Sum(s => s.TotalDebits);
            
            html.AppendLine("<div class='consolidated-summary'>");
            html.AppendLine("<h3>Consolidated Summary</h3>");
            html.AppendLine("<table>");
            html.AppendLine($"<tr><td>Total Opening Balance:</td><td>${totalOpeningBalance:N2}</td></tr>");
            html.AppendLine($"<tr><td>Total Closing Balance:</td><td>${totalClosingBalance:N2}</td></tr>");
            html.AppendLine($"<tr><td>Net Change:</td><td>${totalClosingBalance - totalOpeningBalance:N2}</td></tr>");
            html.AppendLine($"<tr><td>Total Transactions:</td><td>{totalTransactions}</td></tr>");
            html.AppendLine($"<tr><td>Total Credits:</td><td>${totalCredits:N2}</td></tr>");
            html.AppendLine($"<tr><td>Total Debits:</td><td>${totalDebits:N2}</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }
        
        // Transaction Details
        if (request.IncludeTransactionDetails)
        {
            html.AppendLine("<div class='transactions'>");
            html.AppendLine("<h3>All Transactions</h3>");
            html.AppendLine("<table class='transaction-table'>");
            html.AppendLine("<thead>");
            html.AppendLine("<tr><th>Account</th><th>Date</th><th>Description</th><th>Reference</th><th>Amount</th><th>Category</th></tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody>");
            
            var allTransactions = statements
                .SelectMany(s => s.Transactions.Select(t => new { Statement = s, Transaction = t }))
                .OrderBy(x => x.Transaction.TransactionDate);
            
            foreach (var item in allTransactions)
            {
                var amountClass = item.Transaction.Amount >= 0 ? "credit" : "debit";
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{item.Statement.Account?.AccountNumber}</td>");
                html.AppendLine($"<td>{item.Transaction.TransactionDate:yyyy-MM-dd}</td>");
                html.AppendLine($"<td>{item.Transaction.Description}</td>");
                html.AppendLine($"<td>{item.Transaction.Reference}</td>");
                html.AppendLine($"<td class='{amountClass}'>${item.Transaction.Amount:N2}</td>");
                html.AppendLine($"<td>{item.Transaction.Category}</td>");
                html.AppendLine("</tr>");
            }
            
            html.AppendLine("</tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }
        
        // Regulatory Disclosures
        if (template?.IncludeRegulatoryDisclosures == true)
        {
            html.AppendLine("<div class='disclosures'>");
            html.AppendLine("<h3>Important Disclosures</h3>");
            html.AppendLine("<p><small>This consolidated statement is provided for informational purposes. Please review all transactions carefully and report any discrepancies within 30 days.</small></p>");
            html.AppendLine("<p><small>FDIC Insured - Equal Housing Lender - Member FDIC</small></p>");
            html.AppendLine("</div>");
        }
        
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }

    #endregion

    #region Advanced Statistics Helper Methods

    private MonthlyStatistics CalculateMonthlyStatistics(AccountStatement statement)
    {
        if (statement.Transactions?.Any() != true)
        {
            return new MonthlyStatistics();
        }

        var transactions = statement.Transactions.ToList();
        var periodMonths = ((statement.PeriodEndDate.Year - statement.PeriodStartDate.Year) * 12) + 
                          statement.PeriodEndDate.Month - statement.PeriodStartDate.Month + 1;

        var credits = transactions.Where(t => t.Amount > 0).ToList();
        var debits = transactions.Where(t => t.Amount < 0).ToList();

        return new MonthlyStatistics
        {
            AverageMonthlyCredits = credits.Any() ? credits.Sum(t => t.Amount) / Math.Max(1, periodMonths) : 0,
            AverageMonthlyDebits = debits.Any() ? Math.Abs(debits.Sum(t => t.Amount)) / Math.Max(1, periodMonths) : 0,
            AverageTransactionAmount = transactions.Any() ? transactions.Average(t => Math.Abs(t.Amount)) : 0,
            LargestCredit = credits.Any() ? credits.Max(t => t.Amount) : 0,
            LargestDebit = debits.Any() ? Math.Abs(debits.Min(t => t.Amount)) : 0,
            DaysWithActivity = transactions.Select(t => t.TransactionDate.Date).Distinct().Count()
        };
    }

    private Dictionary<string, decimal> CalculateFeeSummary(AccountStatement statement)
    {
        var feeSummary = new Dictionary<string, decimal>();

        if (statement.Transactions?.Any() != true)
        {
            return feeSummary;
        }

        // Group fees by category based on transaction description
        var feeTransactions = statement.Transactions
            .Where(t => t.Amount < 0 && (t.Description.ToLower().Contains("fee") || 
                                       t.Description.ToLower().Contains("charge") ||
                                       t.Description.ToLower().Contains("penalty")))
            .ToList();

        foreach (var transaction in feeTransactions)
        {
            var feeType = DetermineFeeType(transaction.Description);
            if (feeSummary.ContainsKey(feeType))
            {
                feeSummary[feeType] += Math.Abs(transaction.Amount);
            }
            else
            {
                feeSummary[feeType] = Math.Abs(transaction.Amount);
            }
        }

        return feeSummary;
    }

    private string DetermineFeeType(string description)
    {
        return StatementHtmlHelper.DetermineFeeType(description);
    }

    #endregion

    #region Helper Classes

    private class MonthlyStatistics
    {
        public decimal AverageMonthlyCredits { get; set; }
        public decimal AverageMonthlyDebits { get; set; }
        public decimal AverageTransactionAmount { get; set; }
        public decimal LargestCredit { get; set; }
        public decimal LargestDebit { get; set; }
        public int DaysWithActivity { get; set; }
    }

    #endregion
}
