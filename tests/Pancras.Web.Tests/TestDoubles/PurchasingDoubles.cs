using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Purchasing;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Purchasing;
using Pancras.Web.Pages.Purchasing;

namespace Pancras.Web.Tests;

// Test double của Mua hàng, dùng chung cho mọi test trang.

internal sealed class AttachmentDouble : IPurchaseAttachmentRepository, IAttachmentStorage
{
    public List<PurchaseAttachment> Items { get; } = [];

    public Dictionary<string, byte[]> Files { get; } = [];

    public Task<IReadOnlyList<PurchaseAttachment>> ListAsync(Guid purchaseOrderId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PurchaseAttachment>>(Items.Where(item => item.PurchaseOrderId == purchaseOrderId).ToArray());

    public Task<PurchaseAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task AddAsync(PurchaseAttachment attachment, CancellationToken cancellationToken)
    {
        Items.Add(attachment);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        Items.RemoveAll(item => item.Id == id);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        Files[storedName] = copy.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Files.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string storedName, CancellationToken cancellationToken)
    {
        Files.Remove(storedName);
        return Task.CompletedTask;
    }
}

internal sealed class PurchaseDirectoryDouble : IPurchaseOrderRepository
{
    public List<PurchaseOrder> Orders { get; } = [];

    public Task<IReadOnlyList<PurchaseShipmentKey>> ListShipmentKeysAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PurchaseShipmentKey>>(Orders.Select(order => new PurchaseShipmentKey(order.Id, order.Code, order.ContainerNumber, order.BillOfLading, order.Status)).ToArray());

    public Task<PurchaseOrderPage> ListPageAsync(PurchaseOrderQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var visible = Orders.Where(filter.Visible.Compile()).OrderByDescending(order => order.Id).ToList();
        var term = filter.Term?.Trim();
        var matching = visible
            .Where(order => filter.SupplierId is null || order.SupplierId == filter.SupplierId)
            .Where(order => filter.Status is null || order.Status == filter.Status)
            .Where(order => string.IsNullOrEmpty(term)
                || new[] { order.Code, order.ContainerNumber, order.BillOfLading, order.Port, order.SupplierName }
                    .Any(text => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var size = Math.Max(1, pageSize);
        var page = Math.Clamp(pageNumber, 1, Math.Max(1, (matching.Count + size - 1) / size));
        return Task.FromResult(new PurchaseOrderPage(
            matching.Skip((page - 1) * size).Take(size).ToArray(),
            matching.Count,
            page,
            visible.Count > 0,
            matching.Count == 0 ? null : matching[0].Id));
    }

    public Task<PurchaseOrder?> FindReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.FirstOrDefault(order => order.Id == id));

    public Task<PurchaseOrder?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        FindReadOnlyAsync(id, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.Any(order => order.NormalizedCode == normalizedCode));

    public Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        Orders.Add(order);
        return Task.CompletedTask;
    }
}
