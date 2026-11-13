namespace Pancras.Domain.Inventory;

public enum StockDecision
{
    Accepted = 0,
    Rejected,
    WrongRole,
    MissingEvidence,
    NegativeStock,
    NotPending
}
