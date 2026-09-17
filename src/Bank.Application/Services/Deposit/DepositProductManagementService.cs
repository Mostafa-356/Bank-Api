using Bank.Application.DTOs;
using Bank.Application.Helpers.Deposit;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Specialized service for deposit product management (CRUD operations)
/// Handles creation, updating, and deactivation of deposit products
/// </summary>
public sealed class DepositProductManagementService : IDepositProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<DepositProductManagementService> _logger;

    public DepositProductManagementService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ILogger<DepositProductManagementService> logger)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<DepositProductDto?> GetDepositProductAsync(Guid productId)
    {
        var product = await _unitOfWork.Repository<DepositProduct>().GetByIdAsync(productId);
        return product == null ? null : DepositMappingHelper.MapToDepositProductDto(product);
    }

    public async Task<IEnumerable<DepositProductDto>> GetActiveDepositProductsAsync()
    {
        var products = await _unitOfWork.Repository<DepositProduct>()
            .FindAsync(p => p.IsActive);

        return products.Select(DepositMappingHelper.MapToDepositProductDto);
    }

    public async Task<IEnumerable<DepositProductDto>> GetDepositProductsByTypeAsync(DepositProductType productType)
    {
        var products = await _unitOfWork.Repository<DepositProduct>()
            .FindAsync(p => p.IsActive && p.ProductType == productType);

        return products.Select(DepositMappingHelper.MapToDepositProductDto);
    }

    public async Task<DepositProductDto> CreateDepositProductAsync(CreateDepositProductRequest request, Guid createdByUserId)
    {
        var product = new DepositProduct
        {
            Name = request.Name,
            Description = request.Description,
            ProductType = request.ProductType,
            MinimumTermDays = request.MinimumTermDays,
            MaximumTermDays = request.MaximumTermDays,
            DefaultTermDays = request.DefaultTermDays,
            MinimumBalance = request.MinimumBalance,
            MaximumBalance = request.MaximumBalance,
            MinimumOpeningBalance = request.MinimumOpeningBalance,
            BaseInterestRate = request.BaseInterestRate,
            InterestCalculationMethod = request.InterestCalculationMethod,
            CompoundingFrequency = request.CompoundingFrequency,
            HasTieredRates = request.HasTieredRates,
            AllowPartialWithdrawals = request.AllowPartialWithdrawals,
            PenaltyType = request.PenaltyType,
            PenaltyAmount = request.PenaltyAmount,
            PenaltyPercentage = request.PenaltyPercentage,
            PenaltyFreeDays = request.PenaltyFreeDays,
            DefaultMaturityAction = request.DefaultMaturityAction,
            AllowAutoRenewal = request.AllowAutoRenewal,
            AutoRenewalNoticeDays = request.AutoRenewalNoticeDays,
            PromotionalRateStartDate = request.PromotionalRateStartDate,
            PromotionalRateEndDate = request.PromotionalRateEndDate,
            PromotionalRate = request.PromotionalRate
        };

        await _unitOfWork.Repository<DepositProduct>().AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogUserActionAsync(
            createdByUserId,
            "DepositProduct",
            "Create",
            product.Id.ToString(),
            $"Created deposit product: {product.Name}");

        _logger.LogInformation("Created deposit product {ProductId} by user {UserId}", product.Id, createdByUserId);

        return DepositMappingHelper.MapToDepositProductDto(product);
    }

    public async Task<DepositProductDto> UpdateDepositProductAsync(Guid productId, UpdateDepositProductRequest request, Guid updatedByUserId)
    {
        var product = await _unitOfWork.Repository<DepositProduct>().GetByIdAsync(productId);
        if (product == null)
            throw new InvalidOperationException($"Deposit product {productId} not found");

        var originalName = product.Name;

        if (!string.IsNullOrEmpty(request.Name))
            product.Name = request.Name;
        if (!string.IsNullOrEmpty(request.Description))
            product.Description = request.Description;
        if (request.IsActive.HasValue)
            product.IsActive = request.IsActive.Value;
        if (request.BaseInterestRate.HasValue)
            product.BaseInterestRate = request.BaseInterestRate.Value;
        if (request.AllowPartialWithdrawals.HasValue)
            product.AllowPartialWithdrawals = request.AllowPartialWithdrawals.Value;
        if (request.PenaltyType.HasValue)
            product.PenaltyType = request.PenaltyType.Value;
        if (request.PenaltyAmount.HasValue)
            product.PenaltyAmount = request.PenaltyAmount.Value;
        if (request.PenaltyPercentage.HasValue)
            product.PenaltyPercentage = request.PenaltyPercentage.Value;
        if (request.DefaultMaturityAction.HasValue)
            product.DefaultMaturityAction = request.DefaultMaturityAction.Value;
        if (request.AllowAutoRenewal.HasValue)
            product.AllowAutoRenewal = request.AllowAutoRenewal.Value;
        if (request.AutoRenewalNoticeDays.HasValue)
            product.AutoRenewalNoticeDays = request.AutoRenewalNoticeDays.Value;
        if (request.PromotionalRateStartDate.HasValue)
            product.PromotionalRateStartDate = request.PromotionalRateStartDate.Value;
        if (request.PromotionalRateEndDate.HasValue)
            product.PromotionalRateEndDate = request.PromotionalRateEndDate.Value;
        if (request.PromotionalRate.HasValue)
            product.PromotionalRate = request.PromotionalRate.Value;

        _unitOfWork.Repository<DepositProduct>().Update(product);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogUserActionAsync(
            updatedByUserId,
            "DepositProduct",
            "Update",
            product.Id.ToString(),
            $"Updated deposit product: {originalName} -> {product.Name}");

        _logger.LogInformation("Updated deposit product {ProductId} by user {UserId}", productId, updatedByUserId);

        return DepositMappingHelper.MapToDepositProductDto(product);
    }

    public async Task<bool> DeactivateDepositProductAsync(Guid productId, Guid deactivatedByUserId)
    {
        var product = await _unitOfWork.Repository<DepositProduct>().GetByIdAsync(productId);
        if (product == null)
            return false;

        product.IsActive = false;
        _unitOfWork.Repository<DepositProduct>().Update(product);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogUserActionAsync(
            deactivatedByUserId,
            "DepositProduct",
            "Deactivate",
            product.Id.ToString(),
            $"Deactivated deposit product: {product.Name}");

        _logger.LogInformation("Deactivated deposit product {ProductId} by user {UserId}", productId, deactivatedByUserId);

        return true;
    }
}
