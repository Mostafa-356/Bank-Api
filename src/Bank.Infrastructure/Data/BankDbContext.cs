using Bank.Domain.Entities;
using Bank.Infrastructure.Data.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Bank.Infrastructure.Data;

/// <summary>
/// Application database context using ASP.NET Core Identity with Guid keys.
/// Includes automatic soft delete filtering and audit field population.
/// Configured for Neon serverless PostgreSQL with automatic retry and scaling support.
/// </summary>
public class BankDbContext : IdentityDbContext<User, Role, Guid>
{
    private readonly IConfiguration? _configuration;

    public BankDbContext(DbContextOptions<BankDbContext> options, IConfiguration configuration) : base(options)
    {
        _configuration = configuration;
    }

    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options)
    {
        _configuration = null;
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<BatchJob> BatchJobs => Set<BatchJob>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<AccountLockout> AccountLockouts => Set<AccountLockout>();
    public DbSet<IpWhitelist> IpWhitelists => Set<IpWhitelist>();
    public DbSet<PasswordPolicy> PasswordPolicies => Set<PasswordPolicy>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<AccountFee> AccountFees => Set<AccountFee>();
    public DbSet<AccountHold> AccountHolds => Set<AccountHold>();
    public DbSet<AccountRestriction> AccountRestrictions => Set<AccountRestriction>();
    public DbSet<AccountStatusHistory> AccountStatusHistories => Set<AccountStatusHistory>();
    public DbSet<FeeSchedule> FeeSchedules => Set<FeeSchedule>();
    public DbSet<JointAccountHolder> JointAccountHolders => Set<JointAccountHolder>();
    public DbSet<RecurringPayment> RecurringPayments => Set<RecurringPayment>();
    public DbSet<RecurringPaymentExecution> RecurringPaymentExecutions => Set<RecurringPaymentExecution>();
    public DbSet<PaymentTemplate> PaymentTemplates => Set<PaymentTemplate>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
    public DbSet<AccountStatement> AccountStatements => Set<AccountStatement>();
    public DbSet<StatementTransaction> StatementTransactions => Set<StatementTransaction>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanPayment> LoanPayments => Set<LoanPayment>();
    public DbSet<LoanDocument> LoanDocuments => Set<LoanDocument>();
    public DbSet<LoanStatusHistory> LoanStatusHistories => Set<LoanStatusHistory>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardTransaction> CardTransactions => Set<CardTransaction>();
    public DbSet<CardAuthorization> CardAuthorizations => Set<CardAuthorization>();
    public DbSet<CardStatement> CardStatements => Set<CardStatement>();
    public DbSet<CardStatusHistory> CardStatusHistories => Set<CardStatusHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Biller> Billers => Set<Biller>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();
    public DbSet<BillPresentment> BillPresentments => Set<BillPresentment>();
    public DbSet<PaymentReceipt> PaymentReceipts => Set<PaymentReceipt>();
    public DbSet<PaymentRetry> PaymentRetries => Set<PaymentRetry>();
    public DbSet<BillerHealthCheck> BillerHealthChecks => Set<BillerHealthCheck>();
    public DbSet<DepositProduct> DepositProducts => Set<DepositProduct>();
    public DbSet<InterestTier> InterestTiers => Set<InterestTier>();
    public DbSet<FixedDeposit> FixedDeposits => Set<FixedDeposit>();
    public DbSet<DepositTransaction> DepositTransactions => Set<DepositTransaction>();
    public DbSet<DepositCertificate> DepositCertificates => Set<DepositCertificate>();
    public DbSet<MaturityNotice> MaturityNotices => Set<MaturityNotice>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all entity configurations from the Configurations folder
        builder.ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly);

        // Global soft delete query filter for entities inheriting BaseEntity
        ApplySoftDeleteFilters(builder);
    }

    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        builder.Entity<Account>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Transaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BatchJob>().HasQueryFilter(e => !e.IsDeleted);

        builder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Session>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AccountLockout>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<IpWhitelist>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PasswordPolicy>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PasswordHistory>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AccountFee>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AccountHold>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AccountRestriction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AccountStatusHistory>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FeeSchedule>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<JointAccountHolder>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<RecurringPayment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<RecurringPaymentExecution>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PaymentTemplate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Beneficiary>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AccountStatement>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<StatementTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Loan>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LoanPayment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LoanDocument>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<LoanStatusHistory>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Card>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CardTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CardAuthorization>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CardStatement>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CardStatusHistory>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<NotificationPreference>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Biller>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BillPayment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BillPresentment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PaymentReceipt>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PaymentRetry>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BillerHealthCheck>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DepositProduct>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<InterestTier>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FixedDeposit>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DepositTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DepositCertificate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<MaturityNotice>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        // Also set audit on Identity User
        foreach (var entry in ChangeTracker.Entries<User>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}