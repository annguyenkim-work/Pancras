using Microsoft.Extensions.Logging;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Catalog;

public sealed class ResolveSalePriceHandler(
    ICatalogItemRepository repository,
    ILogger<ResolveSalePriceHandler> logger)
{
    public async Task<Result<decimal>> HandleAsync(
        ResolveSalePriceCommand command,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdReadOnlyAsync(command.ItemId, cancellationToken);
        if (item is null)
        {
            logger.LogInformation("Rejected a sale price lookup.");
            return Result<decimal>.Fail(CatalogError.NotFound);
        }

        var request = new SalePriceRequest(command.Customer, command.FromQuoteFile, command.FilePrice);
        if (!CatalogSalePrice.TryResolve(item, request, out var price, out var error))
        {
            logger.LogInformation("Rejected a sale price lookup for {CatalogItemId}.", item.Id);
            return Result<decimal>.Fail(Map(error));
        }

        logger.LogInformation("Resolved a sale price for {CatalogItemId}.", item.Id);
        return Result<decimal>.Ok(price);
    }

    private static CatalogError Map(SalePriceError error) => error switch
    {
        SalePriceError.MissingFilePrice => CatalogError.MissingFilePrice,
        SalePriceError.NegativeFilePrice => CatalogError.Negative,
        _ => CatalogError.UndecidedSalePrice
    };
}
