using Bank.Api.Helpers;
using Bank.Application.Constants;
using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Application.Interfaces.Authorization;
using Bank.Application.Services;
using Bank.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Bank.Api.Controllers.Transaction;

/// <summary>
/// Controller for managing deposit products and fixed deposits
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepositController : ControllerBase
{
    private readonly IDepositService _depositService;
    private readonly IDepositCertificateGenerator _certificateGenerator;
    private readonly IAuthorizationHelper _authorizationHelper;

    public DepositController(
        IDepositService depositService, 
        IDepositCertificateGenerator certificateGenerator,
        IAuthorizationHelper authorizationHelper)
    {
        _depositService = depositService;
        _certificateGenerator = certificateGenerator;
        _authorizationHelper = authorizationHelper;
    }

    #region Helper Methods

    /// <summary>
    /// Verifies deposit ownership using the centralized authorization helper
    /// </summary>
    private async Task<(bool IsAuthorized, string? ErrorMessage)> VerifyDepositOwnershipAsync(Guid depositId)
    {
        var userId = this.GetCurrentUserId();
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        return await _authorizationHelper.VerifyResourceOwnershipAsync(
            depositId,
            userId,
            d => _depositService.GetFixedDepositAsync(d),
            d => d.CustomerId,
            userRole,
            "Deposit");
    }

    /// <summary>
    /// Gets the current user ID and role
    /// </summary>
    private (Guid UserId, string? UserRole) GetCurrentUserContext()
    {
        var userId = this.GetCurrentUserId();
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        return (userId, userRole);
    }

    #endregion

    #region Deposit Products

    /// <summary>
    /// Get all active deposit products
    /// </summary>
    [HttpGet("products")]
    public async Task<ActionResult<IEnumerable<DepositProductDto>>> GetDepositProducts()
    {
        var products = await _depositService.GetActiveDepositProductsAsync();
        return Ok(products);
    }

    /// <summary>
    /// Get deposit products by type
    /// </summary>
    [HttpGet("products/type/{productType}")]
    public async Task<ActionResult<IEnumerable<DepositProductDto>>> GetDepositProductsByType(DepositProductType productType)
    {
        var products = await _depositService.GetDepositProductsByTypeAsync(productType);
        return Ok(products);
    }

    /// <summary>
    /// Get deposit product by ID
    /// </summary>
    [HttpGet("products/{productId}")]
    public async Task<ActionResult<DepositProductDto>> GetDepositProduct(Guid productId)
    {
        var product = await _depositService.GetDepositProductAsync(productId);
        if (product == null)
            return this.CreateNotFoundResponse(DomainConstants.DEPOSIT_PRODUCT_NOT_FOUND);

        return Ok(product);
    }

    /// <summary>
    /// Create a new deposit product (Admin only)
    /// </summary>
    [HttpPost("products")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DepositProductDto>> CreateDepositProduct([FromBody] CreateDepositProductRequest request)
    {
        var userId = this.GetCurrentUserId();
        var product = await _depositService.CreateDepositProductAsync(request, userId);
        return CreatedAtAction(nameof(GetDepositProduct), new { productId = product.Id }, product);
    }

    /// <summary>
    /// Update deposit product (Admin only)
    /// </summary>
    [HttpPut("products/{productId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DepositProductDto>> UpdateDepositProduct(Guid productId, [FromBody] UpdateDepositProductRequest request)
    {
        var userId = this.GetCurrentUserId();
        var product = await _depositService.UpdateDepositProductAsync(productId, request, userId);
        return Ok(product);
    }

    /// <summary>
    /// Deactivate deposit product (Admin only)
    /// </summary>
    [HttpDelete("products/{productId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeactivateDepositProduct(Guid productId)
    {
        var userId = this.GetCurrentUserId();
        var success = await _depositService.DeactivateDepositProductAsync(productId, userId);
        if (!success)
            return this.CreateNotFoundResponse(DomainConstants.DEPOSIT_PRODUCT_NOT_FOUND);

        return NoContent();
    }

    #endregion

    #region Interest Tiers

    /// <summary>
    /// Get interest tiers for a deposit product
    /// </summary>
    [HttpGet("products/{productId}/tiers")]
    public async Task<ActionResult<IEnumerable<InterestTierDto>>> GetInterestTiers(Guid productId)
    {
        var tiers = await _depositService.GetInterestTiersAsync(productId);
        return Ok(tiers);
    }

    /// <summary>
    /// Create interest tier (Admin only)
    /// </summary>
    [HttpPost("products/{productId}/tiers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InterestTierDto>> CreateInterestTier(Guid productId, [FromBody] CreateInterestTierRequest request)
    {
        var userId = this.GetCurrentUserId();
        var tier = await _depositService.CreateInterestTierAsync(productId, request, userId);
        return CreatedAtAction(nameof(GetInterestTiers), new { productId }, tier);
    }

    /// <summary>
    /// Update interest tier (Admin only)
    /// </summary>
    [HttpPut("tiers/{tierId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InterestTierDto>> UpdateInterestTier(Guid tierId, [FromBody] UpdateInterestTierRequest request)
    {
        var userId = this.GetCurrentUserId();
        var tier = await _depositService.UpdateInterestTierAsync(tierId, request, userId);
        return Ok(tier);
    }

    /// <summary>
    /// Delete interest tier (Admin only)
    /// </summary>
    [HttpDelete("tiers/{tierId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteInterestTier(Guid tierId)
    {
        var userId = this.GetCurrentUserId();
        var success = await _depositService.DeleteInterestTierAsync(tierId, userId);
        if (!success)
            return this.CreateNotFoundResponse(DomainConstants.INTEREST_TIER_NOT_FOUND);

        return NoContent();
    }

    #endregion
    #region Fixed Deposits

    /// <summary>
    /// Create a new fixed deposit
    /// </summary>
    [HttpPost("fixed-deposits")]
    public async Task<ActionResult<FixedDepositDto>> CreateFixedDeposit([FromBody] CreateFixedDepositRequest request)
    {
        var customerId = this.GetCurrentUserId();
        var deposit = await _depositService.CreateFixedDepositAsync(request, customerId);
        return CreatedAtAction(nameof(GetFixedDeposit), new { depositId = deposit.Id }, deposit);
    }

    /// <summary>
    /// Get fixed deposit by ID
    /// </summary>
    [HttpGet("fixed-deposits/{depositId}")]
    public async Task<ActionResult<FixedDepositDto>> GetFixedDeposit(Guid depositId)
    {
        var deposit = await _depositService.GetFixedDepositAsync(depositId);
        if (deposit == null)
            return this.CreateNotFoundResponse(DomainConstants.DEPOSIT_NOT_FOUND);

        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        return Ok(deposit);
    }

    /// <summary>
    /// Get fixed deposit by deposit number
    /// </summary>
    [HttpGet("fixed-deposits/number/{depositNumber}")]
    public async Task<ActionResult<FixedDepositDto>> GetFixedDepositByNumber(string depositNumber)
    {
        var deposit = await _depositService.GetFixedDepositByNumberAsync(depositNumber);
        if (deposit == null)
            return this.CreateNotFoundResponse(DomainConstants.DEPOSIT_NOT_FOUND);

        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(deposit.Id);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        return Ok(deposit);
    }

    /// <summary>
    /// Get customer's fixed deposits
    /// </summary>
    [HttpGet("fixed-deposits")]
    public async Task<ActionResult<IEnumerable<FixedDepositDto>>> GetCustomerFixedDeposits()
    {
        var customerId = this.GetCurrentUserId();
        var deposits = await _depositService.GetCustomerFixedDepositsAsync(customerId);
        return Ok(deposits);
    }

    /// <summary>
    /// Get maturing deposits (Admin only)
    /// </summary>
    [HttpGet("fixed-deposits/maturing")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<FixedDepositDto>>> GetMaturingDeposits([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var from = fromDate ?? DateTime.UtcNow;
        var to = toDate ?? DateTime.UtcNow.AddDays(30);
        
        var deposits = await _depositService.GetMaturingDepositsAsync(from, to);
        return Ok(deposits);
    }

    #endregion

    #region Maturity and Renewal

    /// <summary>
    /// Get maturity details for a fixed deposit
    /// </summary>
    [HttpGet("fixed-deposits/{depositId}/maturity")]
    public async Task<ActionResult<MaturityDetailsDto>> GetMaturityDetails(Guid depositId)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var details = await _depositService.GetMaturityDetailsAsync(depositId);
        return Ok(details);
    }

    /// <summary>
    /// Process maturity action for a fixed deposit
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/maturity")]
    public async Task<ActionResult> ProcessMaturity(Guid depositId, [FromBody] MaturityAction action)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var (userId, _) = GetCurrentUserContext();
        var success = await _depositService.ProcessMaturityAsync(depositId, action, userId);
        if (!success)
            return this.CreateErrorResponse(DomainConstants.UNABLE_TO_PROCESS_MATURITY, 400);

        return Ok(new { message = "Maturity action processed successfully" });
    }

    /// <summary>
    /// Renew a fixed deposit
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/renew")]
    public async Task<ActionResult<FixedDepositDto>> RenewFixedDeposit(Guid depositId, [FromBody] RenewDepositRequest request)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var (userId, _) = GetCurrentUserContext();
        var renewedDeposit = await _depositService.RenewFixedDepositAsync(depositId, request, userId);
        return Ok(renewedDeposit);
    }

    #endregion

    #region Withdrawals

    /// <summary>
    /// Calculate early withdrawal details
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/withdrawal/calculate")]
    public async Task<ActionResult<WithdrawalDetailsDto>> CalculateEarlyWithdrawal(Guid depositId, [FromBody] decimal withdrawalAmount)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var details = await _depositService.CalculateEarlyWithdrawalAsync(depositId, withdrawalAmount);
        return Ok(details);
    }

    /// <summary>
    /// Process early withdrawal
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/withdrawal/early")]
    public async Task<ActionResult> ProcessEarlyWithdrawal(Guid depositId, [FromBody] EarlyWithdrawalRequest request)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var (userId, _) = GetCurrentUserContext();
        var success = await _depositService.ProcessEarlyWithdrawalAsync(depositId, request, userId);
        if (!success)
            return this.CreateErrorResponse(DomainConstants.UNABLE_TO_PROCESS_EARLY_WITHDRAWAL, 400);

        return Ok(new { message = "Early withdrawal processed successfully" });
    }

    /// <summary>
    /// Process partial withdrawal
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/withdrawal/partial")]
    public async Task<ActionResult> ProcessPartialWithdrawal(Guid depositId, [FromBody] PartialWithdrawalRequest request)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var (userId, _) = GetCurrentUserContext();
        var success = await _depositService.ProcessPartialWithdrawalAsync(depositId, request, userId);
        if (!success)
            return this.CreateErrorResponse(DomainConstants.UNABLE_TO_PROCESS_EARLY_WITHDRAWAL, 400);

        return Ok(new { message = "Partial withdrawal processed successfully" });
    }

    #endregion
    #region Certificates and Notices

    /// <summary>
    /// Generate deposit certificate
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/certificate")]
    public async Task<ActionResult<DepositCertificateDto>> GenerateCertificate(Guid depositId)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var (userId, _) = GetCurrentUserContext();
        var certificate = await _certificateGenerator.GenerateCertificateAsync(depositId, userId);
        return Ok(certificate);
    }

    /// <summary>
    /// Get certificate by ID
    /// </summary>
    [HttpGet("certificates/{certificateId}")]
    public async Task<ActionResult<DepositCertificateDto>> GetCertificate(Guid certificateId)
    {
        var certificate = await _certificateGenerator.GetCertificateAsync(certificateId);
        if (certificate == null)
            return this.CreateNotFoundResponse(DomainConstants.DEPOSIT_NOT_FOUND);

        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(certificate.FixedDepositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        return Ok(certificate);
    }

    /// <summary>
    /// Download certificate PDF
    /// </summary>
    [HttpGet("certificates/{certificateId}/pdf")]
    public async Task<ActionResult> GetCertificatePdf(Guid certificateId)
    {
        var certificate = await _certificateGenerator.GetCertificateAsync(certificateId);
        if (certificate == null)
            return this.CreateNotFoundResponse(DomainConstants.DEPOSIT_NOT_FOUND);

        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(certificate.FixedDepositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var pdfBytes = await _certificateGenerator.GetCertificatePdfAsync(certificateId);
        return File(pdfBytes, "application/pdf", $"certificate_{certificate.CertificateNumber}.pdf");
    }

    #endregion

    #region Reporting and Analytics

    /// <summary>
    /// Get deposit summary for customer
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<DepositSummaryDto>> GetDepositSummary()
    {
        var (customerId, _) = GetCurrentUserContext();
        var summary = await _depositService.GetDepositSummaryAsync(customerId);
        return Ok(summary);
    }

    /// <summary>
    /// Get deposit transactions
    /// </summary>
    [HttpGet("fixed-deposits/{depositId}/transactions")]
    public async Task<ActionResult<IEnumerable<DepositTransactionDto>>> GetDepositTransactions(
        Guid depositId, 
        [FromQuery] DateTime? fromDate, 
        [FromQuery] DateTime? toDate)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var transactions = await _depositService.GetDepositTransactionsAsync(depositId, fromDate, toDate);
        return Ok(transactions);
    }

    /// <summary>
    /// Get customer deposit portfolio
    /// </summary>
    [HttpGet("portfolio")]
    public async Task<ActionResult<DepositPortfolioDto>> GetDepositPortfolio()
    {
        var (customerId, _) = GetCurrentUserContext();
        var portfolio = await _depositService.GetCustomerDepositPortfolioAsync(customerId);
        return Ok(portfolio);
    }

    #endregion

    #region Admin Operations

    /// <summary>
    /// Process interest for a specific deposit (Admin only)
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/interest")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ProcessInterest(Guid depositId)
    {
        var userId = this.GetCurrentUserId();
        var success = await _depositService.ProcessInterestCreditAsync(depositId, userId);
        if (!success)
            return this.CreateErrorResponse(DomainConstants.UNABLE_TO_PROCESS_INTEREST, 400);

        return Ok(new { message = "Interest processed successfully" });
    }

    /// <summary>
    /// Process daily interest for all deposits (Admin only)
    /// </summary>
    [HttpPost("interest/daily")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ProcessDailyInterest()
    {
        var success = await _depositService.ProcessDailyInterestAsync();
        return Ok(new { message = "Daily interest processing completed", success });
    }

    /// <summary>
    /// Process auto-renewals (Admin only)
    /// </summary>
    [HttpPost("auto-renewals")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ProcessAutoRenewals()
    {
        var success = await _depositService.ProcessAutoRenewalsAsync();
        return Ok(new { message = "Auto-renewals processing completed", success });
    }

    /// <summary>
    /// Send maturity notices (Admin only)
    /// </summary>
    [HttpPost("maturity-notices")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SendMaturityNotices()
    {
        var success = await _depositService.SendMaturityNoticesAsync();
        return Ok(new { message = "Maturity notices processing completed", success });
    }

    #endregion

    #region Enhanced Withdrawal Management

    /// <summary>
    /// Get detailed withdrawal calculation with penalty breakdown
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/withdrawal/detailed-calculation")]
    public async Task<ActionResult<DetailedWithdrawalCalculation>> GetDetailedWithdrawalCalculation(Guid depositId, [FromBody] decimal withdrawalAmount)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var withdrawalService = HttpContext.RequestServices.GetRequiredService<IDepositWithdrawalService>();
        var calculation = await withdrawalService.CalculateDetailedWithdrawalAsync(depositId, withdrawalAmount);
        return Ok(calculation);
    }

    /// <summary>
    /// Get penalty-free periods for a deposit
    /// </summary>
    [HttpGet("fixed-deposits/{depositId}/penalty-free-periods")]
    public async Task<ActionResult<PenaltyFreePeriodsDto>> GetPenaltyFreePeriods(Guid depositId)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var withdrawalService = HttpContext.RequestServices.GetRequiredService<IDepositWithdrawalService>();
        var periods = await withdrawalService.GetPenaltyFreePeriodsAsync(depositId);
        return Ok(periods);
    }

    /// <summary>
    /// Get withdrawal history for a deposit
    /// </summary>
    [HttpGet("fixed-deposits/{depositId}/withdrawal-history")]
    public async Task<ActionResult<IEnumerable<WithdrawalHistoryDto>>> GetWithdrawalHistory(Guid depositId)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var withdrawalService = HttpContext.RequestServices.GetRequiredService<IDepositWithdrawalService>();
        var history = await withdrawalService.GetWithdrawalHistoryAsync(depositId);
        return Ok(history);
    }

    #endregion

    #region Enhanced Maturity Management

    /// <summary>
    /// Process customer consent for auto-renewal
    /// </summary>
    [HttpPost("fixed-deposits/{depositId}/consent")]
    public async Task<ActionResult> ProcessCustomerConsent(Guid depositId, [FromBody] CustomerConsentRequest request)
    {
        var (isAuthorized, errorMessage) = await VerifyDepositOwnershipAsync(depositId);
        if (!isAuthorized)
            return this.CreateForbiddenResponse(errorMessage ?? "Forbidden");

        var maturityService = HttpContext.RequestServices.GetRequiredService<IDepositMaturityService>();
        var success = await maturityService.ProcessCustomerConsentAsync(depositId, request.ConsentGiven, request.PreferredAction);
        
        if (!success)
            return this.CreateErrorResponse(DomainConstants.OPERATION_FAILED, 400);

        return Ok(new { message = "Customer consent processed successfully" });
    }

    /// <summary>
    /// Get maturity reminders for approaching deposits (Admin only)
    /// </summary>
    [HttpGet("maturity-reminders")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<MaturityReminderDto>>> GetMaturityReminders([FromQuery] int? daysAhead = 30)
    {
        var fromDate = DateTime.UtcNow;
        var toDate = DateTime.UtcNow.AddDays(daysAhead ?? 30);
        
        var maturingDeposits = await _depositService.GetMaturingDepositsAsync(fromDate, toDate);
        
        var reminders = maturingDeposits.Select(d => new MaturityReminderDto
        {
            DepositId = d.Id,
            DepositNumber = d.DepositNumber,
            CustomerName = d.CustomerName,
            MaturityDate = d.MaturityDate,
            DaysToMaturity = d.DaysToMaturity,
            MaturityAmount = d.MaturityAmount,
            AutoRenewalEnabled = d.AutoRenewalEnabled,
            CustomerConsentReceived = d.CustomerConsentReceived,
            DefaultAction = d.MaturityAction
        });

        return Ok(reminders);
    }

    /// <summary>
    /// Process all approaching maturity deposits (Admin only)
    /// </summary>
    [HttpPost("maturity/process-approaching")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MaturityProcessingResult>> ProcessApproachingMaturity()
    {
        var maturityService = HttpContext.RequestServices.GetRequiredService<IDepositMaturityService>();
        var result = await maturityService.ProcessApproachingMaturityAsync();
        return Ok(result);
    }

    /// <summary>
    /// Process all matured deposits (Admin only)
    /// </summary>
    [HttpPost("maturity/process-matured")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MaturityProcessingResult>> ProcessMaturedDeposits()
    {
        var maturityService = HttpContext.RequestServices.GetRequiredService<IDepositMaturityService>();
        var result = await maturityService.ProcessMaturedDepositsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Send renewal reminders (Admin only)
    /// </summary>
    [HttpPost("maturity/send-reminders")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SendRenewalReminders()
    {
        var maturityService = HttpContext.RequestServices.GetRequiredService<IDepositMaturityService>();
        var sentCount = await maturityService.SendRenewalRemindersAsync();
        return Ok(new { message = $"Sent {sentCount} renewal reminders" });
    }

    /// <summary>
    /// Get auto-renewal summary (Admin only)
    /// </summary>
    [HttpGet("auto-renewal-summary")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AutoRenewalSummaryDto>> GetAutoRenewalSummary()
    {
        var maturingDeposits = await _depositService.GetMaturingDepositsAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
        
        var summary = new AutoRenewalSummaryDto
        {
            TotalEligible = maturingDeposits.Count(d => d.AutoRenewalEnabled),
            PendingConsent = maturingDeposits.Count(d => d.AutoRenewalEnabled && !d.CustomerConsentReceived),
            TotalRenewedAmount = maturingDeposits.Where(d => d.AutoRenewalEnabled && d.CustomerConsentReceived).Sum(d => d.MaturityAmount)
        };

        return Ok(summary);
    }

    #endregion
}
