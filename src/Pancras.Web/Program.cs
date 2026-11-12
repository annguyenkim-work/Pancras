using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Pancras.Application.Catalog;
using Pancras.Application.Employees;
using Pancras.Application.Suppliers;
using Pancras.Infrastructure.Identity;
using Pancras.Infrastructure.Persistence;
using Pancras.Infrastructure.Seeding;
using Pancras.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/dang-nhap";
        options.LogoutPath = "/dang-xuat";
        options.AccessDeniedPath = "/tu-choi";
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
builder.Services.AddScoped<ISupplierPurchaseStatsReader, EmptySupplierPurchaseStatsReader>();
builder.Services.AddScoped<LoginEmployeeCommandHandler>();
builder.Services.AddScoped<CreateEmployeeCommandHandler>();
builder.Services.AddScoped<UpdateEmployeeCommandHandler>();
builder.Services.AddScoped<GetEmployeeHandler>();
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
builder.Services.AddScoped<CreateCatalogItemCommandHandler>();
builder.Services.AddScoped<UpdateCatalogPricesCommandHandler>();
builder.Services.AddScoped<ResolveSalePriceHandler>();
builder.Services.AddScoped<AdminEmployeeSeed>();
builder.Services.AddScoped<EmployeeSignIn>();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var seed = scope.ServiceProvider.GetRequiredService<AdminEmployeeSeed>();
    await seed.SeedAsync();
}

app.Run();

public partial class Program;
