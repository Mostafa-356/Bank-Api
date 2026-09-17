using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Bank.Application.Services;

public class PaymentTemplateService : IPaymentTemplateService
{
    private readonly IPaymentTemplateRepository _templateRepository;
    private readonly ITransactionService _transactionService;
    private readonly IAccountService _accountService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<PaymentTemplateService> _logger;

    public PaymentTemplateService(
        IPaymentTemplateRepository templateRepository,
        ITransactionService transactionService,
        IAccountService accountService,
        IAuditLogService auditLogService,
        ILogger<PaymentTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _transactionService = transactionService;
        _accountService = accountService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<PaymentTemplate> CreateTemplateAsync(CreatePaymentTemplateRequest request)
    {
        // Validate from account exists
        var fromAccount = await _accountService.GetAccountByIdAsync(request.FromAccountId);
        if (fromAccount == null)
            throw new ArgumentException("Invalid from account specified");

        // Validate to account if specified
        if (request.ToAccountId.HasValue)
        {
            var toAccount = await _accountService.GetAccountByIdAsync(request.ToAccountId.Value);
            if (toAccount == null)
                throw new ArgumentException("Invalid to account specified");
        }

        var template = new PaymentTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            FromAccountId = request.FromAccountId,
            ToAccountId = request.ToAccountId,
            BeneficiaryName = request.BeneficiaryName,
            BeneficiaryAccountNumber = request.BeneficiaryAccountNumber,
            BeneficiaryBankCode = request.BeneficiaryBankCode,
            Amount = request.Amount,
            Type = request.Type,
            Reference = request.Reference,
            Notes = request.Notes,
            Category = request.Category,
            Tags = request.Tags != null ? JsonSerializer.Serialize(request.Tags) : null,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _templateRepository.AddAsync(template);

        await _auditLogService.LogUserActionAsync(
            request.CreatedByUserId,
            "PaymentTemplateCreated",
            $"Created payment template '{request.Name}' for account {request.FromAccountId}",
            template.Id.ToString());

        _logger.LogInformation("Created payment template {TemplateId} '{TemplateName}' for user {UserId}", 
            template.Id, SecureLoggingService.SanitizeInput(request.Name), request.CreatedByUserId);

        return template;
    }

    public async Task<PaymentTemplate?> GetTemplateAsync(Guid id)
    {
        return await _templateRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<PaymentTemplate>> GetUserTemplatesAsync(Guid userId)
    {
        return await _templateRepository.GetByUserIdAsync(userId);
    }

    public async Task<IEnumerable<PaymentTemplate>> GetAccountTemplatesAsync(Guid accountId)
    {
        return await _templateRepository.GetByAccountIdAsync(accountId);
    }

    public async Task<IEnumerable<PaymentTemplate>> GetTemplatesByCategoryAsync(Guid userId, PaymentTemplateCategory category)
    {
        return await _templateRepository.GetByCategoryAsync(userId, category);
    }

    public async Task<bool> UpdateTemplateAsync(Guid id, UpdatePaymentTemplateRequest request)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            return false;

        if (!string.IsNullOrEmpty(request.Name))
            template.Name = request.Name;
        
        if (!string.IsNullOrEmpty(request.Description))
            template.Description = request.Description;
        
        if (request.ToAccountId.HasValue)
        {
            // Validate account exists
            var toAccount = await _accountService.GetAccountByIdAsync(request.ToAccountId.Value);
            if (toAccount == null)
                throw new ArgumentException("Invalid to account specified");
            template.ToAccountId = request.ToAccountId.Value;
        }
        
        if (!string.IsNullOrEmpty(request.BeneficiaryName))
            template.BeneficiaryName = request.BeneficiaryName;
        
        if (!string.IsNullOrEmpty(request.BeneficiaryAccountNumber))
            template.BeneficiaryAccountNumber = request.BeneficiaryAccountNumber;
        
        if (!string.IsNullOrEmpty(request.BeneficiaryBankCode))
            template.BeneficiaryBankCode = request.BeneficiaryBankCode;
        
        if (request.Amount.HasValue)
            template.Amount = request.Amount.Value;
        
        if (request.Type.HasValue)
            template.Type = request.Type.Value;
        
        if (request.Reference != null)
            template.Reference = request.Reference;
        
        if (request.Notes != null)
            template.Notes = request.Notes;
        
        if (request.Category.HasValue)
            template.Category = request.Category.Value;
        
        if (request.Tags != null)
            template.Tags = JsonSerializer.Serialize(request.Tags);

        template.UpdatedAt = DateTime.UtcNow;
        await _templateRepository.UpdateAsync(template);

        await _auditLogService.LogUserActionAsync(
            template.CreatedByUserId,
            "PaymentTemplateUpdated",
            $"Updated payment template '{template.Name}' ({id})",
            id.ToString());

        return true;
    }

    public async Task<bool> DeleteTemplateAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            return false;

        await _templateRepository.DeleteAsync(id);

        await _auditLogService.LogUserActionAsync(
            template.CreatedByUserId,
            "PaymentTemplateDeleted",
            $"Deleted payment template '{template.Name}' ({id})",
            id.ToString());

        return true;
    }

    public async Task<bool> ActivateTemplateAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            return false;

        template.Activate();
        template.UpdatedAt = DateTime.UtcNow;
        await _templateRepository.UpdateAsync(template);

        await _auditLogService.LogUserActionAsync(
            template.CreatedByUserId,
            "PaymentTemplateActivated",
            $"Activated payment template '{template.Name}' ({id})",
            id.ToString());

        return true;
    }

    public async Task<bool> DeactivateTemplateAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            return false;

        template.Deactivate();
        template.UpdatedAt = DateTime.UtcNow;
        await _templateRepository.UpdateAsync(template);

        await _auditLogService.LogUserActionAsync(
            template.CreatedByUserId,
            "PaymentTemplateDeactivated",
            $"Deactivated payment template '{template.Name}' ({id})",
            id.ToString());

        return true;
    }

    public async Task<Transaction> ExecuteTemplateAsync(Guid templateId, ExecuteTemplateRequest request)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
            throw new ArgumentException("Template not found");

        if (!template.IsActive)
            throw new InvalidOperationException("Template is not active");

        // Use template values or override with request values
        var amount = request.Amount ?? template.Amount;
        if (!amount.HasValue)
            throw new ArgumentException("Amount must be specified either in template or request");

        var toAccountId = request.ToAccountId ?? template.ToAccountId;
        if (!toAccountId.HasValue)
            throw new ArgumentException("To account must be specified either in template or request");

        var transactionRequest = new CreateTransactionRequest
        {
            FromAccountId = template.FromAccountId,
            ToAccountId = toAccountId.Value,
            Amount = amount.Value,
            Description = request.Description ?? template.Description,
            Reference = request.Reference ?? template.Reference,
            Type = template.Type
        };

        var transaction = await _transactionService.CreateTransactionAsync(transactionRequest);

        // Record template usage
        template.RecordUsage();
        template.UpdatedAt = DateTime.UtcNow;
        await _templateRepository.UpdateAsync(template);

        await _auditLogService.LogUserActionAsync(
            template.CreatedByUserId,
            "PaymentTemplateExecuted",
            $"Executed payment template '{template.Name}' for {amount:C}",
            templateId.ToString());

        _logger.LogInformation("Executed payment template {TemplateId} '{TemplateName}' creating transaction {TransactionId}", 
            templateId, template.Name, transaction.Id);

        return transaction;
    }

    public async Task<IEnumerable<PaymentTemplate>> GetMostUsedTemplatesAsync(Guid userId, int count = 10)
    {
        return await _templateRepository.GetMostUsedAsync(userId, count);
    }

    public async Task<IEnumerable<PaymentTemplate>> GetRecentlyUsedTemplatesAsync(Guid userId, int count = 10)
    {
        return await _templateRepository.GetRecentlyUsedAsync(userId, count);
    }

    public async Task<IEnumerable<PaymentTemplate>> SearchTemplatesAsync(Guid userId, string searchTerm)
    {
        return await _templateRepository.SearchAsync(userId, searchTerm);
    }

    public async Task<IEnumerable<PaymentTemplate>> GetTemplatesByTagsAsync(Guid userId, string[] tags)
    {
        return await _templateRepository.GetByTagsAsync(userId, tags);
    }
}