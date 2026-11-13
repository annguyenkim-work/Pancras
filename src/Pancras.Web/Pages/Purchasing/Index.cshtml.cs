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
    AllocateChargeCommandHandler allocateCharge,
    AssignChargeGroupCommandHandler assignGroup,
    RecordCustomsDeclarationCommandHandler recordDeclaration,
    ReceivePurchaseOrderCommandHandler receiveOrder,
    ListSuppliersForPurchaseHandler listSuppliers,
    ListInventoryDocumentsHandler inventory) : PageModel
{
    public IReadOnlyList<PurchaseOrderSummary> Orders { get; private set; } = [];

    public bool HasAnyOrders { get; private set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    public PurchaseOrderDetail? Selected { get; private set; }

    public IReadOnlyList<SupplierPurchaseOption> Suppliers { get; private set; } = [];

    public IReadOnlyList<StockLocationChoice> Locations { get; private set; } = [];

    public IReadOnlyList<StockItemChoice> Items { get; private set; } = [];

    public bool ShowForm { get; private set; }

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

    [BindProperty]
    public RecordCustomsDeclarationCommand DeclarationCommand { get; set; } = new();

    [BindProperty]
    public string? DeclaredCustomsText { get; set; }

    [BindProperty]
    public string? DeclaredDutyText { get; set; }

    [BindProperty]
    public string? DeclaredVatText { get; set; }

    public async Task OnGetAsync(Guid? id, Guid? order, Guid? formula, string? form, string? notice, string? error)
    {
        ShowForm = string.Equals(form, "1", StringComparison.Ordinal);
        Notice = notice switch
        {
            "created" => "Đã tạo đơn nhập.",
            "received" => "Đã nhập kho.",
            _ => null
        };
        if (!string.IsNullOrEmpty(error) && Enum.TryParse<PurchaseError>(error, out var parsed) && parsed != PurchaseError.None)
        {
            ErrorMessage = PurchaseErrorMessages.For(parsed);
        }

        var selected = order is Guid orderId && orderId != Guid.Empty ? orderId : id;
        await LoadAsync(selected, formula);
    }

    public async Task<IActionResult> OnPostCreateAsync()
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
            Lines = lines
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

        var logistics = Form.OceanFreight + Form.PortHandling + Form.InspectionFee + Form.InlandTrucking;
        if (logistics > 0)
        {
            var allocated = await allocateCharge.HandleAsync(
                new AllocateChargeCommand
                {
                    PurchaseOrderId = result.Value,
                    Amount = logistics,
                    Method = ChargeSplitMethod.FobValue
                },
                HttpContext.RequestAborted);
            if (allocated.IsSuccess)
            {
                await assignGroup.HandleAsync(
                    new AssignChargeGroupCommand
                    {
                        PurchaseOrderId = result.Value,
                        ChargeId = allocated.Value,
                        Group = ChargeGroup.Logistics
                    },
                    HttpContext.RequestAborted);
            }
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

    public async Task<IActionResult> OnPostDeclarationAsync()
    {
        if (!TryMoney(DeclaredCustomsText, out var customs, out var hasCustoms)
            || !TryMoney(DeclaredDutyText, out var duty, out var hasDuty)
            || !TryMoney(DeclaredVatText, out var vat, out var hasVat))
        {
            return Redirect($"/purchasing?order={DeclarationCommand.PurchaseOrderId}&error={PurchaseError.InvalidOrder}");
        }

        DeclarationCommand.DeclaredCustomsValue = customs;
        DeclarationCommand.DeclaredDuty = duty;
        DeclarationCommand.DeclaredVat = vat;
        DeclarationCommand.HasCustoms = hasCustoms;
        DeclarationCommand.HasDuty = hasDuty;
        DeclarationCommand.HasVat = hasVat;
        var result = await recordDeclaration.HandleAsync(DeclarationCommand, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            return Redirect($"/purchasing?order={DeclarationCommand.PurchaseOrderId}&error={result.PurchaseError}");
        }

        return Redirect($"/purchasing?order={DeclarationCommand.PurchaseOrderId}");
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

    private static bool TryMoney(string? text, out decimal? amount, out bool present)
    {
        amount = null;
        present = false;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), out var parsed)
            || decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out parsed))
        {
            amount = parsed;
            present = true;
            return true;
        }

        return false;
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
