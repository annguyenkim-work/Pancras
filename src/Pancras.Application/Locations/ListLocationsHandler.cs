using Pancras.Domain.Inventory;

namespace Pancras.Application.Locations;

public sealed class ListLocationsHandler(ILocationRepository repository)
{
    public Task<IReadOnlyList<StockLocation>> HandleAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);
}
