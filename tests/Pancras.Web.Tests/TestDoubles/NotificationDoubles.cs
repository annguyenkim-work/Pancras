using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Domain.Employees;
using Pancras.Domain.Notifications;
using Pancras.Web.Pages.Notifications;

namespace Pancras.Web.Tests;

// Test double của Thông báo, dùng chung cho mọi test trang.

internal sealed class NoticeStoreDouble : Pancras.Application.Notifications.INoticeStore
{
    public Task AddAsync(Notification notice, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveForDocumentAsync(Guid documentId, CancellationToken cancellationToken) => Task.CompletedTask;
}
