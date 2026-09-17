using Bank.Application.DTOs.Payment.Biller;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for bill payment processing operations
/// Handles payment scheduling, processing, cancellation, and updates
/// </summary>
public class BillPaymentProcessingService : IBillPaymentProcessingService
{
    private readonly IBillPaymentRepository _billPaymentRepository;
    private readonly IBillerRepository _billerRepository;
    private readonly IAccountService _accountService;
    private readonly IBillerIntegrationService _billerIntegrationService;
    private readonly IPaymentRetryService _paymentRetryService;
    private readonly IPaymentReceiptService _paymentReceiptService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BillPaymentProcessingService> _logger;

    public BillPaymentProcessingService(
        IBillPaymentRepository billPaymentRepository,
        IBillerRepository billerRepository,
        IAccountService accountService,
        IBillerIntegrationService billerIntegrationService,
        IPaymentRetryService paymentRetryService,
        IPaymentReceiptService paymentReceiptService,
        IUnitOfWork unitOfWork,
        ILogger<BillPaymentProcessingService> logger)
    {
        _billPaymentRepository = billPaymentRepository;
        _billerRepository = billerRepository;
        _accountService = accountService;
        _billerIntegrationService = billerIntegrationService;
        _paymentRetryService = paymentRetryService;
        _paymentReceiptService = paymentReceiptService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ScheduleBillPaymentResponse> ScheduleBillPaymentAsync(Guid customerId, ScheduleBillPaymentRequest request)
    {
        // Validate the request
        var (isValid, errorMessage) = await ValidateBillPaymentAsync(customerId, request);
        if (!isValid)
        {
            return new ScheduleBillPaymentResponse
            {
                Status = BillPaymentStatus.Failed,
                Message = errorMessage
            };
        }

        var biller = await _billerRepository.GetByIdAsync(request.BillerId);
        if (biller == null)
        {
            return new ScheduleBillPaymentResponse
            {
                Status = BillPaymentStatus.Failed,
                Message = "Biller not found"
            };
        }

        // Create the bill payment
        var billPayment = new BillPayment
        {
            CustomerId = customerId,
            BillerId = request.BillerId,
            Amount = request.Amount,
            Currency = request.Currency,
            ScheduledDate = request.ScheduledDate,
            Status = BillPaymentStatus.Pending,
            Reference = request.Reference ?? string.Empty,
            Description = request.Description ?? string.Empty
        };

        await _billPaymentRepository.AddAsync(billPayment);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Bill payment scheduled: {PaymentId} for customer {CustomerId} to biller {BillerId}", 
            billPayment.Id, customerId, request.BillerId);

        return new ScheduleBillPaymentResponse
        {
            PaymentId = billPayment.Id,
            Status = BillPaymentStatus.Pending,
            ScheduledDate = request.ScheduledDate,
            ExpectedProcessingDate = biller.CalculateProcessingDate(request.ScheduledDate),
            Message = "Bill payment scheduled successfully"
        };
    }

    public async Task<List<ProcessBillPaymentResponse>> ProcessBillPaymentAsync(DateTime? processingDate = null)
    {
        var processDate = processingDate ?? DateTime.UtcNow;
        var duePayments = await _billPaymentRepository.GetScheduledPaymentsDueAsync(processDate);
        var responses = new List<ProcessBillPaymentResponse>();

        foreach (var payment in duePayments)
        {
            try
            {
                // Check if customer has sufficient funds
                var customerAccounts = await _accountService.GetUserAccountsAsync(payment.CustomerId);
                var primaryAccount = customerAccounts.FirstOrDefault(a => a.Type == AccountType.Checking);

                if (primaryAccount == null || primaryAccount.Balance < payment.Amount)
                {
                    payment.MarkAsFailed();
                    
                    // Schedule for retry
                    await _paymentRetryService.SchedulePaymentRetryAsync(new PaymentRetryRequest
                    {
                        PaymentId = payment.Id,
                        RetryAttempt = 1,
                        FailureReason = "Insufficient funds"
                    });

                    responses.Add(new ProcessBillPaymentResponse
                    {
                        PaymentId = payment.Id,
                        Status = BillPaymentStatus.Failed,
                        Message = "Insufficient funds - scheduled for retry", Success = false
                    });
                    continue;
                }

                // Send payment to external biller system
                var billerRequest = new BillerPaymentRequest(
                    payment.Id,
                    payment.BillerId,
                    payment.Biller.AccountNumber,
                    payment.Biller.RoutingNumber,
                    payment.Amount,
                    payment.Currency,
                    payment.Reference,
                    payment.Description,
                    payment.ScheduledDate
                );

                var billerResponse = await _billerIntegrationService.SendPaymentToBillerAsync(billerRequest);

                if (billerResponse.Success)
                {
                    payment.MarkAsProcessed();
                    
                    // Generate payment receipt
                    await _paymentReceiptService.GeneratePaymentReceiptAsync(payment.Id);
                    
                    responses.Add(new ProcessBillPaymentResponse
                    {
                        PaymentId = payment.Id,
                        Status = BillPaymentStatus.Processed,
                        ProcessedDate = payment.ProcessedDate,
                        Message = "Payment processed successfully", Success = true
                    });
                }
                else
                {
                    payment.MarkAsFailed();
                    
                    // Schedule for retry
                    await _paymentRetryService.SchedulePaymentRetryAsync(new PaymentRetryRequest
                    {
                        PaymentId = payment.Id,
                        RetryAttempt = 1,
                        FailureReason = billerResponse.Message
                    });

                    responses.Add(new ProcessBillPaymentResponse
                    {
                        PaymentId = payment.Id,
                        Status = BillPaymentStatus.Failed,
                        Message = $"Payment failed: {billerResponse.Message} - scheduled for retry", Success = false
                    });
                }

                _logger.LogInformation("Bill payment processed: {PaymentId} for amount {Amount} - Status: {Status}", 
                    payment.Id, payment.Amount, payment.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bill payment {PaymentId}", payment.Id);
                payment.MarkAsFailed();
                
                // Schedule for retry
                await _paymentRetryService.SchedulePaymentRetryAsync(new PaymentRetryRequest
                {
                    PaymentId = payment.Id,
                    RetryAttempt = 1,
                    FailureReason = ex.Message
                });

                responses.Add(new ProcessBillPaymentResponse
                {
                    PaymentId = payment.Id,
                    Status = BillPaymentStatus.Failed,
                    Message = "Payment processing failed - scheduled for retry", Success = false
                });
            }

            _billPaymentRepository.Update(payment);
        }

        if (duePayments.Any())
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return responses;
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

        _logger.LogInformation("Bill payment updated: {PaymentId} by customer {CustomerId}", 
            paymentId, customerId);

        return true;
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateBillPaymentAsync(Guid customerId, ScheduleBillPaymentRequest request)
    {
        // Check if biller exists and is active
        var biller = await _billerRepository.GetByIdAsync(request.BillerId);
        if (biller == null)
        {
            return (false, "Biller not found");
        }

        if (!biller.IsActive)
        {
            return (false, "Biller is not active");
        }

        // Validate amount
        if (!biller.IsAmountValid(request.Amount))
        {
            return (false, $"Amount must be between {biller.MinAmount:C} and {biller.MaxAmount:C}");
        }

        // Validate scheduled date
        if (request.ScheduledDate.Date < DateTime.UtcNow.Date)
        {
            return (false, "Scheduled date cannot be in the past");
        }

        // Check if customer has sufficient funds (basic check)
        var customerAccounts = await _accountService.GetUserAccountsAsync(customerId);
        var totalBalance = customerAccounts.Where(a => a.Status == AccountStatus.Active).Sum(a => a.Balance);
        
        if (totalBalance < request.Amount)
        {
            return (false, "Insufficient funds");
        }

        return (true, string.Empty);
    }
}
