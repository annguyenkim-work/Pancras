# Async, logging, xử lý lỗi

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Quy định chung chạy ngang mọi tầng: lập trình bất đồng bộ, ghi log kỹ thuật, và xử lý lỗi bất ngờ.

## 1. Async

- Mọi thao tác I/O (database, tệp, mạng) là `async`/`await`; tên phương thức có hậu tố **`Async`**.
- **Truyền `CancellationToken` xuyên suốt** từ PageModel (`HttpContext.RequestAborted`) xuống handler, repository và các lời gọi EF.
- Cấm: `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, `async void` (trừ trình xử lý sự kiện), `Task.Run` để bọc I/O.
- Không cần `ConfigureAwait(false)` (ASP.NET Core không có `SynchronizationContext`).
- Không dùng `lock` quanh `await`; tranh chấp dữ liệu giải quyết bằng concurrency token (xem [data-access.md](data-access.md)).

## 2. Logging

- Mọi handler và service **inject `ILogger<T>`**.
- Dùng `Microsoft.Extensions.Logging`; **không thêm thư viện log**. Đích ghi: console/journald (do hệ điều hành thu).
- **Mẫu câu tiếng Anh + tham số có tên, không nối chuỗi** (không dùng `$"..."`): `logger.LogInformation("Approved transfer {SlipId}.", slip.Id);`
- **Chỉ ghi Id**, không ghi tên người, số điện thoại, email, mật khẩu, token, mã xác thực (không PII, không secret).
- Mức log:

| Mức | Dùng cho |
| --- | --- |
| Information | Kết quả thành công; từ chối nghiệp vụ thường (trùng mã, thiếu dữ liệu, sai trạng thái) |
| Warning | Thiếu quyền, xung đột đồng thời, khóa tài khoản, đầu vào bất thường (nghi sửa Id, tệp quá cỡ) |
| Error | Lỗi kỹ thuật và ngoại lệ không lường trước (database, tệp, bug) |

- **Log kỹ thuật khác nhật ký nghiệp vụ.** `ILogger` là cho người vận hành, tiếng Anh; `IAuditLog` là nhật ký thao tác cho quản trị: lưu mã hành động (`nhóm.sự_kiện`), UI hiển thị bằng tiếng Việt (xem [use-cases-and-results.md](use-cases-and-results.md)).

## 3. Lỗi

- **Lỗi nghiệp vụ trả bằng `Result`, không ném exception** (xem use-cases-and-results.md). Page handler không có `try/catch` cho lỗi nghiệp vụ.
- **Chỉ `catch` ngoại lệ kỹ thuật cụ thể, ở đúng biên:** ví dụ `DbUpdateException` do trùng khóa/xung đột (trong repository), `IOException`/`UnauthorizedAccessException` khi đọc ghi tệp, `OverflowException` khi tính toán số. Mỗi `catch` có điều kiện hẹp, và hoặc đổi thành mã lỗi, hoặc log rồi ném lại.
- **Không `catch (Exception)` nuốt lỗi.** Không để `catch` rỗng.
- Domain ném `ArgumentException`/`InvalidOperationException` cho **lập trình sai** (đối số không hợp lệ không thể xảy ra nếu đã qua validator), không dùng cho kết quả nghiệp vụ.
- **Lỗi bất ngờ không bắt** đi lên `UseExceptionHandler` ở `Program.cs`: log `Error` kèm exception, trả trang 500.
- **Trang 500 hiện mã theo dõi (`TraceId`)**, cùng mã đó có trong log. Không hiện stack trace hay tên ngoại lệ.
- Trang 500, 404, 403 và 429 là trang **tĩnh, tự chứa** (không dùng `_Layout`) để vẫn hiển thị khi ứng dụng lỗi. Chữ tiếng Việt, dùng đúng design tokens.

## Nợ lệch so với quy tắc

| Vấn đề | Hiện trạng |
| --- | --- |
| Mức Warning | 109 lời gọi `LogWarning`, phần lớn là "Rejected ..." từ chối nghiệp vụ thường; cần hạ xuống `Information` khi chạm tới |
| Trang 500 | Chưa hiện `TraceId`; HTML nằm cứng trong `Program.cs` |
| `catch` rỗng | `catch` không điều kiện ở `StockDocumentAttachmentHandlers.cs:136`, `TransferAttachmentHandlers.cs:165`, `PurchaseAttachmentHandlers.cs:127`: cần thu hẹp theo loại ngoại lệ và log |
| `.Result` | `UpdateEmployeeCommandHandler.cs:85` (`outcome.Result`) là thuộc tính của kết quả, không phải chờ task; giữ nhưng nên đổi tên cho khỏi gây nhầm |
| Bắt `OverflowException` | `Purchasing/Index.cshtml.cs:231` và `Domain/Purchasing/ChargeAllocation.cs:97`: kiểm tra phạm vi trước thay vì bắt ngoại lệ, nếu được |

## Chờ chốt (chưa có hiệu lực)

- Thời gian lưu log trên máy chủ và cách thu gom/xoay vòng (để ở `06-operations/deployment.md`).
- Kiểm tra sức khỏe (health check) và giám sát lỗi (cảnh báo khi Error tăng).
