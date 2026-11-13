using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pancras.Application.Locations;
using Pancras.Domain.Inventory;

namespace Pancras.Infrastructure.Persistence;

public sealed class LocationRepository(PancrasDbContext context) : ILocationRepository
{
    public async Task<IReadOnlyList<StockLocation>> ListAsync(CancellationToken cancellationToken) =>
        await context.StockLocations.AsNoTracking().OrderBy(location => location.Name).ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.StockLocations.AsNoTracking().AnyAsync(location => location.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        context.StockLocations.AsNoTracking()
            .AnyAsync(location => location.Name.ToLower() == name.ToLower(), cancellationToken);

    public async Task<bool> TryAddAsync(StockLocation location, CancellationToken cancellationToken)
    {
        await context.StockLocations.AddAsync(location, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(location).State = EntityState.Detached;
            return false;
        }
    }

}
