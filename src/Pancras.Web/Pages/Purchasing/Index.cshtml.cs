using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Application.Purchasing;
using Pancras.Application.Suppliers;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;
using Pancras.Web.Formatting;

namespace Pancras.Web.Pages.Purchasing;

[Authorize(Roles = EmployeeRole.Admin + "," + EmployeeRole.Director + "," + EmployeeRole.Accountant)]
[RequestSizeLimit(12 * 1024 * 1024)]
public class IndexModel(
    ListPurchaseOrdersHandler listOrders,
    GetPurchaseOrderHandler getOrder,
    CreatePurchaseOrderCommandHandler createOrder,
    UpdatePurchaseOrderCommandHandler updateOrder,
    DeletePurchaseChargeCommandHandler deleteCharge,
    ListPurchaseAttachmentsHandler listAttachments,
    UploadPurchaseAttachmentCommandHandler uploadAttachment,
    OpenPurchaseAttachmentHandler openAttachment,
    DeletePurchaseAttachmentCommandHandler deleteAttachment,
    AllocateChargeCommandHandler allocateCharge,
    AssignChargeGroupCommandHandler assignGroup,
    ReceivePurchaseOrderCommandHandler receiveOrder,
    ListSuppliersForPurchaseHandler listSuppliers,
    ListInventoryDocumentsHandler inventory) : PageModel
{
    public IReadOnlyList<PurchaseOrderSummary> Orders { get; private set; } = [];

    public bool HasAnyOrders { get; private set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public PurchaseOrderDetail? Selected { get; private set; }

    public IReadOnlyList<PurchaseAttachmentView> Attachments { get; private set; } = [];

    public IReadOnlyList<SupplierPurchaseOption> Suppliers { get; private set; } = [];

    public IReadOnlyList<StockLocationChoice> Locations { get; private set; } = [];

    public IReadOnlyList<StockItemChoice> Items { get; private set; } = [];

    public bool ShowForm { get; private set; }

    /// <summary>Có giá trị khi form đang sửa một đơn chưa nhập kho.</summary>
    public Guid? EditingId { get; private set; }

    public PurchaseLineView? FormulaLine { get; private set; }

    public string? Notice { get; private set; }

    public string? ErrorMessage { get; private set; }

    [BindProperty]
    public PurchaseForm Form { get; set; } = PurchaseForm.Create();

    [BindProperty]
    public AllocateChargeCommand ChargeCommand { get; set; } = new();

    [BindProperty]
    public List<Guid> ManualLineIds { get; set; } = [];

    [BindProperty]
    public List<decimal> ManualAmounts { get; set; } = [];

    [BindProperty]
    public AssignChargeGroupCommand GroupCommand { get; set; } = new();

    public async Task OnGetAsync(Guid? id, Guid? order, Guid? formula, string? form, string? notice, string? error, Guid? edit)
    {
        ShowForm = string.Equals(form, "1", StringComparison.Ordinal);
        Notice = notice switch
        {
            "created" => "Đã tạo đơn nhập.",
            "received" => "Đã nhập kho.",
            "updated" => "Đã lưu thay đổi.",
            "charge-deleted" => "Đã xóa khoản chi phí.",
            "attachment-added" => "Đã đính kèm chứng từ.",
            "attachment-deleted" => "Đã xóa chứng từ.",
            _ => null
        };
        if (!string.IsNullOrEmpty(error) && Enum.TryParse<PurchaseError>(error, out var parsed) && parsed != PurchaseError.None)
        {
            ErrorMessage = PurchaseErrorMessages.For(parsed);
        }

        var selected = order is Guid orderId && orderId != Guid.Empty ? orderId : id;
        if (edit is Guid editId && editId != Guid.Empty)
        {
            selected = editId;
        }

        await LoadAsync(selected, formula);
        if (edit is not null && Selected is { Status: PurchaseStatus.Open } editing && editing.Id == edit)
        {
            ShowForm = true;
            EditingId = editing.Id;
            FillFormFrom(editing);
        }
    }

    private void FillFormFrom(PurchaseOrderDetail detail)
    {
        Form = new PurchaseForm
        {
            Code = detail.Code,
            SupplierId = detail.SupplierId,
            ReceiveLocationId = detail.ReceiveLocationId,
            ContainerNumber = detail.ContainerNumber,
            BillOfLading = detail.BillOfLading,
            Port = detail.Port,
            ExpectedOn = detail.ExpectedOn,
            ExchangeRate = decimal.Truncate(detail.ExchangeRate),
            VatPercent = Tidy(detail.VatRate * 100m),
            VatDeductible = detail.VatDeductible,
            Lines = detail.Lines
                .Select(line => new CreatePurchaseLineInput
                {
                    LineId = line.Id,
                    CatalogItemId = line.CatalogItemId,
                    Quantity = line.Quantity,
                    SupplementalDuty = line.SupplementalDuty + line.SpecialConsumptionTax + line.EnvironmentTax
                })
                .ToList(),
            UnitPrices = detail.Lines.Select(line => Tidy(line.Quantity == 0 ? line.FobUsd : line.FobUsd / line.Quantity)).ToList(),
            DutyPercents = detail.Lines.Select(line => Tidy(line.DutyRate * 100m)).ToList()
        };
    }

    /// <summary>Làm tròn 4 số lẻ để hiện trong ô nhập.</summary>
    private static decimal Tidy(decimal value) => decimal.Round(value, 4);

    private void ApplyLineInputs()
    {
        var lines = Form.Lines ?? [];
        for (var index = 0; index < lines.Count; index++)
        {
            if (index < Form.UnitPrices.Count)
            {
                lines[index].FobUsd = Form.UnitPrices[index] * lines[index].Quantity;
            }

            if (index < Form.DutyPercents.Count)
            {
                lines[index].DutyRate = Form.DutyPercents[index] / 100m;
            }
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid orderId)
    {
        ApplyLineInputs();
        var result = await updateOrder.HandleAsync(
            new UpdatePurchaseOrderCommand
            {
                PurchaseOrderId = orderId,
                Code = Form.Code,
                SupplierId = Form.SupplierId,
                ReceiveLocationId = Form.ReceiveLocationId,
                ContainerNumber = Form.ContainerNumber,
                BillOfLading = Form.BillOfLading,
                Port = Form.Port,
                ExpectedOn = Form.ExpectedOn,
                ExchangeRate = Form.ExchangeRate,
                VatRate = Form.VatPercent / 100m,
                VatDeductible = Form.VatDeductible,
                Lines = Form.Lines ?? []
            },
            HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            ShowForm = true;
            EditingId = orderId;
            ErrorMessage = PurchaseErrorMessages.For(result.PurchaseError);
            KeepDraftLines();
            await LoadAsync(orderId, null);
            return Page();
        }

        return Redirect($"/purchasing?order={orderId}&notice=updated");
    }

    public async Task<IActionResult> OnPostUploadAsync(Guid orderId, AttachmentKind kind, IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Redirect($"/purchasing?order={orderId}&error={PurchaseError.AttachmentInvalid}");
        }

        await using var content = file.OpenReadStream();
        var result = await uploadAttachment.HandleAsync(
            new UploadPurchaseAttachmentCommand { PurchaseOrderId = orderId, Kind = kind, FileName = file.FileName, Content = content },
            HttpContext.RequestAborted);
        return result.IsFailure
            ? Redirect($"/purchasing?order={orderId}&error={result.PurchaseError}")
            : Redirect($"/purchasing?order={orderId}&notice=attachment-added");
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid id)
    {
        var result = await openAttachment.HandleAsync(id, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var download = result.Value!;
        Response.Headers.CacheControl = "private, no-store";
        // Luôn tải về (attachment), không mở trong trình duyệt, và nội dung do hệ thống gán kiểu, không tin tên file.
        return File(download.Content, download.ContentType, download.FileName);
    }

    public async Task<IActionResult> OnPostDeleteAttachmentAsync(Guid orderId, Guid attachmentId)
    {
        var result = await deleteAttachment.HandleAsync(attachmentId, HttpContext.RequestAborted);
        return result.IsFailure
            ? Redirect($"/purchasing?order={orderId}&error={result.PurchaseError}")
            : Redirect($"/purchasing?order={orderId}&notice=attachment-deleted");
    }

    public async Task<IActionResult> OnPostDeleteChargeAsync(Guid orderId, Guid chargeId)
    {
        var result = await deleteCharge.HandleAsync(new DeletePurchaseChargeCommand(orderId, chargeId), HttpContext.RequestAborted);
        return result.IsFailure
            ? Redirect($"/purchasing?order={orderId}&error={result.PurchaseError}")
            : Redirect($"/purchasing?order={orderId}&notice=charge-deleted");
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        ApplyLineInputs();
        var command = new CreatePurchaseOrderCommand
        {
            Code = Form.Code,
            SupplierId = Form.SupplierId,
            ReceiveLocationId = Form.ReceiveLocationId,
            ContainerNumber = Form.ContainerNumber,
            BillOfLading = Form.BillOfLading,
            Port = Form.Port,
            ExpectedOn = Form.ExpectedOn,
            ExchangeRate = Form.ExchangeRate,
            VatRate = Form.VatPercent / 100m,
            VatDeductible = Form.VatDeductible,
            Lines = Form.Lines ?? []
        };
        var result = await createOrder.HandleAsync(command, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            ShowForm = true;
            ErrorMessage = PurchaseErrorMessages.For(result.PurchaseError);
            KeepDraftLines();
            await LoadAsync(null, null);
            return Page();
        }

        // Cước biển nằm trong trị giá tính thuế (mua FOB); các khoản sau thông quan là logistics.
        var allAdded = true;
        allAdded &= await AddInitialChargeAsync(result.Value, Form.OceanFreight, "Cước vận chuyển đường biển quốc tế (Ocean Freight)", ChargeGroup.CustomsValueAdjustment, true);
        allAdded &= await AddInitialChargeAsync(result.Value, Form.PortHandling, "Phí nâng hạ bãi cảng (THC, D/O, vệ sinh container)", ChargeGroup.Logistics, true);
        allAdded &= await AddInitialChargeAsync(result.Value, Form.InspectionFee, "Phí kiểm định chất lượng sản phẩm", ChargeGroup.Logistics, false);
        allAdded &= await AddInitialChargeAsync(result.Value, Form.InlandTrucking, "Vận chuyển kéo xe container từ cảng về kho", ChargeGroup.Logistics, true);

        // Không báo "đã tạo đơn" khi có khoản chi phí bị bỏ sót: giá vốn sẽ thiếu mà người dùng không biết.
        return allAdded
            ? Redirect($"/purchasing?order={result.Value}&notice=created")
            : Redirect($"/purchasing?order={result.Value}&error={PurchaseError.ChargeFailed}");
    }

    private async Task<bool> AddInitialChargeAsync(Guid orderId, decimal amount, string label, ChargeGroup group, bool preferVolume)
    {
        if (amount <= 0)
        {
            return true;
        }

        // Chia theo thể tích nếu mọi mặt hàng đã khai báo; chưa đủ số đo thì chia theo trị giá FOB.
        var methods = preferVolume ? new[] { ChargeSplitMethod.Volume, ChargeSplitMethod.FobValue } : new[] { ChargeSplitMethod.FobValue };
        foreach (var method in methods)
        {
            var allocated = await allocateCharge.HandleAsync(
                new AllocateChargeCommand { PurchaseOrderId = orderId, Amount = amount, Method = method, Label = label, Group = group },
                HttpContext.RequestAborted);
            if (allocated.IsSuccess)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<IActionResult> OnPostReceiveAsync(Guid orderId)
    {
        var result = await receiveOrder.HandleAsync(new ReceivePurchaseOrderCommand(orderId), HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return Redirect($"/purchasing?id={orderId}&error={result.PurchaseError}");
        }

        return Redirect($"/purchasing?order={orderId}&notice=received");
    }

    public async Task<IActionResult> OnPostSaveChargeAsync()
    {
        if (ChargeCommand.Method == ChargeSplitMethod.Manual)
        {
            if (ManualLineIds.Count != ManualAmounts.Count)
            {
                return Redirect($"/purchasing?order={ChargeCommand.PurchaseOrderId}&error={PurchaseError.InvalidOrder}");
            }

            var shares = new Dictionary<Guid, decimal>();
            for (var index = 0; index < ManualLineIds.Count; index++)
            {
                shares[ManualLineIds[index]] = ManualAmounts[index];
            }

            ChargeCommand.ManualShares = shares;
        }

        var result = await allocateCharge.HandleAsync(ChargeCommand, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return Redirect($"/purchasing?order={ChargeCommand.PurchaseOrderId}&error={result.PurchaseError}");
        }

        return Redirect($"/purchasing?order={ChargeCommand.PurchaseOrderId}");
    }

    public async Task<IActionResult> OnPostAssignGroupAsync()
    {
        var result = await assignGroup.HandleAsync(GroupCommand, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return Redirect($"/purchasing?order={GroupCommand.PurchaseOrderId}&error={result.PurchaseError}");
        }

        return Redirect($"/purchasing?order={GroupCommand.PurchaseOrderId}");
    }

    public async Task<IActionResult> OnPostExportAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            return Redirect("/purchasing");
        }

        var detail = await getOrder.HandleAsync(orderId, HttpContext.RequestAborted);
        if (detail is null)
        {
            return Redirect("/purchasing");
        }

        return File(PurchaseCsv.Build(detail), "text/csv", "don-mua.csv");
    }

    private void KeepDraftLines()
    {
        Form.Lines ??= [];
        Form.UnitPrices ??= [];
        Form.DutyPercents ??= [];
        if (Form.Lines.Count > 0)
        {
            return;
        }

        Form.Lines.Add(new CreatePurchaseLineInput());
        Form.UnitPrices.Add(0m);
        Form.DutyPercents.Add(0m);
    }

    private async Task LoadAsync(Guid? id, Guid? formula)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var allOrders = await listOrders.HandleAsync(cancellationToken);
        HasAnyOrders = allOrders.Count > 0;
        var term = Search?.Trim();
        Orders = string.IsNullOrEmpty(term)
            ? allOrders
            : allOrders.Where(order =>
                order.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                || order.SupplierName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || order.ContainerNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || order.BillOfLading.Contains(term, StringComparison.OrdinalIgnoreCase)
                || order.Port.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
        Suppliers = await listSuppliers.HandleAsync(cancellationToken);
        Locations = await inventory.ListAllLocationsAsync(cancellationToken);
        Items = await inventory.ListItemsAsync(cancellationToken);
        if ((id is null || id == Guid.Empty) && Orders.Count > 0)
        {
            id = Orders[0].Id;
        }

        if (id is Guid selected && selected != Guid.Empty)
        {
            Selected = await getOrder.HandleAsync(selected, cancellationToken);
            if (Selected is not null)
            {
                Attachments = await listAttachments.HandleAsync(Selected.Id, cancellationToken);
            }

            if (Selected is not null && formula is Guid formulaId)
            {
                FormulaLine = Selected.Lines.FirstOrDefault(line => line.Id == formulaId);
            }
        }
    }
}

public sealed class PurchaseForm
{
    public string? Code { get; set; }

    public Guid SupplierId { get; set; }

    public Guid ReceiveLocationId { get; set; }

    public string? ContainerNumber { get; set; }

    public string? BillOfLading { get; set; }

    public string? Port { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    public decimal ExchangeRate { get; set; }

    public decimal VatPercent { get; set; }

    public bool VatDeductible { get; set; }

    public decimal OceanFreight { get; set; }

    public decimal PortHandling { get; set; }

    public decimal InspectionFee { get; set; }

    public decimal InlandTrucking { get; set; }

    public List<decimal> UnitPrices { get; set; } = [];

    public List<decimal> DutyPercents { get; set; } = [];

    public List<CreatePurchaseLineInput> Lines { get; set; } = [];

    public static PurchaseForm Create() => new()
    {
        VatPercent = 10m,
        Lines = [new CreatePurchaseLineInput(), new CreatePurchaseLineInput(), new CreatePurchaseLineInput()],
        UnitPrices = [0m, 0m, 0m],
        DutyPercents = [0m, 0m, 0m]
    };
}
