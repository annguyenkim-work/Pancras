namespace Pancras.Web.Pages.Shared;

public sealed record EmptyStateModel(string Title, string? Hint = null, string? ActionLabel = null, string? ActionHref = null);
