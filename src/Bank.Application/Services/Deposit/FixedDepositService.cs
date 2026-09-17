using AutoMapper;
using Bank.Application.DTOs;
using Bank.Application.Helpers.Deposit;
using Bank.Application.Helpers.Shared;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for core fixed deposit operations
/// Handles creation, retrieval, and queries of fixed deposit accounts
/// </summary>
public sealed class FixedDepositService : IFixedDepositService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<FixedDepositService> _logger;
    private readonly IMapper _mapper;

    public FixedDepositService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ILogger<FixedDepositService> logger,
        IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<FixedDepositDto> CreateFixedDepositAsync(CreateFixedDepositRequest request, Guid customerId)
    {
        var product = await _unitOfWork.Repository<DepositProduct>()
            .GetByIdAsync(request.DepositProductId);
        
        if (product == null || !product.IsActive)
            throw new InvalidOperationException($"Deposit product {request.DepositProductId} not found or inactive");

        var linkedAccount = await _unitOfWork.Repository<Account>().GetByIdAsync(request.LinkedAccountId);
        if (linkedAccount == null || linkedAccount.CustomerId != customerId)
            throw new InvalidOperationException("Invalid linked account");

        // Validate balance requirements
        if (!product.IsValidBalance(request.PrincipalAmount))
            throw new InvalidOperationException($"Principal amount must be between {product.MinimumBalance} and {product.MaximumBalance}");

        // Validate term
        var termDays = request.TermDays ?? product.DefaultTermDays ?? 365;
        if (!product.IsValidTerm(termDays))
            throw new InvalidOperationException($"Term must be between {product.MinimumTermDays} and {product.MaximumTermDays} days");

        // Check account balance
        if (linkedAccount.Balance < request.PrincipalAmount)
            throw new InvalidOperationException("Insufficient balance in linked account");

        var deposit = new FixedDeposit
        {
            CustomerId = customerId,
            DepositProductId = request.DepositProductId,
            LinkedAccountId = request.LinkedAccountId,
            PrincipalAmount = request.PrincipalAmount,
            InterestRate = product.GetApplicableRate(request.PrincipalAmount, termDays),
            TermDays = termDays,
            StartDate = DateTime.UtcNow,
            MaturityDate = DateTime.UtcNow.AddDays(termDays),
            Status = FixedDepositStatus.Active,
            InterestCalculationMethod = product.InterestCalculationMethod,
            CompoundingFrequency = product.CompoundingFrequency,
            LastInterestCalculationDate = DateTime.UtcNow,
            MaturityAction = request.MaturityAction ?? product.DefaultMaturityAction,
            AutoRenewalEnabled = request.AutoRenewalEnabled ?? product.AllowAutoRenewal,
            RenewalTermDays = request.RenewalTermDays,
            PenaltyType = product.PenaltyType,
            PenaltyAmount = product.PenaltyAmount,
            PenaltyPercentage = product.PenaltyPercentage,
            DepositNumber = TokenHelper.GenerateDepositNumber()
        };

        // Debit the linked account
        linkedAccount.Balance -= request.PrincipalAmount;
        
        await _unitOfWork.Repository<FixedDeposit>().AddAsync(deposit);
        _unitOfWork.Repository<Account>().Update(linkedAccount);

        // Create deposit transaction record
        var transaction = new DepositTransaction
        {
            FixedDepositId = deposit.Id,
            TransactionType = DepositTransactionType.InterestCredit,
            Amount = request.PrincipalAmount,
            Description = $"Fixed deposit creation - {deposit.DepositNumber}",
            TransactionDate = DateTime.UtcNow,
            Status = TransactionStatus.Completed,
            TransactionReference = TokenHelper.GenerateTransactionReference()
        };

        await _unitOfWork.Repository<DepositTransaction>().AddAsync(transaction);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogUserActionAsync(
            customerId,
            "FixedDeposit",
            "Create",
            deposit.Id.ToString(),
            $"Created fixed deposit: {deposit.DepositNumber} for {request.PrincipalAmount:C}");

        _logger.LogInformation("Created fixed deposit {DepositId} for customer {CustomerId}", deposit.Id, customerId);

        return await MapToFixedDepositDtoAsync(deposit);
    }

    public async Task<FixedDepositDto?> GetFixedDepositAsync(Guid depositId)
    {
        var deposit = await _unitOfWork.Repository<FixedDeposit>()
            .GetByIdAsync(depositId);

        return deposit == null ? null : await MapToFixedDepositDtoAsync(deposit);
    }

    public async Task<FixedDepositDto?> GetFixedDepositByNumberAsync(string depositNumber)
    {
        var deposits = await _unitOfWork.Repository<FixedDeposit>()
            .FindAsync(d => d.DepositNumber == depositNumber);

        var deposit = deposits.FirstOrDefault();
        return deposit == null ? null : await MapToFixedDepositDtoAsync(deposit);
    }

    public async Task<IEnumerable<FixedDepositDto>> GetCustomerFixedDepositsAsync(Guid customerId)
    {
        var deposits = await _unitOfWork.Repository<FixedDeposit>()
            .FindAsync(d => d.CustomerId == customerId);

        var result = new List<FixedDepositDto>();
        foreach (var deposit in deposits.OrderByDescending(d => d.CreatedAt))
        {
            result.Add(await MapToFixedDepositDtoAsync(deposit));
        }
        return result;
    }

    public async Task<IEnumerable<FixedDepositDto>> GetMaturingDepositsAsync(DateTime fromDate, DateTime toDate)
    {
        var deposits = await _unitOfWork.Repository<FixedDeposit>()
            .FindAsync(d => d.Status == FixedDepositStatus.Active && 
                           d.MaturityDate >= fromDate && 
                           d.MaturityDate <= toDate);

        var result = new List<FixedDepositDto>();
        foreach (var deposit in deposits.OrderBy(d => d.MaturityDate))
        {
            result.Add(await MapToFixedDepositDtoAsync(deposit));
        }
        return result;
    }
    private async Task<FixedDepositDto> MapToFixedDepositDtoAsync(FixedDeposit deposit)
        => await DepositMappingHelper.MapToFixedDepositDtoAsync(deposit);
}
