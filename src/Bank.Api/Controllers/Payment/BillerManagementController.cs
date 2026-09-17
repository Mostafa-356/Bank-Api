using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers.Payment;

/// <summary>
/// Controller for biller management operations (Admin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class BillerManagementController : ControllerBase
{
    private readonly IBillerRepository _billerRepository;
    private readonly IBillerIntegrationService _billerIntegrationService;
    private readonly IBillerHealthCheckRepository _billerHealthCheckRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BillerManagementController> _logger;

    public BillerManagementController(
        IBillerRepository billerRepository,
        IBillerIntegrationService billerIntegrationService,
        IBillerHealthCheckRepository billerHealthCheckRepository,
        IUnitOfWork unitOfWork,
        ILogger<BillerManagementController> logger)
    {
        _billerRepository = billerRepository;
        _billerIntegrationService = billerIntegrationService;
        _billerHealthCheckRepository = billerHealthCheckRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Create a new biller
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BillerDto>> CreateBiller([FromBody] CreateBillerRequest request)
    {
        // Check if biller already exists with same account/routing number
        var exists = await _billerRepository.ExistsAsync(request.AccountNumber, request.RoutingNumber);
        if (exists)
        {
            return BadRequest("A biller with the same account and routing number already exists");
        }

        var biller = new Bank.Domain.Entities.Biller
        {
            Name = request.Name,
            Category = request.Category,
            AccountNumber = request.AccountNumber,
            RoutingNumber = request.RoutingNumber,
            Address = request.Address,
            SupportedPaymentMethods = System.Text.Json.JsonSerializer.Serialize(request.SupportedPaymentMethods),
            MinAmount = request.MinAmount,
            MaxAmount = request.MaxAmount,
            ProcessingDays = request.ProcessingDays,
            IsActive = true
        };

        await _billerRepository.AddAsync(biller);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Biller created: {BillerId} - {BillerName}", biller.Id, Bank.Application.Services.SecureLoggingService.SanitizeInput(biller.Name));

        var billerDto = MapToBillerDto(biller);
        return CreatedAtAction(nameof(GetBillerById), new { billerId = biller.Id }, billerDto);
    }

    /// <summary>
    /// Update an existing biller
    /// </summary>
    [HttpPut("{billerId}")]
    public async Task<ActionResult<BillerDto>> UpdateBiller(Guid billerId, [FromBody] UpdateBillerRequest request)
    {
        var biller = await _billerRepository.GetByIdAsync(billerId);
        if (biller == null)
        {
            return NotFound("Biller not found");
        }

        // Check if another biller exists with same account/routing number (excluding current biller)
        var existingBillers = await _billerRepository.GetAllAsync();
        var duplicateBiller = existingBillers.FirstOrDefault(b => 
            b.Id != billerId && 
            b.AccountNumber == request.AccountNumber && 
            b.RoutingNumber == request.RoutingNumber);

        if (duplicateBiller != null)
        {
            return BadRequest("Another biller with the same account and routing number already exists");
        }

        // Update biller properties
        biller.Name = request.Name;
        biller.Category = request.Category;
        biller.AccountNumber = request.AccountNumber;
        biller.RoutingNumber = request.RoutingNumber;
        biller.Address = request.Address;
        biller.SupportedPaymentMethods = System.Text.Json.JsonSerializer.Serialize(request.SupportedPaymentMethods);
        biller.MinAmount = request.MinAmount;
        biller.MaxAmount = request.MaxAmount;
        biller.ProcessingDays = request.ProcessingDays;
        biller.IsActive = request.IsActive;

        _billerRepository.Update(biller);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Biller updated: {BillerId} - {BillerName}", biller.Id, Bank.Application.Services.SecureLoggingService.SanitizeInput(biller.Name));

        var billerDto = MapToBillerDto(biller);
        return Ok(billerDto);
    }

    /// <summary>
    /// Get biller by ID
    /// </summary>
    [HttpGet("{billerId}")]
    public async Task<ActionResult<BillerDto>> GetBillerById(Guid billerId)
    {
        var biller = await _billerRepository.GetByIdAsync(billerId);
        if (biller == null)
        {
            return NotFound("Biller not found");
        }

        var billerDto = MapToBillerDto(biller);
        return Ok(billerDto);
    }

    /// <summary>
    /// Get all billers (including inactive)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BillerDto>>> GetAllBillers([FromQuery] bool activeOnly = false)
    {
        var billers = activeOnly 
            ? await _billerRepository.GetActiveBillersAsync()
            : (await _billerRepository.GetAllAsync()).ToList();

        var billerDtos = billers.Select(MapToBillerDto).ToList();
        return Ok(billerDtos);
    }

    /// <summary>
    /// Delete a biller (soft delete)
    /// </summary>
    [HttpDelete("{billerId}")]
    public async Task<ActionResult> DeleteBiller(Guid billerId)
    {
        var biller = await _billerRepository.GetByIdAsync(billerId);
        if (biller == null)
        {
            return NotFound("Biller not found");
        }

        // Check if biller has any pending payments
        var billerWithPayments = await _billerRepository.GetBillerWithPaymentsAsync(billerId);
        var hasPendingPayments = billerWithPayments?.BillPayments?.Any(bp => 
            bp.Status == BillPaymentStatus.Pending || bp.Status == BillPaymentStatus.Processing) == true;

        if (hasPendingPayments)
        {
            return BadRequest("Cannot delete biller with pending payments. Please wait for all payments to complete or cancel them first.");
        }

        _billerRepository.Remove(biller);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Biller deleted: {BillerId} - {BillerName}", biller.Id, biller.Name);

        return Ok(new { message = "Biller deleted successfully" });
    }

    /// <summary>
    /// Activate or deactivate a biller
    /// </summary>
    [HttpPost("{billerId}/toggle-status")]
    public async Task<ActionResult> ToggleBillerStatus(Guid billerId)
    {
        var biller = await _billerRepository.GetByIdAsync(billerId);
        if (biller == null)
        {
            return NotFound("Biller not found");
        }

        biller.IsActive = !biller.IsActive;
        _billerRepository.Update(biller);
        await _unitOfWork.SaveChangesAsync();

        var status = biller.IsActive ? "activated" : "deactivated";
        _logger.LogInformation("Biller {Status}: {BillerId} - {BillerName}", status, biller.Id, biller.Name);

        return Ok(new { message = $"Biller {status} successfully", isActive = biller.IsActive });
    }

    /// <summary>
    /// Get biller health check history
    /// </summary>
    [HttpGet("{billerId}/health-history")]
    public async Task<ActionResult<List<BillerHealthCheckDto>>> GetBillerHealthHistory(Guid billerId, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        var healthChecks = await _billerHealthCheckRepository.GetHealthCheckHistoryAsync(billerId, fromDate, toDate);
        var healthCheckDtos = healthChecks.Select(MapToBillerHealthCheckDto).ToList();
        return Ok(healthCheckDtos);
    }

    /// <summary>
    /// Get unhealthy billers
    /// </summary>
    [HttpGet("unhealthy")]
    public async Task<ActionResult<List<BillerHealthCheckDto>>> GetUnhealthyBillers()
    {
        var unhealthyBillers = await _billerHealthCheckRepository.GetUnhealthyBillersAsync();
        var healthCheckDtos = unhealthyBillers.Select(MapToBillerHealthCheckDto).ToList();
        return Ok(healthCheckDtos);
    }

    /// <summary>
    /// Get health check statistics
    /// </summary>
    [HttpGet("health-statistics")]
    public async Task<ActionResult<Dictionary<string, object>>> GetHealthCheckStatistics([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
    {
        var statistics = await _billerHealthCheckRepository.GetHealthCheckStatisticsAsync(fromDate, toDate);
        return Ok(statistics);
    }

    /// <summary>
    /// Perform health check for all billers
    /// </summary>
    [HttpPost("health-check-all")]
    public async Task<ActionResult<List<BillerHealthCheckResponse>>> PerformHealthCheckForAllBillers()
    {
        var activeBillers = await _billerRepository.GetActiveBillersAsync();
        var healthCheckTasks = activeBillers.Select(async biller =>
        {
            try
            {
                return await _billerIntegrationService.CheckBillerHealthAsync(biller.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health for biller {BillerId}", biller.Id);
                return new BillerHealthCheckResponse
                {
                    BillerId = biller.Id,
                    IsHealthy = false,
                    Status = "Error",
                    LastChecked = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
        });

        var healthCheckResults = await Task.WhenAll(healthCheckTasks);
        return Ok(healthCheckResults);
    }

    #region Private Helper Methods

    private static BillerDto MapToBillerDto(Bank.Domain.Entities.Biller biller)
    {
        string[] supportedMethods = Array.Empty<string>();
        
        if (!string.IsNullOrEmpty(biller.SupportedPaymentMethods))
        {
            try
            {
                supportedMethods = System.Text.Json.JsonSerializer.Deserialize<string[]>(biller.SupportedPaymentMethods) ?? Array.Empty<string>();
            }
            catch
            {
                // If deserialization fails, return empty array
            }
        }

        return new BillerDto
        {
            Id = biller.Id,
            Name = biller.Name,
            Category = biller.Category,
            AccountNumber = biller.AccountNumber,
            RoutingNumber = biller.RoutingNumber,
            Address = biller.Address,
            IsActive = biller.IsActive,
            SupportedPaymentMethods = supportedMethods,
            MinAmount = biller.MinAmount,
            MaxAmount = biller.MaxAmount,
            ProcessingDays = biller.ProcessingDays,
            CreatedAt = biller.CreatedAt
        };
    }

    private static BillerHealthCheckDto MapToBillerHealthCheckDto(Bank.Domain.Entities.BillerHealthCheck healthCheck)
    {
        return new BillerHealthCheckDto
        {
            Id = healthCheck.Id,
            BillerId = healthCheck.BillerId,
            BillerName = healthCheck.Biller?.Name ?? string.Empty,
            IsHealthy = healthCheck.IsHealthy,
            CheckDate = healthCheck.CheckDate,
            ResponseTime = healthCheck.ResponseTime,
            Status = healthCheck.Status,
            ErrorMessage = healthCheck.ErrorMessage,
            ConsecutiveFailures = healthCheck.ConsecutiveFailures,
            LastSuccessfulCheck = healthCheck.LastSuccessfulCheck
        };
    }

    #endregion
}

// DTO for biller health check
public class BillerHealthCheckDto
{
    public Guid Id { get; set; }
    public Guid BillerId { get; set; }
    public string BillerName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime CheckDate { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastSuccessfulCheck { get; set; }
}