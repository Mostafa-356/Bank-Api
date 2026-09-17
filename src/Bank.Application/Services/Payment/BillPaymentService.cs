using Bank.Application.DTOs;
using Bank.Application.DTOs.Payment.Biller;
using Bank.Application.Interfaces;
using Bank.Domain.Common;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AutoMapper;

namespace Bank.Application.Services;

/// <summary>
/// Enhanced service implementation for bill payment operations with external integration
/// </summary>
public class BillPaymentService : IBillPaymentService
{
    private readonly IBillerRepository _billerRepository;
    private readonly IBillPaymentRepository _billPaymentRepository;
    private readonly IBillPaymentProcessingService _billPaymentProcessingService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BillPaymentService> _logger;
    private readonly IMapper _mapper;

    public BillPaymentService(
        IBillerRepository billerRepository,
        IBillPaymentRepository billPaymentRepository,
        IBillPaymentProcessingService billPaymentProcessingService,
        IUnitOfWork unitOfWork,
        ILogger<BillPaymentService> logger,
        IMapper mapper)
    {
        _billerRepository = billerRepository;
        _billPaymentRepository = billPaymentRepository;
        _billPaymentProcessingService = billPaymentProcessingService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<List<BillerDto>> GetAvailableBillersAsync()
    {
        var billers = await _billerRepository.GetActiveBillersAsync();
        return billers.Select(MapToBillerDto).ToList();
    }

    public async Task<List<BillerDto>> GetBillersByCategoryAsync(BillerCategory category)
    {
        var billers = await _billerRepository.GetBillersByCategoryAsync(category);
        return billers.Select(MapToBillerDto).ToList();
    }

    public async Task<List<BillerDto>> SearchBillersAsync(BillerSearchRequest request)
    {
        List<Biller> billers;

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            billers = await _billerRepository.SearchBillersByNameAsync(request.SearchTerm);
        }
        else if (request.Category.HasValue)
        {
            billers = await _billerRepository.GetBillersByCategoryAsync(request.Category.Value);
        }
        else
        {
            billers = request.ActiveOnly 
                ? await _billerRepository.GetActiveBillersAsync()
                : (await _billerRepository.GetAllAsync()).ToList();
        }

        if (request.ActiveOnly)
        {
            billers = billers.Where(b => b.IsActive).ToList();
        }

        return billers.Select(MapToBillerDto).ToList();
    }

    public async Task<BillerDto?> GetBillerByIdAsync(Guid billerId)
    {
        var biller = await _billerRepository.GetByIdAsync(billerId);
        return biller != null ? MapToBillerDto(biller) : null;
    }

    public async Task<ScheduleBillPaymentResponse> ScheduleBillPaymentAsync(Guid customerId, ScheduleBillPaymentRequest request)
    {
        // Delegate to BillPaymentProcessingService to avoid duplication
        return await _billPaymentProcessingService.ScheduleBillPaymentAsync(customerId, request);
    }

    public async Task<List<ProcessBillPaymentResponse>> ProcessBillPaymentAsync(DateTime? processingDate = null)
    {
        // Delegate to BillPaymentProcessingService to avoid duplication
        return await _billPaymentProcessingService.ProcessBillPaymentAsync(processingDate);
    }

    public async Task<Bank.Domain.Common.PagedResult<BillPaymentHistoryDto>> GetBillPaymentHistoryAsync(Guid customerId, BillPaymentHistoryRequest request)
    {
        var result = await _billPaymentRepository.GetCustomerPaymentHistoryAsync(
            customerId, 
            request.PageNumber, 
            request.PageSize,
            request.FromDate,
            request.ToDate);

        var historyItems = result.Items.Select(MapToBillPaymentHistoryDto).ToList();

        if (request.Status.HasValue)
        {
            historyItems = historyItems.Where(h => h.Status == request.Status.Value).ToList();
        }

        return new Bank.Domain.Common.PagedResult<BillPaymentHistoryDto>
        {
            Items = historyItems,
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<List<BillPaymentDto>> GetPendingBillPaymentsAsync(Guid customerId)
    {
        var payments = await _billPaymentRepository.GetCustomerPendingPaymentsAsync(customerId);
        return payments.Select(MapToBillPaymentDto).ToList();
    }

    public async Task<bool> CancelScheduledPaymentAsync(Guid customerId, Guid paymentId)
    {
        var payment = await _billPaymentRepository.GetByIdAsync(paymentId);
        
        if (payment == null || payment.CustomerId != customerId)
        {
            return false;
        }

        if (!payment.CanBeCancelled())
        {
            return false;
        }

        payment.Cancel();
        _billPaymentRepository.Update(payment);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Bill payment cancelled: {PaymentId} by customer {CustomerId}", 
            paymentId, customerId);

        return true;
    }

    public async Task<BillPaymentDto?> GetBillPaymentByIdAsync(Guid customerId, Guid paymentId)
    {
        var payment = await _billPaymentRepository.GetPaymentWithDetailsAsync(paymentId);
        
        if (payment == null || payment.CustomerId != customerId)
        {
            return null;
        }

        return MapToBillPaymentDto(payment);
    }

    public async Task<bool> UpdateScheduledPaymentAsync(Guid customerId, Guid paymentId, UpdateBillPaymentRequest request)
    {
        var payment = await _billPaymentRepository.GetByIdAsync(paymentId);
        
        if (payment == null || payment.CustomerId != customerId || !payment.CanBeCancelled())
        {
            return false;
        }

        // Validate the updated amount
        var biller = await _billerRepository.GetByIdAsync(payment.BillerId);
        if (biller != null && !biller.IsAmountValid(request.Amount))
        {
            return false;
        }

        payment.Amount = request.Amount;
        payment.ScheduledDate = request.ScheduledDate;
        payment.Reference = request.Reference ?? payment.Reference;
        payment.Description = request.Description ?? payment.Description;

        _billPaymentRepository.Update(payment);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateBillPaymentAsync(Guid customerId, ScheduleBillPaymentRequest request)
    {
        // Delegate to BillPaymentProcessingService to avoid duplication
        return await _billPaymentProcessingService.ValidateBillPaymentAsync(customerId, request);
    }

    #region Private Helper Methods

    private BillerDto MapToBillerDto(Biller biller)
    {
        return _mapper.Map<BillerDto>(biller);
    }

    private BillPaymentDto MapToBillPaymentDto(BillPayment payment)
    {
        return _mapper.Map<BillPaymentDto>(payment);
    }

    private BillPaymentHistoryDto MapToBillPaymentHistoryDto(BillPayment payment)
    {
        return _mapper.Map<BillPaymentHistoryDto>(payment);
    }

    #endregion
}