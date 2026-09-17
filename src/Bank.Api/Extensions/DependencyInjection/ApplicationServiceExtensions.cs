using Bank.Application.Interfaces;
using Bank.Application.Interfaces.Authorization;
using Bank.Application.Services.Authorization;
using Bank.Infrastructure.Services;

namespace Bank.Api.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for application service registration (business logic)
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Register all application services (business logic)
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Authentication & Authorization Services
        services.AddScoped<IAuthService, Bank.Application.Services.AuthService>();

        services.AddScoped<ISessionService, Bank.Application.Services.SessionService>();
        services.AddScoped<IAuthorizationHelper, AuthorizationHelper>();

        // Account Management Services
        services.AddScoped<IAccountService, Bank.Application.Services.AccountService>();
        services.AddScoped<IAccountLifecycleService, Bank.Application.Services.AccountLifecycleService>();
        services.AddScoped<IAccountLockoutService, Bank.Application.Services.AccountLockoutService>();
        services.AddScoped<IJointAccountService, Bank.Application.Services.JointAccountService>();

        // Transaction & Payment Services
        services.AddScoped<ITransactionService, Bank.Application.Services.TransactionService>();
        services.AddScoped<IBatchService, Bank.Application.Services.BatchService>();
        services.AddScoped<IRecurringPaymentService, Bank.Application.Services.RecurringPaymentService>();
        services.AddScoped<IPaymentTemplateService, Bank.Application.Services.PaymentTemplateService>();
        services.AddScoped<IBeneficiaryService, Bank.Application.Services.BeneficiaryService>();
        services.AddScoped<IAccountValidationService, Bank.Application.Services.AccountValidationService>();
        services.AddScoped<ITransferEligibilityService, Bank.Application.Services.TransferEligibilityService>();

        // Statement Services
        services.AddScoped<IStatementService, Bank.Application.Services.StatementService>();
        services.AddScoped<IStatementGenerator, Bank.Application.Services.StatementGenerator>();

        // Loan Services
        services.AddScoped<ILoanService, Bank.Application.Services.LoanService>();
        services.AddScoped<ILoanInterestCalculationService, Bank.Application.Services.LoanInterestCalculationService>();
        services.AddScoped<ILoanAnalyticsService, Bank.Application.Services.LoanAnalyticsService>();
        
        // Card Services
        services.AddScoped<ICardService, Bank.Application.Services.CardService>();
        services.AddScoped<ICardNetworkService, Bank.Application.Services.CardNetworkService>();
        services.AddScoped<IPinManagementService, Bank.Application.Services.PinManagementService>();
        
        // Bill Payment Services
        services.AddScoped<IBillPaymentService, Bank.Application.Services.BillPaymentService>();
        services.AddScoped<IBillPaymentProcessingService, Bank.Application.Services.BillPaymentProcessingService>();
        services.AddScoped<IBillerIntegrationService, Bank.Application.Services.BillerIntegrationService>();
        services.AddScoped<IPaymentRetryService, Bank.Application.Services.PaymentRetryService>();
        services.AddScoped<IPaymentReceiptService, Bank.Application.Services.PaymentReceiptService>();
        services.AddScoped<IBillPresentmentService, Bank.Application.Services.BillPresentmentService>();
        services.AddScoped<IBatchPaymentService, Bank.Application.Services.BatchPaymentService>();
        services.AddScoped<Bank.Application.Interfaces.Payment.IBeneficiaryValidationService, Bank.Application.Services.BeneficiaryValidationService>();
        services.AddScoped<Bank.Application.Interfaces.Payment.IPaymentRetryNotificationService, Bank.Application.Services.PaymentRetryNotificationService>();
        services.AddScoped<Bank.Application.Interfaces.Payment.IPaymentReceiptGenerationService, Bank.Application.Services.PaymentReceiptGenerationService>();

        // Deposit Services (Specialized by Responsibility)
        services.AddScoped<IDepositService, Bank.Application.Services.DepositService>();
        services.AddScoped<IDepositProductService, Bank.Application.Services.DepositProductManagementService>();
        services.AddScoped<IInterestTierService, Bank.Application.Services.InterestTierService>();
        services.AddScoped<IFixedDepositService, Bank.Application.Services.FixedDepositService>();
        services.AddScoped<IDepositMaturityService, Bank.Application.Services.DepositMaturityService>();
        services.AddScoped<IDepositWithdrawalService, Bank.Application.Services.DepositWithdrawalService>();
        services.AddScoped<IDepositCertificateGenerator, Bank.Application.Services.DepositCertificateGenerator>();
        // TODO: Implement remaining deposit specialized services in Phase 1b
        // services.AddScoped<IDepositReportingService, Bank.Application.Services.DepositReportingService>();

        // HTTP Client for external integrations
        services.AddHttpClient<Bank.Application.Services.BillerIntegrationService>();

        // Notification Services
        services.AddScoped<INotificationService, Bank.Application.Services.NotificationService>();

        // Background Services (only if database is available)
        var allowOfflineMode = configuration.GetValue<bool>("DatabaseSettings:AllowOfflineMode", false);
        if (!allowOfflineMode)
        {
            services.AddHostedService<Bank.Application.Services.LoanBackgroundService>();
            services.AddHostedService<Bank.Application.Services.BillPaymentBackgroundService>();
            services.AddHostedService<Bank.Application.Services.BillerHealthCheckBackgroundService>();
            services.AddHostedService<Bank.Application.Services.DepositBackgroundService>();
            services.AddHostedService<Bank.Application.Services.PaymentRetryBackgroundService>();
        }

        // Financial Calculation Services
        services.AddScoped<IFeeCalculationService, Bank.Application.Services.FeeCalculationService>();
        services.AddScoped<IInterestCalculationService, Bank.Application.Services.InterestCalculationService>();

        // Template Service (centralized template management)
        services.AddSingleton<ITemplateService, Bank.Infrastructure.Services.TemplateService>();

        // Security & Compliance Services
        services.AddScoped<IFraudDetectionService, Bank.Application.Services.FraudDetectionService>();
        services.AddScoped<IAuditLogService, Bank.Application.Services.AuditLogService>();
        services.AddScoped<IAuditEventPublisher, Bank.Application.Services.AuditEventPublisher>();
        services.AddScoped<IPasswordPolicyService, Bank.Application.Services.PasswordPolicyService>();
        services.AddScoped<IIpWhitelistService, Bank.Application.Services.IpWhitelistService>();

        // Utility Services
        services.AddScoped<ITokenGenerationService, Bank.Application.Services.TokenGenerationService>();
        services.AddScoped<ICalculationService, Bank.Application.Services.CalculationService>();
        services.AddScoped<IValidationService, Bank.Application.Services.ValidationService>();

        return services;
    }
}
