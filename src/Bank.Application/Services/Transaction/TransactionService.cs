using Bank.Application.DTOs;
using Bank.Application.DTOs.Transaction.Core;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Bank.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Transaction> InitiateTransactionAsync(Guid fromAccountId, Guid toAccountId, decimal amount, TransactionType type, string description)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var fromAccount = await _unitOfWork.Repository<Account>().GetByIdAsync(fromAccountId) 
                ?? throw new Exception("Sender account not found.");
            var toAccount = await _unitOfWork.Repository<Account>().GetByIdAsync(toAccountId) 
                ?? throw new Exception("Recipient account not found.");

            if (fromAccount.Balance < amount)
                throw new Exception("Insufficient funds.");

            var transaction = new Transaction
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount,
                Type = type,
                Description = description,
                Status = TransactionStatus.Processing
            };

            // RTGS = instant settlement, ACH/WPS = deferred
            if (type == TransactionType.RTGS)
            {
                fromAccount.Balance -= amount;
                toAccount.Balance += amount;
                transaction.Status = TransactionStatus.Completed;
                transaction.ProcessedAt = DateTime.UtcNow;
                _unitOfWork.Repository<Account>().Update(fromAccount);
                _unitOfWork.Repository<Account>().Update(toAccount);
            }
            else
            {
                transaction.Status = TransactionStatus.Pending;
                fromAccount.Balance -= amount;
                _unitOfWork.Repository<Account>().Update(fromAccount);
            }

            await _unitOfWork.Repository<Transaction>().AddAsync(transaction);
            await _unitOfWork.CommitTransactionAsync();

            return transaction;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request)
    {
        return await InitiateTransactionAsync(
            request.FromAccountId,
            request.ToAccountId,
            request.Amount,
            request.Type,
            request.Description);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionHistoryAsync(Guid accountId)
    {
        return await _unitOfWork.Repository<Transaction>().Query()
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id)
    {
        return await _unitOfWork.Repository<Transaction>().Query()
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<(IEnumerable<Transaction> Transactions, int TotalCount)> SearchTransactionsAsync(
        TransactionSearchCriteria criteria, 
        int pageNumber = 1, 
        int pageSize = 50)
    {
        var query = _unitOfWork.Repository<Transaction>().Query();

        // Apply filters
        if (criteria.AccountId.HasValue)
        {
            query = query.Where(t => t.FromAccountId == criteria.AccountId || t.ToAccountId == criteria.AccountId);
        }

        if (criteria.FromAccountId.HasValue)
        {
            query = query.Where(t => t.FromAccountId == criteria.FromAccountId);
        }

        if (criteria.ToAccountId.HasValue)
        {
            query = query.Where(t => t.ToAccountId == criteria.ToAccountId);
        }

        if (criteria.FromDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= criteria.FromDate);
        }

        if (criteria.ToDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= criteria.ToDate);
        }

        if (criteria.Type.HasValue)
        {
            query = query.Where(t => t.Type == criteria.Type);
        }

        if (criteria.Status.HasValue)
        {
            query = query.Where(t => t.Status == criteria.Status);
        }

        if (criteria.MinAmount.HasValue)
        {
            query = query.Where(t => t.Amount >= criteria.MinAmount);
        }

        if (criteria.MaxAmount.HasValue)
        {
            query = query.Where(t => t.Amount <= criteria.MaxAmount);
        }

        if (!string.IsNullOrEmpty(criteria.Description))
        {
            query = query.Where(t => t.Description.Contains(criteria.Description));
        }

        if (!string.IsNullOrEmpty(criteria.Reference))
        {
            query = query.Where(t => t.Reference != null && t.Reference.Contains(criteria.Reference));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination, ordering, and includes
        var transactions = await query
            .Include(t => t.FromAccount)
            .Include(t => t.ToAccount)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (transactions, totalCount);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByDateRangeAsync(
        Guid accountId, 
        DateTime fromDate, 
        DateTime toDate)
    {
        return await _unitOfWork.Repository<Transaction>().Query()
            .Where(t => (t.FromAccountId == accountId || t.ToAccountId == accountId) &&
                       t.CreatedAt >= fromDate && t.CreatedAt <= toDate)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByTypeAsync(
        Guid accountId, 
        TransactionType type)
    {
        return await _unitOfWork.Repository<Transaction>().Query()
            .Where(t => (t.FromAccountId == accountId || t.ToAccountId == accountId) &&
                       t.Type == type)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByAmountRangeAsync(
        Guid accountId, 
        decimal minAmount, 
        decimal maxAmount)
    {
        return await _unitOfWork.Repository<Transaction>().Query()
            .Where(t => (t.FromAccountId == accountId || t.ToAccountId == accountId) &&
                       t.Amount >= minAmount && t.Amount <= maxAmount)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByStatusAsync(
        Guid accountId, 
        TransactionStatus status)
    {
        return await _unitOfWork.Repository<Transaction>().Query()
            .Where(t => (t.FromAccountId == accountId || t.ToAccountId == accountId) &&
                       t.Status == status)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<byte[]> ExportTransactionsToCsvAsync(TransactionSearchCriteria criteria)
    {
        var (transactions, _) = await SearchTransactionsAsync(criteria, 1, int.MaxValue);
        
        var csv = new StringBuilder();
        csv.AppendLine("Date,Reference,Type,Status,From Account,To Account,Amount,Description");

        foreach (var transaction in transactions)
        {
            csv.AppendLine($"{transaction.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{transaction.Reference ?? ""}," +
                          $"{transaction.Type}," +
                          $"{transaction.Status}," +
                          $"{transaction.FromAccount?.AccountNumber ?? ""}," +
                          $"{transaction.ToAccount?.AccountNumber ?? ""}," +
                          $"{transaction.Amount:F2}," +
                          $"\"{transaction.Description}\"");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportTransactionsToExcelAsync(TransactionSearchCriteria criteria)
    {
        // For now, return CSV format. In a real implementation, you would use a library like EPPlus
        // to generate actual Excel files
        return await ExportTransactionsToCsvAsync(criteria);
    }

    public async Task<TransactionStatistics> GetTransactionStatisticsAsync(
        Guid accountId, 
        DateTime fromDate, 
        DateTime toDate)
    {
        var transactions = await _unitOfWork.Repository<Transaction>().Query()
            .Where(t => (t.FromAccountId == accountId || t.ToAccountId == accountId) &&
                       t.CreatedAt >= fromDate && t.CreatedAt <= toDate)
            .ToListAsync();

        var statistics = new TransactionStatistics
        {
            TotalTransactions = transactions.Count,
            TotalAmount = transactions.Sum(t => t.Amount),
            AverageAmount = transactions.Any() ? transactions.Average(t => t.Amount) : 0,
            CreditTransactions = transactions.Count(t => t.ToAccountId == accountId),
            DebitTransactions = transactions.Count(t => t.FromAccountId == accountId),
            TotalCredits = transactions.Where(t => t.ToAccountId == accountId).Sum(t => t.Amount),
            TotalDebits = transactions.Where(t => t.FromAccountId == accountId).Sum(t => t.Amount)
        };

        // Group by transaction type
        statistics.TransactionsByType = transactions
            .GroupBy(t => t.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        // Group by transaction status
        statistics.TransactionsByStatus = transactions
            .GroupBy(t => t.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        return statistics;
    }
}
