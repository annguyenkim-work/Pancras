using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pancras.Application.Catalog;
using Pancras.Application.Employees;
using Pancras.Application.Inventory;
using Pancras.Application.Locations;
using Pancras.Application.Purchasing;
using Pancras.Application.Suppliers;
using Pancras.Infrastructure.Identity;
using Pancras.Infrastructure.Persistence;
using Pancras.Web.Catalog;
using Pancras.Web.Pages;
using Pancras.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    foreach (var route in ComingSoonModel.Routes)
    {
        options.Conventions.AddPageRoute("/ComingSoon", route);
    }
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.Cookie.Name = "Pancras.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = context =>
        {
            var validator = context.HttpContext.RequestServices.GetRequiredService<EmployeePrincipalValidator>();
            return validator.ValidateAsync(context);
        };
    });
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .PostConfigure<TimeProvider>((options, timeProvider) => options.TimeProvider = timeProvider);
builder.Services.AddAuthentication().AddCookie(PendingTwoFactor.Scheme, options =>
{
    options.Cookie.Name = PendingTwoFactor.CookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    options.SlidingExpiration = false;
});
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("PancrasERP");
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            PlainStatusPage(
                "Thử lại sau",
                "Bạn thử đăng nhập quá nhiều lần. Vui lòng đợi một phút rồi thử lại."),
            cancellationToken);
    };
    // RemoteIpAddress: khi đứng sau reverse proxy, bật Deployment:BehindReverseProxy và KnownProxies
    // để không gom mọi người dùng vào một bucket.
    options.AddPolicy("login", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPerMinute", 120),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Pancras");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Thiếu ConnectionStrings:DefaultConnection. Đặt chuỗi kết nối bằng user-secrets, không ghi mật khẩu vào git.");
}

builder.Services.AddScoped<AuditStampInterceptor>();
builder.Services.AddDbContext<PancrasDbContext>((provider, options) =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
        .AddInterceptors(provider.GetRequiredService<AuditStampInterceptor>()));
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ICatalogItemRepository, CatalogItemRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<ILocationManagementRepository, LocationRepository>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IPurchaseAttachmentRepository, PurchaseAttachmentRepository>();
builder.Services.AddScoped<ITransferAttachmentRepository, TransferAttachmentRepository>();
builder.Services.AddScoped<IStockDocumentAttachmentRepository, StockDocumentAttachmentRepository>();
// Chứng từ lưu ngoài wwwroot nên không thể tải trực tiếp bằng đường dẫn; chỉ tải qua trang có kiểm tra quyền.
var attachmentsPath = builder.Configuration["Storage:AttachmentsPath"];
builder.Services.AddSingleton<IAttachmentStorage>(_ => new FileSystemAttachmentStorage(
    string.IsNullOrWhiteSpace(attachmentsPath)
        ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "attachments")
        : attachmentsPath));
builder.Services.AddScoped<UploadPurchaseAttachmentCommandHandler>();
builder.Services.AddScoped<ListPurchaseAttachmentsHandler>();
builder.Services.AddScoped<OpenPurchaseAttachmentHandler>();
builder.Services.AddScoped<DeletePurchaseAttachmentCommandHandler>();
builder.Services.AddScoped<ISupplierPurchaseStatsReader, PurchaseSupplierStatsReader>();
builder.Services.AddScoped<LoginEmployeeCommandHandler>();
builder.Services.AddScoped<CreateEmployeeCommandHandler>();
builder.Services.AddScoped<UpdateEmployeeCommandHandler>();
builder.Services.AddScoped<GetEmployeeHandler>();
builder.Services.AddScoped<ListEmployeesHandler>();
builder.Services.AddScoped<SetEmployeeActiveCommandHandler>();
builder.Services.AddScoped<UpdateOwnProfileCommandHandler>();
builder.Services.AddScoped<ChangeOwnPasswordCommandHandler>();
builder.Services.AddScoped<ListLocationsHandler>();
builder.Services.AddScoped<CreateLocationCommandHandler>();
builder.Services.AddScoped<UpdateLocationCommandHandler>();
builder.Services.AddScoped<DeleteLocationCommandHandler>();
builder.Services.AddScoped<ListSupplierCardsHandler>();
builder.Services.AddScoped<CreateSupplierCommandHandler>();
builder.Services.AddScoped<ListSuppliersForPurchaseHandler>();
builder.Services.AddScoped<GetSupplierProfileHandler>();
builder.Services.AddScoped<UpdateSupplierCommandHandler>();
builder.Services.AddScoped<DeactivateSupplierCommandHandler>();
builder.Services.AddScoped<ReactivateSupplierCommandHandler>();
builder.Services.AddScoped<ListCatalogItemsHandler>();
builder.Services.AddScoped<GetCatalogItemHandler>();
builder.Services.AddScoped<GetCatalogMeasuresHandler>();
builder.Services.AddScoped<ListMatchingCatalogItemsHandler>();
builder.Services.AddSingleton<CatalogImageStore>();
builder.Services.AddScoped<CreateCatalogItemCommandHandler>();
builder.Services.AddScoped<UpdateCatalogPricesCommandHandler>();
builder.Services.AddScoped<ResolveSalePriceHandler>();
builder.Services.AddScoped<ListStockBalancesHandler>();
builder.Services.AddScoped<GetStockItemDetailHandler>();
builder.Services.AddScoped<ListStockHistoryHandler>();
builder.Services.AddScoped<ListInventoryDocumentsHandler>();
builder.Services.AddScoped<ListStockNoticesHandler>();
builder.Services.AddScoped<CreateTransferRequestCommandHandler>();
builder.Services.AddScoped<DecideTransferCommandHandler>();
builder.Services.AddScoped<UpdateTransferRequestCommandHandler>();
builder.Services.AddScoped<ShipTransferCommandHandler>();
builder.Services.AddScoped<ResolveTransferDiscrepancyCommandHandler>();
builder.Services.AddScoped<ReturnTransferCommandHandler>();
builder.Services.AddScoped<UploadTransferAttachmentCommandHandler>();
builder.Services.AddScoped<UploadStockDocumentAttachmentCommandHandler>();
builder.Services.AddScoped<OpenStockDocumentAttachmentHandler>();
builder.Services.AddScoped<OpenTransferAttachmentHandler>();
builder.Services.AddScoped<ConfirmTransferReceiptCommandHandler>();
builder.Services.AddScoped<CancelTransferCommandHandler>();
builder.Services.AddScoped<CreateStocktakeCommandHandler>();
builder.Services.AddScoped<ApproveStocktakeCommandHandler>();
builder.Services.AddScoped<CreateQuantityAdjustmentCommandHandler>();
builder.Services.AddScoped<ApproveQuantityAdjustmentCommandHandler>();
builder.Services.AddScoped<CreateDisposalSlipCommandHandler>();
builder.Services.AddScoped<ApproveDisposalSlipCommandHandler>();
builder.Services.AddScoped<ListPurchaseOrdersHandler>();
builder.Services.AddScoped<GetPurchaseOrderHandler>();
builder.Services.AddScoped<ChangePurchaseStatusCommandHandler>();
builder.Services.AddScoped<AdjustPurchaseCostCommandHandler>();
builder.Services.AddScoped<CreatePurchaseOrderCommandHandler>();
builder.Services.AddScoped<UpdatePurchaseOrderCommandHandler>();
builder.Services.AddScoped<ReceivePurchaseOrderCommandHandler>();
builder.Services.AddScoped<ExportPurchaseOrderHandler>();
builder.Services.AddScoped<EmployeeSignIn>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Pancras.Application.Auditing.IRequestInfo, RequestInfo>();
builder.Services.AddScoped<Pancras.Application.Auditing.IAuditLog, Pancras.Infrastructure.Persistence.AuditLog>();
builder.Services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddScoped<Pancras.Application.Auditing.ListAuditHandler>();
builder.Services.AddScoped<VerifyTwoFactorCommandHandler>();
builder.Services.AddScoped<BeginTwoFactorSetupHandler>();
builder.Services.AddScoped<ConfirmTwoFactorSetupCommandHandler>();
builder.Services.AddScoped<DisableTwoFactorCommandHandler>();
builder.Services.AddScoped<ResetTwoFactorCommandHandler>();
builder.Services.AddScoped<UnlockEmployeeCommandHandler>();
builder.Services.AddScoped<EmployeePrincipalValidator>();

var behindProxy = builder.Configuration.GetValue("Deployment:BehindReverseProxy", builder.Environment.IsProduction());
if (behindProxy)
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 1;
        foreach (var proxy in builder.Configuration.GetSection("Deployment:KnownProxies").Get<string[]>() ?? [])
        {
            if (System.Net.IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }
    });
}

if (builder.Environment.IsProduction())
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
    });
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
}

// Giới hạn kích thước request ở mức máy chủ (file chứng từ tối đa 10 MB cộng phần form). Không dùng [RequestSizeLimit]
// trên trang: nó là resource filter, chạy sau bộ kiểm tra antiforgery vốn đã đọc cả form nên không chặn được sớm.
const long MaxRequestBytes = 12L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBytes);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options => options.MultipartBodyLengthLimit = MaxRequestBytes);

var app = builder.Build();

if (behindProxy)
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsProduction())
{
    if (string.Equals(app.Configuration["AllowedHosts"], "*", StringComparison.Ordinal))
    {
        app.Logger.LogWarning("AllowedHosts is '*'. Set it to the real domain name in production.");
    }

    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; "
        + "script-src 'self' 'unsafe-inline'; "
        + "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
        + "font-src 'self' https://fonts.gstatic.com; "
        + "img-src 'self' data:; "
        + "connect-src 'self'; "
        + "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await next();
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Pancras.Unhandled");
        var failure = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        logger.LogError(failure?.Error, "Unhandled exception.");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(
            "<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />"
            + "<title>Đã có lỗi - PancrasERP</title><link rel=\"stylesheet\" href=\"/css/site.css\" /></head>"
            + "<body class=\"min-h-screen bg-slate-100 font-sans text-slate-900\"><main class=\"flex min-h-screen items-center justify-center p-4\">"
            + "<div class=\"w-full max-w-md rounded-2xl bg-white p-8 text-center shadow-sm\">"
            + "<h1 class=\"text-xl font-bold\">Đã có lỗi</h1>"
            + "<p class=\"mt-2 text-sm text-slate-600\">Hệ thống chưa xử lý được yêu cầu của bạn. Thử lại sau ít phút. Nếu lỗi lặp lại, báo cho quản trị viên.</p>"
            + "<a href=\"/\" class=\"mt-6 inline-block rounded-lg bg-sky-600 px-4 py-2 text-sm font-bold text-white\">Về trang chủ</a>"
            + "</div></main></body></html>");
    });
});

app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    if (response.StatusCode != StatusCodes.Status404NotFound)
    {
        return;
    }

    response.ContentType = "text/html; charset=utf-8";
    await response.WriteAsync(PlainStatusPage(
        "Không tìm thấy trang",
        "Địa chỉ này không có trên hệ thống."));
});

app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

if (args.Length > 0 && string.Equals(args[0], AdminBootstrap.Command, StringComparison.Ordinal))
{
    return await AdminBootstrap.RunAsync(app);
}

app.Run();
return 0;

static string PlainStatusPage(string title, string message) =>
    "<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />"
    + "<title>" + title + " - PancrasERP</title><link rel=\"stylesheet\" href=\"/css/site.css\" /></head>"
    + "<body class=\"min-h-screen bg-slate-100 font-sans text-slate-900\"><main class=\"flex min-h-screen items-center justify-center p-4\">"
    + "<div class=\"w-full max-w-md rounded-2xl bg-white p-8 text-center shadow-sm\">"
    + "<h1 class=\"text-xl font-bold\">" + title + "</h1>"
    + "<p class=\"mt-2 text-sm text-slate-600\">" + message + "</p>"
    + "<a href=\"/\" class=\"mt-6 inline-block rounded-lg bg-sky-600 px-4 py-2 text-sm font-bold text-white\">Về trang chủ</a>"
    + "</div></main></body></html>";

public partial class Program;
