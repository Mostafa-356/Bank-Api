using Bank.Application.DTOs;
using Bank.Application.DTOs.Payment.Biller;
using Bank.Application.Interfaces;
using Bank.Domain.Common;
using Bank.Domain.Entities;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AutoMapper;
using DomainBillPresentmentStatus = Bank.Domain.Enums.BillPresentmentStatus;

namespace Bank.Application.Services;

/// <summary>
/// Service implementation for bill presentment operations
/// </summary>
public class BillPresentmentService : IBillPresentmentService
{
    private readonly IBillPresentmentRepository _billPresentmentRepository;
    private readonly IBillerRepository _billerRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BillPresentmentService> _logger;
    private readonly IMapper _mapper;

    public BillPresentmentService(
        IBillPresentmentRepository billPresentmentRepository,
        IBillerRepository billerRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<BillPresentmentService> logger,
        IMapper mapper)
    {
        _billPresentmentRepository = billPresentmentRepository;
        _billerRepository = billerRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<List<BillPresentmentDto>> GetCustomerBillPresentmentsAsync(Guid customerId, DomainBillPresentmentStatus? status = null)
    {
        try
        {
            var presentments = await _billPresentmentRepository.GetCustomerBillPresentmentsAsync(customerId, status);
            return presentments.Select(MapToBillPresentmentDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bill presentments for customer {CustomerId}", customerId);
            return new List<BillPresentmentDto>();
        }
    }

    public async Task<List<BillPresentmentDto>> GetBillPresentmentsByBillerAsync(Guid billerId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var presentments = await _billPresentmentRepository.GetBillPresentmentsByBillerAsync(billerId, fromDate, toDate);
            return presentments.Select(MapToBillPresentmentDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bill presentments for biller {BillerId}", billerId);
            return new List<BillPresentmentDto>();
        }
    }

    public async Task<List<BillPresentmentDto>> GetOverdueBillPresentmentsAsync()
    {
        try
        {
            var presentments = await _billPresentmentRepository.GetOverdueBillPresentmentsAsync();
            return presentments.Select(MapToBillPresentmentDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue bill presentments");
            return new List<BillPresentmentDto>();
        }
    }

    public async Task<List<BillPresentmentDto>> GetBillPresentmentsDueWithinDaysAsync(int days)
    {
        try
        {
            var presentments = await _billPresentmentRepository.GetBillPresentmentsDueWithinDaysAsync(days);
            return presentments.Select(MapToBillPresentmentDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bill presentments due within {Days} days", days);
            return new List<BillPresentmentDto>();
        }
    }

    public async Task<BillPresentmentDto> CreateBillPresentmentAsync(CreateBillPresentmentRequest request)
    {
        try
        {
            // Validate customer and biller exist
            var customer = await _userRepository.GetByIdAsync(request.CustomerId);
            if (customer == null)
            {
                throw new InvalidOperationException($"Customer {request.CustomerId} not found");
            }

            var biller = await _billerRepository.GetByIdAsync(request.BillerId);
            if (biller == null)
            {
                throw new InvalidOperationException($"Biller {request.BillerId} not found");
            }

            // Check if bill presentment already exists for this external bill ID
            var existingPresentment = await _billPresentmentRepository.GetByExternalBillIdAsync(request.ExternalBillId);
            if (existingPresentment != null)
            {
                throw new InvalidOperationException($"Bill presentment already exists for external bill ID {request.ExternalBillId}");
            }

            // Create bill presentment
            var presentment = new BillPresentment
            {
                CustomerId = request.CustomerId,
                BillerId = request.BillerId,
                AccountNumber = request.AccountNumber,
                AmountDue = request.AmountDue,
                MinimumPayment = request.MinimumPayment,
                DueDate = request.DueDate,
                StatementDate = request.StatementDate,
                BillNumber = request.BillNumber,
                ExternalBillId = request.ExternalBillId,
                Status = DomainBillPresentmentStatus.Presented,
                LineItemsJson = JsonSerializer.Serialize(request.LineItems)
            };

            await _billPresentmentRepository.AddAsync(presentment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Bill presentment created: {PresentmentId} for customer {CustomerId} and biller {BillerId}",
                presentment.Id, request.CustomerId, request.BillerId);

            // Load related entities for DTO mapping
            presentment.Customer = customer;
            presentment.Biller = biller;

            return MapToBillPresentmentDto(presentment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bill presentment");
            throw;
        }
    }


    public async Task<bool> UpdateBillPresentmentStatusAsync(Guid presentmentId, DomainBillPresentmentStatus status)
    {
        try
        {
            var presentment = await _billPresentmentRepository.GetByIdAsync(presentmentId);
            if (presentment == null)
            {
                return false;
            }

            presentment.Status = status;
            _billPresentmentRepository.Update(presentment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Bill presentment {PresentmentId} status updated to {Status}",
                presentmentId, status);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bill presentment status for {PresentmentId}", presentmentId);
            return false;
        }
    }

    public async Task<bool> MarkBillPresentmentAsPaidAsync(Guid presentmentId, Guid paymentId)
    {
        try
        {
            var presentment = await _billPresentmentRepository.GetByIdAsync(presentmentId);
            if (presentment == null)
            {
                return false;
            }

            if (!presentment.CanBePaid())
            {
                return false;
            }

            presentment.MarkAsPaid(paymentId);
            _billPresentmentRepository.Update(presentment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Bill presentment {PresentmentId} marked as paid with payment {PaymentId}",
                presentmentId, paymentId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking bill presentment {PresentmentId} as paid", presentmentId);
            return false;
        }
    }

    public async Task<bool> CancelBillPresentmentAsync(Guid presentmentId)
    {
        try
        {
            var presentment = await _billPresentmentRepository.GetByIdAsync(presentmentId);
            if (presentment == null)
            {
                return false;
            }

            presentment.Cancel();
            _billPresentmentRepository.Update(presentment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Bill presentment {PresentmentId} cancelled", presentmentId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling bill presentment {PresentmentId}", presentmentId);
            return false;
        }
    }

    public async Task<BillPresentmentDto?> GetBillPresentmentByIdAsync(Guid presentmentId)
    {
        try
        {
            var presentment = await _billPresentmentRepository.GetBillPresentmentWithDetailsAsync(presentmentId);
            return presentment != null ? MapToBillPresentmentDto(presentment) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bill presentment {PresentmentId}", presentmentId);
            return null;
        }
    }

    public async Task<int> ProcessOverdueBillPresentmentsAsync()
    {
        try
        {
            var overduePresentments = await _billPresentmentRepository.GetOverdueBillPresentmentsAsync();
            var processedCount = 0;

            foreach (var presentment in overduePresentments)
            {
                if (presentment.Status == DomainBillPresentmentStatus.Presented && presentment.IsOverdue())
                {
                    presentment.MarkAsOverdue();
                    _billPresentmentRepository.Update(presentment);
                    processedCount++;
                }
            }

            if (processedCount > 0)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Processed {Count} overdue bill presentments", processedCount);
            }

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing overdue bill presentments");
            return 0;
        }
    }

    public async Task<List<BillPresentmentSyncResult>> SynchronizeBillPresentmentsAsync(Guid customerId, Guid billerId)
    {
        var results = new List<BillPresentmentSyncResult>();

        try
        {
            // In a real implementation, this would call external biller APIs to get latest bill data
            // For now, we'll simulate the synchronization process
            
            var existingPresentments = await _billPresentmentRepository.GetUnpaidBillPresentmentsAsync(customerId, billerId);
            
            foreach (var presentment in existingPresentments)
            {
                try
                {
                    // Simulate external API call to check bill status
                    await Task.Delay(100); // Simulate network delay

                    // Mock synchronization result
                    var syncResult = new BillPresentmentSyncResult
                    {
                        ExternalBillId = presentment.ExternalBillId, Success = true,
                        Status = presentment.Status,
                        SyncDate = DateTime.UtcNow,
                        Message = "Synchronization successful"
                    };

                    results.Add(syncResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error synchronizing bill presentment {ExternalBillId}", presentment.ExternalBillId);
                    
                    results.Add(new BillPresentmentSyncResult
                    {
                        ExternalBillId = presentment.ExternalBillId, Success = false,
                        SyncDate = DateTime.UtcNow,
                        Message = $"Synchronization failed: {ex.Message}"
                    });
                }
            }

            _logger.LogInformation("Synchronized {Count} bill presentments for customer {CustomerId} and biller {BillerId}",
                results.Count, customerId, billerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing bill presentments for customer {CustomerId} and biller {BillerId}",
                customerId, billerId);
        }

        return results;
    }

    #region Private Helper Methods

    private BillPresentmentDto MapToBillPresentmentDto(BillPresentment presentment)
    {
        return _mapper.Map<BillPresentmentDto>(presentment);
    }

    #endregion
}