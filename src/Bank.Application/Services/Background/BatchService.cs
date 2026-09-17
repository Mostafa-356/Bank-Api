using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Bank.Application.Services;

public class BatchService : IBatchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransactionService _transactionService;

    public BatchService(IUnitOfWork unitOfWork, ITransactionService transactionService)
    {
        _unitOfWork = unitOfWork;
        _transactionService = transactionService;
    }

    public async Task<BatchJob> CreateBatchJobAsync(string fileName, int totalRecords)
    {
        var job = new BatchJob
        {
            FileName = fileName,
            TotalRecords = totalRecords,
            Status = BatchJobStatus.Pending
        };

        await _unitOfWork.Repository<BatchJob>().AddAsync(job);
        await _unitOfWork.SaveChangesAsync();
        return job;
    }

    public async Task ProcessBatchAsync(Guid jobId, IEnumerable<TransactionRequest> transactions)
    {
        var job = await _unitOfWork.Repository<BatchJob>().GetByIdAsync(jobId);
        if (job == null) return;

        job.Status = BatchJobStatus.Processing;
        _unitOfWork.Repository<BatchJob>().Update(job);
        await _unitOfWork.SaveChangesAsync();

        foreach (var req in transactions)
        {
            try
            {
                await _transactionService.InitiateTransactionAsync(
                    req.FromAccountId, req.ToAccountId, req.Amount, req.Type, req.Description);
                job.SuccessCount++;
            }
            catch
            {
                job.FailureCount++;
            }

            _unitOfWork.Repository<BatchJob>().Update(job);
            await _unitOfWork.SaveChangesAsync();
        }

        job.Status = BatchJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        _unitOfWork.Repository<BatchJob>().Update(job);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<BatchJob?> GetBatchJobStatusAsync(Guid jobId)
    {
        return await _unitOfWork.Repository<BatchJob>().Query()
            .Include(j => j.Transactions)
            .FirstOrDefaultAsync(j => j.Id == jobId);
    }

    public async Task<IEnumerable<BatchJob>> GetAllBatchJobsAsync()
    {
        return await _unitOfWork.Repository<BatchJob>().Query()
            .OrderByDescending(j => j.CreatedAt)
            .Take(50) // Limit to recent 50 for performance
            .ToListAsync();
    }
}
