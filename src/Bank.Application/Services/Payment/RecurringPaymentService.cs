using Bank.Application.DTOs;
using Bank.Application.DTOs.Payment.Recurring;
using Bank.Application.DTOs.Transaction.Core;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

public class RecurringPaymentService : IRecurringPaymentService
{
    private readonly IRecurringPaymentRepository _recurringPaymentRepository;
    private readonly ITransactionService _transactionService;
    private readonly IAccountService _accountService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<RecurringPaymentService> _logger;

    public RecurringPaymentService(
        IRecurringPaymentRepository recurringPaymentRepository,
        ITransactionService transactionService,
        IAccountService accountService,
        IAuditLogService auditLogService,
        ILogger<RecurringPaymentService> logger)
    {
        _recurringPaymentRepository = recurringPaymentRepository;
        _transactionService = transactionService;
        _accountService = accountService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<RecurringPayment> CreateRecurringPaymentAsync(CreateRecurringPaymentRequest request)
    {
        // Validate accounts exist and are active
        var fromAccount = await _accountService.GetAccountByIdAsync(request.FromAccountId);
        var toAccount = await _accountService.GetAccountByIdAsync(request.ToAccountId);
        
        if (fromAccount == null || toAccount == null)
            throw new ArgumentException("Invalid account specified");

        var recurringPayment = new RecurringPayment
        {
            Id = Guid.NewGuid(),
            FromAccountId = request.FromAccountId,
            ToAccountId = request.ToAccountId,
            Amount = request.Amount,
            Description = request.Description,
            Reference = request.Reference,
            Frequency = request.Frequency,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaxOccurrences = request.MaxOccurrences,
            NextExecutionDate = request.StartDate,
            MaxRetries = request.MaxRetries,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _recurringPaymentRepository.AddAsync(recurringPayment);
        
        await _auditLogService.LogUserActionAsync(
            request.CreatedByUserId,
            "RecurringPaymentCreated",
            $"Created recurring payment from {request.FromAccountId} to {request.ToAccountId} for {request.Amount:C}",
            recurringPayment.Id.ToString());

        _logger.LogInformation("Created recurring payment {RecurringPaymentId} for user {UserId}", 
            recurringPayment.Id, request.CreatedByUserId);

        return recurringPayment;
    }

    public async Task<RecurringPayment?> GetRecurringPaymentAsync(Guid id)
    {
        return await _recurringPaymentRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<RecurringPayment>> GetUserRecurringPaymentsAsync(Guid userId)
    {
        return await _recurringPaymentRepository.GetByUserIdAsync(userId);
    }

    public async Task<IEnumerable<RecurringPayment>> GetAccountRecurringPaymentsAsync(Guid accountId)
    {
        return await _recurringPaymentRepository.GetByAccountIdAsync(accountId);
    }

    public async Task<bool> UpdateRecurringPaymentAsync(Guid id, UpdateRecurringPaymentRequest request)
    {
        var recurringPayment = await _recurringPaymentRepository.GetByIdAsync(id);
        if (recurringPayment == null)
            return false;

        if (request.Amount.HasValue)
            recurringPayment.Amount = request.Amount.Value;
        
        if (!string.IsNullOrEmpty(request.Description))
            recurringPayment.Description = request.Description;
        
        if (request.Reference != null)
            recurringPayment.Reference = request.Reference;
        
        if (request.Frequency.HasValue)
        {
            recurringPayment.Frequency = request.Frequency.Value;
            recurringPayment.NextExecutionDate = recurringPayment.CalculateNextExecutionDate();
        }
        
        if (request.EndDate.HasValue)
            recurringPayment.EndDate = request.EndDate.Value;
        
        if (request.MaxOccurrences.HasValue)
            recurringPayment.MaxOccurrences = request.MaxOccurrences.Value;
        
        if (request.MaxRetries.HasValue)
            recurringPayment.MaxRetries = request.MaxRetries.Value;

        recurringPayment.UpdatedAt = DateTime.UtcNow;
        await _recurringPaymentRepository.UpdateAsync(recurringPayment);

        await _auditLogService.LogUserActionAsync(
            recurringPayment.CreatedByUserId,
            "RecurringPaymentUpdated",
            $"Updated recurring payment {id}",
            id.ToString());

        return true;
    }

    public async Task<bool> PauseRecurringPaymentAsync(Guid id, string reason)
    {
        var recurringPayment = await _recurringPaymentRepository.GetByIdAsync(id);
        if (recurringPayment == null)
            return false;

        recurringPayment.Pause(reason);
        recurringPayment.UpdatedAt = DateTime.UtcNow;
        await _recurringPaymentRepository.UpdateAsync(recurringPayment);

        await _auditLogService.LogUserActionAsync(
            recurringPayment.CreatedByUserId,
            "RecurringPaymentPaused",
            $"Paused recurring payment {id}: {reason}",
            id.ToString());

        return true;
    }

    public async Task<bool> ResumeRecurringPaymentAsync(Guid id)
    {
        var recurringPayment = await _recurringPaymentRepository.GetByIdAsync(id);
        if (recurringPayment == null)
            return false;

        recurringPayment.Resume();
        recurringPayment.UpdatedAt = DateTime.UtcNow;
        await _recurringPaymentRepository.UpdateAsync(recurringPayment);

        await _auditLogService.LogUserActionAsync(
            recurringPayment.CreatedByUserId,
            "RecurringPaymentResumed",
            $"Resumed recurring payment {id}",
            id.ToString());

        return true;
    }

    public async Task<bool> CancelRecurringPaymentAsync(Guid id, string reason)
    {
        var recurringPayment = await _recurringPaymentRepository.GetByIdAsync(id);
        if (recurringPayment == null)
            return false;

        recurringPayment.Cancel(reason);
        recurringPayment.UpdatedAt = DateTime.UtcNow;
        await _recurringPaymentRepository.UpdateAsync(recurringPayment);

        await _auditLogService.LogUserActionAsync(
            recurringPayment.CreatedByUserId,
            "RecurringPaymentCancelled",
            $"Cancelled recurring payment {id}: {reason}",
            id.ToString());

        return true;
    }

    public async Task<IEnumerable<RecurringPayment>> GetDueRecurringPaymentsAsync()
    {
        return await _recurringPaymentRepository.GetDuePaymentsAsync();
    }

    public async Task<bool> ExecuteRecurringPaymentAsync(Guid recurringPaymentId)
    {
        var recurringPayment = await _recurringPaymentRepository.GetByIdAsync(recurringPaymentId);
        if (recurringPayment == null || !recurringPayment.ShouldExecute())
            return false;

        try
        {
            // Create execution record
            var execution = new RecurringPaymentExecution
            {
                Id = Guid.NewGuid(),
                RecurringPaymentId = recurringPaymentId,
                ScheduledDate = recurringPayment.NextExecutionDate,
                Amount = recurringPayment.Amount,
                Status = RecurringPaymentExecutionStatus.Processing,
                CreatedAt = DateTime.UtcNow
            };

            // Execute the transaction
            var transactionRequest = new CreateTransactionRequest
            {
                FromAccountId = recurringPayment.FromAccountId,
                ToAccountId = recurringPayment.ToAccountId,
                Amount = recurringPayment.Amount,
                Description = $"Recurring Payment: {recurringPayment.Description}",
                Reference = recurringPayment.Reference,
                Type = TransactionType.ACH
            };

            var transaction = await _transactionService.CreateTransactionAsync(transactionRequest);
            
            execution.TransactionId = transaction.Id;
            execution.ExecutedDate = DateTime.UtcNow;
            execution.Status = RecurringPaymentExecutionStatus.Completed;

            // Update recurring payment
            recurringPayment.RecordExecution(true);
            recurringPayment.UpdatedAt = DateTime.UtcNow;

            await _recurringPaymentRepository.UpdateAsync(recurringPayment);
            await _recurringPaymentRepository.AddExecutionAsync(execution);

            _logger.LogInformation("Successfully executed recurring payment {RecurringPaymentId}", recurringPaymentId);
            return true;
        }
        catch (Exception ex)
        {
            // Record failure
            var execution = new RecurringPaymentExecution
            {
                Id = Guid.NewGuid(),
                RecurringPaymentId = recurringPaymentId,
                ScheduledDate = recurringPayment.NextExecutionDate,
                Amount = recurringPayment.Amount,
                Status = RecurringPaymentExecutionStatus.Failed,
                FailureReason = ex.Message,
                CreatedAt = DateTime.UtcNow
            };

            recurringPayment.RecordExecution(false, ex.Message);
            recurringPayment.UpdatedAt = DateTime.UtcNow;

            await _recurringPaymentRepository.UpdateAsync(recurringPayment);
            await _recurringPaymentRepository.AddExecutionAsync(execution);

            _logger.LogError(ex, "Failed to execute recurring payment {RecurringPaymentId}", recurringPaymentId);
            return false;
        }
    }

    public async Task<int> ProcessDueRecurringPaymentsAsync()
    {
        var duePayments = await GetDueRecurringPaymentsAsync();
        int processedCount = 0;

        foreach (var payment in duePayments)
        {
            if (await ExecuteRecurringPaymentAsync(payment.Id))
                processedCount++;
        }

        _logger.LogInformation("Processed {ProcessedCount} out of {TotalCount} due recurring payments", 
            processedCount, duePayments.Count());

        return processedCount;
    }

    public async Task<IEnumerable<RecurringPaymentExecution>> GetExecutionHistoryAsync(Guid recurringPaymentId)
    {
        return await _recurringPaymentRepository.GetExecutionHistoryAsync(recurringPaymentId);
    }

    public async Task<BulkTransferResult> ProcessBulkTransfersAsync(BulkTransferRequest request)
    {
        var batchId = Guid.NewGuid();
        var result = new BulkTransferResult
        {
            BatchId = batchId,
            Status = BulkTransferStatus.Processing,
            TotalTransfers = request.Transfers.Count,
            TotalAmount = request.Transfers.Sum(t => t.Amount),
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            foreach (var transfer in request.Transfers)
            {
                var itemResult = new BulkTransferItemResult
                {
                    ToAccountId = transfer.ToAccountId,
                    Amount = transfer.Amount,
                    Reference = transfer.Reference
                };

                try
                {
                    var transactionRequest = new CreateTransactionRequest
                    {
                        FromAccountId = request.FromAccountId,
                        ToAccountId = transfer.ToAccountId,
                        Amount = transfer.Amount,
                        Description = transfer.Description ?? request.Description,
                        Reference = transfer.Reference,
                        Type = TransactionType.ACH
                    };

                    var transaction = await _transactionService.CreateTransactionAsync(transactionRequest);
                    itemResult.Success = true;
                    itemResult.TransactionId = transaction.Id;
                    result.SuccessfulTransfers++;
                }
                catch (Exception ex)
                {
                    itemResult.Success = false;
                    itemResult.ErrorMessage = ex.Message;
                    result.FailedTransfers++;
                }

                result.Results.Add(itemResult);
            }

            result.Status = result.FailedTransfers == 0 ? BulkTransferStatus.Completed : 
                           result.SuccessfulTransfers == 0 ? BulkTransferStatus.Failed : 
                           BulkTransferStatus.PartiallyCompleted;
            result.CompletedAt = DateTime.UtcNow;

            await _auditLogService.LogUserActionAsync(
                request.CreatedByUserId,
                "BulkTransferProcessed",
                $"Processed bulk transfer batch {batchId}: {result.SuccessfulTransfers}/{result.TotalTransfers} successful",
                batchId.ToString());

            return result;
        }
        catch (Exception ex)
        {
            result.Status = BulkTransferStatus.Failed;
            result.CompletedAt = DateTime.UtcNow;
            
            _logger.LogError(ex, "Failed to process bulk transfer batch {BatchId}", batchId);
            throw;
        }
    }

    public async Task<BulkTransferResult> GetBulkTransferStatusAsync(Guid batchId)
    {
        // This would typically be stored in a repository
        // For now, return a placeholder implementation
        throw new NotImplementedException("Bulk transfer status retrieval not yet implemented");
    }
}