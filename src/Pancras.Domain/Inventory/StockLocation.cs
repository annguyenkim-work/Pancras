namespace Pancras.Domain.Inventory;

public enum StockLocationKind
{
    Warehouse = 1,
    Showroom = 2
}

public sealed class StockLocation
{
    public const int NameMaxLength = 200;

    private StockLocation()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public StockLocationKind Kind { get; private set; }

    public static StockLocation Create(string name, StockLocationKind kind)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > NameMaxLength)
        {
            throw new ArgumentException("Name is missing or too long.", nameof(name));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return new StockLocation
        {
            Id = Guid.CreateVersion7(),
            Name = trimmed,
            Kind = kind
        };
    }
}
