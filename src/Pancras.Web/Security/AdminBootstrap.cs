using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Web.Security;

/// <summary>
/// Tạo Admin đầu tiên khi triển khai: <c>dotnet Pancras.Web.dll create-admin</c>.
/// Đọc thông tin từ biến môi trường để mật khẩu không nằm trong lịch sử lệnh. Chỉ chạy khi chưa có Admin nào đang hoạt động.
/// </summary>
public static class AdminBootstrap
{
    public const string Command = "create-admin";

    public static async Task<int> RunAsync(WebApplication app)
    {
        var name = Environment.GetEnvironmentVariable("PANCRAS_ADMIN_NAME");
        var email = Environment.GetEnvironmentVariable("PANCRAS_ADMIN_EMAIL");
        var phone = Environment.GetEnvironmentVariable("PANCRAS_ADMIN_PHONE");
        var password = Environment.GetEnvironmentVariable("PANCRAS_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(phone) || string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("Thiếu biến môi trường. Cần PANCRAS_ADMIN_NAME, PANCRAS_ADMIN_EMAIL, PANCRAS_ADMIN_PHONE, PANCRAS_ADMIN_PASSWORD.");
            return 2;
        }

        using var scope = app.Services.CreateScope();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
        if (await employees.CountActiveAdminsAsync(CancellationToken.None) > 0)
        {
            Console.Error.WriteLine("Đã có Admin đang hoạt động. Không tạo thêm bằng lệnh này.");
            return 3;
        }

        var handler = scope.ServiceProvider.GetRequiredService<CreateEmployeeCommandHandler>();
        var result = await handler.HandleAsync(
            new CreateEmployeeCommand
            {
                FullName = name,
                Email = email,
                Phone = phone,
                Password = password,
                Role = EmployeeRole.Admin,
                JobTitle = "Quản trị hệ thống"
            },
            new EmployeeActor(Guid.Empty, EmployeeRole.Admin, null),
            CancellationToken.None);
        if (result.IsFailure)
        {
            Console.Error.WriteLine("Không tạo được Admin: " + result.EmployeeError + (result.EmployeeError == EmployeeError.WeakPassword ? " (mật khẩu cần từ 8 đến 128 ký tự)" : string.Empty));
            return 1;
        }

        Console.WriteLine("Đã tạo Admin. Mã đăng nhập: " + result.Value!.Code);
        return 0;
    }
}
