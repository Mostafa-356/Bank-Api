using AutoMapper;
using Bank.Application.DTOs;
using Bank.Application.Helpers.Deposit;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for managing interest tiers for deposit products
/// Handles creation, retrieval, update, and deletion of tiered interest rates
/// </summary>
public sealed class InterestTierService : IInterestTierService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<InterestTierService> _logger;
    private readonly IMapper _mapper;

    public InterestTierService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ILogger<InterestTierService> logger,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<InterestTierDto> CreateInterestTierAsync(Guid productId, CreateInterestTierRequest request, Guid createdByUserId)
    {
        var product = await _unitOfWork.Repository<DepositProduct>().GetByIdAsync(productId);
        if (product == null)
            throw new InvalidOperationException($"Deposit product {productId} not found");

        var tier = new InterestTier
        {
            DepositProductId = productId,
            TierName = request.TierName,
            MinimumBalance = request.MinimumBalance,
            MaximumBalance = request.MaximumBalance,
            InterestRate = request.InterestRate,
            TierBasis = request.TierBasis,
            DisplayOrder = request.DisplayOrder,
            EffectiveFromDate = request.EffectiveFromDate,
            EffectiveToDate = request.EffectiveToDate,
            IsPromotional = request.IsPromotional
        };

        await _unitOfWork.Repository<InterestTier>().AddAsync(tier);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogUserActionAsync(
            createdByUserId,
            "InterestTier",
            "Create",
            tier.Id.ToString(),
            $"Created interest tier: {tier.TierName} for product {product.Name}");

        _logger.LogInformation("Created interest tier {TierId} for product {ProductId}", tier.Id, productId);

        return MapToInterestTierDto(tier);
    }

    public async Task<InterestTierDto> UpdateInterestTierAsync(Guid tierId, UpdateInterestTierRequest request, Guid updatedByUserId)
    {
        var tier = await _unitOfWork.Repository<InterestTier>().GetByIdAsync(tierId);
        if (tier == null)
            throw new InvalidOperationException($"Interest tier {tierId} not found");

        if (!string.IsNullOrEmpty(request.TierName))
            tier.TierName = request.TierName;
        if (request.InterestRate.HasValue)
            tier.InterestRate = request.InterestRate.Value;
        if (request.IsActive.HasValue)
            tier.IsActive = request.IsActive.Value;
        if (request.DisplayOrder.HasValue)
            tier.DisplayOrder = request.DisplayOrder.Value;
        if (request.EffectiveFromDate.HasValue)
            tier.EffectiveFromDate = request.EffectiveFromDate.Value;
        if (request.EffectiveToDate.HasValue)
            tier.EffectiveToDate = request.EffectiveToDate.Value;

        _unitOfWork.Repository<InterestTier>().Update(tier);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogUserActionAsync(
            updatedByUserId,
            "InterestTier",
            "Update",
            tier.Id.ToString(),
            $"Updated interest tier: {tier.TierName}");

        _logger.LogInformation("Updated interest tier {TierId}", tierId);

        return MapToInterestTierDto(tier);
    }

    public async Task<bool> DeleteInterestTierAsync(Guid tierId, Guid deletedByUserId)
    {
        var tier = await _unitOfWork.Repository<InterestTier>().GetByIdAsync(tierId);
        if (tier == null)
            return false;

        _unitOfWork.Repository<InterestTier>().Remove(tier);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogUserActionAsync(
            deletedByUserId,
            "InterestTier",
            "Delete",
            tier.Id.ToString(),
            $"Deleted interest tier: {tier.TierName}");

        _logger.LogInformation("Deleted interest tier {TierId}", tierId);

        return true;
    }

    public async Task<IEnumerable<InterestTierDto>> GetInterestTiersAsync(Guid productId)
    {
        var tiers = await _unitOfWork.Repository<InterestTier>()
            .FindAsync(t => t.DepositProductId == productId && t.IsActive);

        return tiers.OrderBy(t => t.DisplayOrder).Select(MapToInterestTierDto);
    }

    private static InterestTierDto MapToInterestTierDto(InterestTier tier)
        => DepositMappingHelper.MapToInterestTierDto(tier);
}
