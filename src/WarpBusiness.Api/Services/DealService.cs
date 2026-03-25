using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Domain;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Api.Services;

public class DealService : IDealService
{
    private readonly ApplicationDbContext _db;

    public DealService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<DealDto>> GetDealsAsync(int page, int pageSize, string? search, string? stage, CancellationToken ct = default)
    {
        var query = _db.Deals
            .Include(d => d.Contact)
            .Include(d => d.Company)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(stage) && Enum.TryParse<DealStage>(stage, ignoreCase: true, out var dealStage))
            query = query.Where(d => d.Stage == dealStage);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DealDto(
                d.Id, d.Title, d.Value, d.Currency, d.Stage.ToString(),
                d.Probability, d.ExpectedCloseDate,
                d.ContactId,
                d.Contact != null ? d.Contact.FirstName + " " + d.Contact.LastName : null,
                d.CompanyId,
                d.Company != null ? d.Company.Name : null,
                d.OwnerId, d.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<DealDto>(items, totalCount, page, pageSize);
    }

    public async Task<DealDto?> GetDealByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Deals
            .Include(d => d.Contact)
            .Include(d => d.Company)
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DealDto(
                d.Id, d.Title, d.Value, d.Currency, d.Stage.ToString(),
                d.Probability, d.ExpectedCloseDate,
                d.ContactId,
                d.Contact != null ? d.Contact.FirstName + " " + d.Contact.LastName : null,
                d.CompanyId,
                d.Company != null ? d.Company.Name : null,
                d.OwnerId, d.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DealDto> CreateDealAsync(CreateDealRequest request, string userId, CancellationToken ct = default)
    {
        var deal = new Deal
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Value = request.Value,
            Currency = request.Currency,
            Stage = Enum.Parse<DealStage>(request.Stage),
            Probability = request.Probability,
            ExpectedCloseDate = request.ExpectedCloseDate,
            ContactId = request.ContactId,
            CompanyId = request.CompanyId,
            OwnerId = userId,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(ct);

        return (await GetDealByIdAsync(deal.Id, ct))!;
    }

    public async Task<DealDto?> UpdateDealAsync(Guid id, UpdateDealRequest request, CancellationToken ct = default)
    {
        var deal = await _db.Deals.FindAsync([id], ct);
        if (deal is null) return null;

        deal.Title = request.Title;
        deal.Value = request.Value;
        deal.Currency = request.Currency;
        deal.Stage = Enum.Parse<DealStage>(request.Stage);
        deal.Probability = request.Probability;
        deal.ExpectedCloseDate = request.ExpectedCloseDate;
        deal.ContactId = request.ContactId;
        deal.CompanyId = request.CompanyId;
        deal.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetDealByIdAsync(id, ct);
    }

    public async Task<bool> DeleteDealAsync(Guid id, CancellationToken ct = default)
    {
        var deal = await _db.Deals.FindAsync([id], ct);
        if (deal is null) return false;
        _db.Deals.Remove(deal);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DealPipelineSummary> GetDealSummaryAsync(CancellationToken ct = default)
    {
        var stageSummaries = await _db.Deals
            .AsNoTracking()
            .GroupBy(d => d.Stage)
            .Select(g => new DealStageSummary(g.Key.ToString(), g.Count(), g.Sum(d => d.Value)))
            .ToListAsync(ct);

        var totalPipelineValue = stageSummaries
            .Where(s => s.Stage != DealStage.ClosedLost.ToString())
            .Sum(s => s.TotalValue);

        var totalDealCount = stageSummaries.Sum(s => s.Count);

        return new DealPipelineSummary(stageSummaries, totalPipelineValue, totalDealCount);
    }
}
