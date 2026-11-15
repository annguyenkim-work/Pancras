namespace Pancras.Domain.Auditing;

/// <summary>Một dòng nhật ký thao tác. Không ghi mật khẩu, mã 2FA hay khóa bí mật.</summary>
public sealed class AuditEntry
{
    public const int DetailMaxLength = 500;

    private AuditEntry()
    {
        Action = string.Empty;
        ActorCode = string.Empty;
        TargetType = string.Empty;
        TargetId = string.Empty;
        Detail = string.Empty;
        IpAddress = string.Empty;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? ActorEmployeeId { get; private set; }

    public string ActorCode { get; private set; }

    public string Action { get; private set; }

    public string TargetType { get; private set; }

    public string TargetId { get; private set; }

    public string Detail { get; private set; }

    public string IpAddress { get; private set; }

    public static AuditEntry Create(
        DateTimeOffset occurredAt,
        Guid? actorEmployeeId,
        string? actorCode,
        string action,
        string? targetType = null,
        string? targetId = null,
        string? detail = null,
        string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action is required.", nameof(action));
        }

        return new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = occurredAt,
            ActorEmployeeId = actorEmployeeId,
            ActorCode = Fit(actorCode, 220),
            Action = Fit(action, 60),
            TargetType = Fit(targetType, 60),
            TargetId = Fit(targetId, 80),
            Detail = Fit(detail, DetailMaxLength),
            IpAddress = Fit(ipAddress, 64)
        };
    }

    private static string Fit(string? value, int max)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

public static class AuditActions
{
    public const string AccessDenied = "access.denied";
    public const string LoginSucceeded = "login.succeeded";
    public const string LoginFailed = "login.failed";
    public const string LoginLockedOut = "login.locked_out";
    public const string TwoFactorFailed = "2fa.failed";
    public const string TwoFactorEnabled = "2fa.enabled";
    public const string TwoFactorDisabled = "2fa.disabled";
    public const string TwoFactorReset = "2fa.reset_by_admin";
    public const string RecoveryCodeUsed = "2fa.recovery_code_used";
    public const string PasswordChanged = "password.changed";
    public const string PasswordReset = "password.reset_by_admin";
    public const string EmployeeCreated = "employee.created";
    public const string EmployeeUpdated = "employee.updated";
    public const string EmployeeRoleChanged = "employee.role_changed";
    public const string EmployeeActivated = "employee.activated";
    public const string EmployeeDeactivated = "employee.deactivated";
    public const string EmployeeUnlocked = "employee.unlocked";
    public const string LocationCreated = "location.created";
    public const string LocationUpdated = "location.updated";
    public const string LocationDeleted = "location.deleted";
    public const string TransferRequested = "stock.transfer_requested";
    public const string TransferUpdated = "stock.transfer_updated";
    public const string TransferApproved = "stock.transfer_approved";
    public const string TransferRejected = "stock.transfer_rejected";
    public const string TransferDiscrepancyResolved = "stock.transfer_discrepancy_resolved";
    public const string TransferAttachmentAdded = "stock.transfer_attachment_added";
    public const string TransferAttachmentDownloaded = "stock.transfer_attachment_downloaded";
    public const string StockDocumentAttachmentAdded = "stock.document_attachment_added";
    public const string StockDocumentAttachmentDownloaded = "stock.document_attachment_downloaded";
    public const string TransferShipped = "stock.transfer_shipped";
    public const string TransferReceived = "stock.transfer_received";
    public const string TransferCancelled = "stock.transfer_cancelled";
    public const string TransferReturned = "stock.transfer_returned";
    public const string StocktakeApproved = "stock.stocktake_approved";
    public const string AdjustmentApproved = "stock.adjustment_approved";
    public const string DisposalApproved = "stock.disposal_approved";
    public const string PricesChanged = "catalog.prices_changed";
    public const string PurchaseCreated = "purchase.created";
    public const string PurchaseExported = "purchase.exported";
    public const string PurchaseReceived = "purchase.received";
    public const string PurchaseUpdated = "purchase.updated";
    public const string PurchaseStatusChanged = "purchase.status_changed";
    public const string PurchaseCostAdjusted = "purchase.cost_adjusted";
    public const string PurchaseChargeAdded = "purchase.charge_added";
    public const string PurchaseChargeDeleted = "purchase.charge_deleted";
    public const string PurchaseAttachmentAdded = "purchase.attachment_added";
    public const string PurchaseAttachmentDownloaded = "purchase.attachment_downloaded";
    public const string PurchaseAttachmentDeleted = "purchase.attachment_deleted";
    public const string SupplierCreated = "supplier.created";
    public const string SupplierUpdated = "supplier.updated";
    public const string SupplierDeactivated = "supplier.deactivated";
    public const string SupplierReactivated = "supplier.reactivated";
}
