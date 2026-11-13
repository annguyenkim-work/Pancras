using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = context =>
        {
            var validator = context.HttpContext.RequestServices.GetRequiredService<EmployeePrincipalValidator>();
            return validator.ValidateAsync(context);
        };
    });
builder.Services.AddAuthentication().AddCookie(PendingTwoFactor.Scheme, options =>
{
    options.Cookie.Name = PendingTwoFactor.CookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
    options.AddPolicy("login", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPerMinute", 20),
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

builder.Services.AddDbContext<PancrasDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ICatalogItemRepository, CatalogItemRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<ISupplierPurchaseStatsReader, EmptySupplierPurchaseStatsReader>();
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
builder.Services.AddScoped<ListSupplierCardsHandler>();
builder.Services.AddScoped<CreateSupplierCommandHandler>();
builder.Services.AddScoped<ListSuppliersForPurchaseHandler>();
builder.Services.AddScoped<GetSupplierProfileHandler>();
builder.Services.AddScoped<UpdateSupplierCommandHandler>();
builder.Services.AddScoped<DeleteSupplierCommandHandler>();
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
builder.Services.AddScoped<CreateTransferSlipCommandHandler>();
builder.Services.AddScoped<CreateAndShipTransferCommandHandler>();
builder.Services.AddScoped<ConfirmTransferShipmentCommandHandler>();
builder.Services.AddScoped<ConfirmTransferReceiptCommandHandler>();
builder.Services.AddScoped<CreateStocktakeCommandHandler>();
builder.Services.AddScoped<ApproveStocktakeCommandHandler>();
builder.Services.AddScoped<CreateQuantityAdjustmentCommandHandler>();
builder.Services.AddScoped<ApproveQuantityAdjustmentCommandHandler>();
builder.Services.AddScoped<CreateDisposalSlipCommandHandler>();
builder.Services.AddScoped<ApproveDisposalSlipCommandHandler>();
builder.Services.AddScoped<ListPurchaseOrdersHandler>();
builder.Services.AddScoped<GetPurchaseOrderHandler>();
builder.Services.AddScoped<CreatePurchaseOrderCommandHandler>();
builder.Services.AddScoped<AllocateChargeCommandHandler>();
builder.Services.AddScoped<AssignChargeGroupCommandHandler>();
builder.Services.AddScoped<RecordCustomsDeclarationCommandHandler>();
builder.Services.AddScoped<ReceivePurchaseOrderCommandHandler>();
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

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Pancras.Unhandled");
        var failure = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        logger.LogError(failure?.Error, "Unhandled exception.");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("Đã có lỗi. Thử lại sau.");
    });
});

app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

public partial class Program;
