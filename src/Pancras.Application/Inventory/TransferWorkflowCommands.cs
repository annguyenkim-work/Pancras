using Microsoft.Extensions.Logging;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed record DecideTransferCommand(Guid TransferSlipId, EmployeeActor Actor, bool Approve, string? Reason = null);

public sealed record ShipTransferCommand(Guid TransferSlipId, EmployeeActor Actor, string? SealNumber = null, string? Carrier = null);

public sealed record ReceivedLineInput(Guid LineId, int Quantity);

public sealed record ResolveTransferCommand(Guid TransferSlipId, EmployeeActor Actor, string? Resolution);

public sealed record ReturnTransferCommand(Guid TransferSlipId, EmployeeActor Actor, string? Reason);

public sealed class UpdateTransferRequestCommand
{
    public Guid TransferSlipId { get; set; }

    public Guid SourceLocationId { get; set; }

    public Guid DestinationLocationId { get; set; }

    public string? Note { get; set; }

    public EmployeeActor Actor { get; set; } = new(Guid.Empty, null, null);

    public IReadOnlyList<TransferLineInput> Lines { get; set; } = [];
}
