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
public class IndexModel(
    ListPurchaseOrdersHandler listOrders,
    GetPurchaseOrderHandler getOrder,
    CreatePurchaseOrderCommandHandler createOrder,
    UpdatePurchaseOrderCommandHandler updateOrder,
    ListPurchaseAttachmentsHandler listAttachments,
    UploadPurchaseAttachmentCommandHandler uploadAttachment,
    OpenPurchaseAttachmentHandler openAttachment,
    DeletePurchaseAttachmentCommandHandler deleteAttachment,
    ReceivePurchaseOrderCommandHandler receiveOrder,
    ListSuppliersForPurchaseHandler listSuppliers,
    ListInventoryDocumentsHandler inventory,
    Pancras.Application.Catalog.GetCatalogMeasuresHandler catalogMeasures) : PageModel
{
    /// <summary>Cân nặng và thể tích theo mặt hàng, để bản xem thử trong form chia chi phí theo khối lượng/thể tích đúng như máy chủ.</summary>
    public IReadOnlyDictionary<Guid, Pancras.Application.Catalog.CatalogItemMeasure> ItemMeasures { get; private set; } =
        new Dictionary<Guid, Pancras.Application.Catalog.CatalogItemMeasure>();

    private bool NeedMeasures { get; set; }

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

    public async Task OnGetAsync(Guid? id, Guid? order, Guid? formula, string? form, string? notice, string? error, Guid? edit)
    {
        ShowForm = string.Equals(form, "1", StringComparison.Ordinal);
        NeedMeasures = ShowForm || edit is not null;
        Notice = notice switch
        {
            "created" => "Đã tạo đơn nhập.",
            "received" => "Đã nhập kho.",
            "updated" => "Đã lưu thay đổi.",
            "charge-deleted" => "Đã xóa khoản chi phí.",
            "charge-added" => "Đã thêm khoản chi phí.",
            "group-saved" => "Đã cập nhật nhóm chi phí.",
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
            Charges = detail.Charges
                .Select(charge => new ChargeRowInput
                {
                    ChargeId = charge.Id,
                    Label = charge.Label,
                    Amount = charge.Amount,
                    Group = charge.Group ?? ChargeGroup.Logistics,
                    Method = charge.Method,
                    ManualShares = charge.Method == ChargeSplitMethod.Manual
                        ? charge.Shares.ToDictionary(share => share.LineId, share => share.Amount)
                        : null
                })
                .ToList(),
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
                Lines = Form.Lines ?? [],
                // Danh sách trong form là danh sách đúng: dòng bị xóa trong bảng thì khoản đó bị xóa khỏi đơn.
                Charges = (Form.Charges ?? [])
                    .Select(row => new UpdatePurchaseChargeInput { ChargeId = row.ChargeId, Amount = row.Amount, Label = row.Label, Group = row.Group, Method = row.Method })
                    .ToList()
            },
            HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            ShowForm = true;
            NeedMeasures = true;
            EditingId = orderId;
            ErrorMessage = PurchaseErrorMessages.For(result.PurchaseError, PurchaseErrorMessages.UpdateFailed);
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
            Lines = Form.Lines ?? [],
            Charges = (Form.Charges ?? [])
                .Select(row => new CreatePurchaseChargeInput { Amount = row.Amount, Label = row.Label, Group = row.Group, Method = row.Method })
                .ToList()
        };
        var result = await createOrder.HandleAsync(command, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            ShowForm = true;
            NeedMeasures = true;
            ErrorMessage = PurchaseErrorMessages.For(result.PurchaseError, PurchaseErrorMessages.CreateFailed);
            KeepDraftLines();
            await LoadAsync(null, null);
            return Page();
        }

        return Redirect($"/purchasing?order={result.Value}&notice=created");
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
        if (NeedMeasures)
        {
            ItemMeasures = await catalogMeasures.HandleAsync(Items.Select(item => item.Id).ToArray(), cancellationToken);
        }

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

    public List<decimal> UnitPrices { get; set; } = [];

    public List<decimal> DutyPercents { get; set; } = [];

    public List<CreatePurchaseLineInput> Lines { get; set; } = [];

    /// <summary>Các dòng chi phí chung của cả đơn (bảng trong form).</summary>
    public List<ChargeRowInput> Charges { get; set; } = [];

    public static PurchaseForm Create() => new()
    {
        VatPercent = 10m,
        Lines = [new CreatePurchaseLineInput(), new CreatePurchaseLineInput(), new CreatePurchaseLineInput()],
        UnitPrices = [0m, 0m, 0m],
        DutyPercents = [0m, 0m, 0m],
        // Bốn dòng mẫu: nhóm quyết định khoản có làm tăng trị giá tính thuế (cước biển) hay cộng thẳng vào giá vốn (còn lại).
        // Cước, THC, kéo xe nên chia theo thể tích: để chế độ tự động (AutoVolume), trang tự chọn thể tích khi mọi mặt hàng
        // đã có số đo và trị giá FOB khi chưa, thay vì mặc định thể tích rồi báo lỗi ngay.
        Charges =
        [
            new ChargeRowInput { Label = "Cước vận chuyển đường biển quốc tế (Ocean Freight)", Group = ChargeGroup.CustomsValueAdjustment, Method = ChargeSplitMethod.FobValue, AutoVolume = true },
            new ChargeRowInput { Label = "Phí nâng hạ bãi cảng (THC, D/O, vệ sinh container)", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.FobValue, AutoVolume = true },
            new ChargeRowInput { Label = "Phí kiểm định chất lượng sản phẩm", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.FobValue },
            new ChargeRowInput { Label = "Vận chuyển kéo xe container từ cảng về kho", Group = ChargeGroup.Logistics, Method = ChargeSplitMethod.FobValue, AutoVolume = true }
        ]
    };
}

/// <summary>Dữ liệu cho một dòng trong bảng chi phí (partial <c>_ChargeRow</c>); <paramref name="Index"/> là chỉ số hoặc chỗ giữ chỗ cho mẫu JS.</summary>
public sealed record ChargeRowView(ChargeRowInput Row, string Index);

public sealed class ChargeRowInput
{
    /// <summary>Có giá trị khi là khoản đã lưu của đơn đang sửa.</summary>
    public Guid? ChargeId { get; set; }

    public string? Label { get; set; }

    public decimal Amount { get; set; }

    public ChargeGroup Group { get; set; } = ChargeGroup.Logistics;

    public ChargeSplitMethod Method { get; set; } = ChargeSplitMethod.FobValue;

    /// <summary>Dòng mẫu chia tự động theo thể tích/FOB (chỉ để dựng form, không nhận từ form: gửi lên là cách chia đã chọn).</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool AutoVolume { get; set; }

    /// <summary>Chỉ để hiển thị bản xem thử của khoản chia tay (không nhận từ form).</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IReadOnlyDictionary<Guid, decimal>? ManualShares { get; set; }
}
