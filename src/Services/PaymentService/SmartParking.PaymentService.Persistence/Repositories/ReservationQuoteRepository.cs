using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

public class ReservationQuoteRepository : IReservationQuoteRepository
{
    private readonly PaymentDbContext _dbContext;

    public ReservationQuoteRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReservationQuote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReservationQuotes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(ReservationQuote quote, CancellationToken cancellationToken = default)
    {
        await _dbContext.ReservationQuotes.AddAsync(quote, cancellationToken);
    }

    public void Update(ReservationQuote quote)
    {
        _dbContext.ReservationQuotes.Update(quote);
    }
}
