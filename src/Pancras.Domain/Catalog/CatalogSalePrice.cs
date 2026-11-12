namespace Pancras.Domain.Catalog;

public enum SaleCustomerKind
{
    Retail = 1,
    Dealer = 2,
    Distributor = 3,
    Project = 4
}

public readonly record struct SalePriceRequest(SaleCustomerKind Customer, bool FromQuoteFile, decimal? FilePrice);

public enum SalePriceError
{
    None = 0,
    Undecided,
    MissingFilePrice,
    NegativeFilePrice
}

public static class CatalogSalePrice
{
    public static bool TryResolve(
        CatalogItem item,
        SalePriceRequest request,
        out decimal price,
        out SalePriceError error)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (request.FromQuoteFile)
        {
            if (request.FilePrice is null)
            {
                price = 0;
                error = SalePriceError.MissingFilePrice;
                return false;
            }

            if (request.FilePrice.Value < 0)
            {
                price = 0;
                error = SalePriceError.NegativeFilePrice;
                return false;
            }

            price = decimal.Round(request.FilePrice.Value, 0, MidpointRounding.AwayFromZero);
            error = SalePriceError.None;
            return true;
        }

        if (request.Customer == SaleCustomerKind.Retail)
        {
            price = item.RetailPrice;
            error = SalePriceError.None;
            return true;
        }

        if (request.Customer == SaleCustomerKind.Dealer)
        {
            price = item.DealerPrice;
            error = SalePriceError.None;
            return true;
        }

        price = 0;
        error = SalePriceError.Undecided;
        return false;
    }
}
