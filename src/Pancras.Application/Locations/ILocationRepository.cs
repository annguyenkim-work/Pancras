using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

public interface ILocationRepository
{
    Task<IReadOnlyList<StockLocation>> ListAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken);

    Task AddAsync(StockLocation location, CancellationToken cancellationToken);
}
