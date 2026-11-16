# Quy định test

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16).

**Mục đích:** Loại test, cách đặt tên, khi nào bắt buộc, và test dùng dữ liệu thế nào.

## 1. Dự án test

Framework xUnit. Bốn dự án, mỗi dự án test đúng một tầng:

| Dự án | Test gì | Cách làm |
| --- | --- | --- |
| `Pancras.Domain.Tests` | Thực thể, bất biến, `*Access`, tính toán (giá vốn, tồn kho, mã nhân viên, TOTP) | Không phụ thuộc gì ngoài Domain |
| `Pancras.Application.Tests` | Handler: thành công, từng mã lỗi, quyền | Fake viết tay (`FakeEmployeeStore`, `FakeInventoryStore`...), không dùng thư viện mock |
| `Pancras.Infrastructure.Tests` | Repository, dấu vết bản ghi, lưu phiếu | EF Core InMemory |
| `Pancras.Web.Tests` | Trang, đăng nhập, phân quyền, quét mã nguồn | `WebApplicationFactory` + EF InMemory/test double |

Test không được phụ thuộc cơ sở dữ liệu thật hay mạng.

## 2. Đặt tên

- Tên test tiếng Anh dạng **`Subject_Behavior_Condition`**, ví dụ `Create_RejectsDuplicateCode_WithoutAddingAnotherRow`.
- Comment và dữ liệu mẫu có thể là tiếng Việt.
- Class test: `<ĐốiTượng>Tests` (ví dụ `CreateCatalogItemCommandHandlerTests`), cùng tầng với đối tượng.
- Mỗi test kiểm một hành vi; không gom nhiều hành vi không liên quan vào một test.

## 3. Khi nào bắt buộc

1. **Mọi handler mới:** có test thành công, test cho **từng mã lỗi** nó có thể trả, và test thiếu quyền.
2. **Mọi bất biến Domain** (tồn không âm, luôn còn Admin hoạt động, mỗi kho một Trưởng...) có test ở `Domain.Tests`.
3. **Sửa lỗi:** viết test tái hiện lỗi trước, thấy nó fail, rồi mới sửa. Test hồi quy đặt cạnh test của handler liên quan.
4. **Trang có phân quyền:** test ở `Web.Tests` rằng vai không được phép **không thấy nút** và **bị chặn khi POST trực tiếp**, và dữ liệu nhạy cảm không nằm trong HTML trả về.

## 4. Cơ sở dữ liệu trong test

- Test tự động chỉ dùng fake hoặc EF InMemory; **không dùng PostgreSQL thật**.
- InMemory không kiểm concurrency token, transaction, migration. Những hành vi này **ghi vào checklist test thủ công** và kiểm trên PostgreSQL thật (xem `05-process/manual-testing-and-review.md`).

## 5. Luật viết thành test quét mã nguồn

Đã có: `NoNativeDialogTests` quét `.cshtml`, cấm `confirm()/alert()/prompt()`.

Quy tắc đã chốt, **làm sau** khi dọn nợ UI (chưa tự động hóa ở thời điểm này):

- Domain/Application không tham chiếu ASP.NET/EF Core; Web không dùng Infrastructure ngoài `Program.cs`.
- `.cshtml` không dùng `IsInRole`, không `onclick` inline, không dùng class màu ngoài palette.
- Tiền không `ToString` tự do ở trang; chỉ dùng hàm chung (`VndMoney`).

Test quét mã nguồn mới sẽ đỏ ngay với nợ hiện có, nên cần danh sách ngoại lệ tạm có hạn xử lý.

## 6. Lệnh

```bash
dotnet test Pancras.slnx --warnaserror --nologo
dotnet test tests/Pancras.Domain.Tests --filter "FullyQualifiedName~SomeTest"
```

- Mỗi lần gọi `dotnet test` chỉ truyền một đường dẫn dự án (hai csproj cùng lúc lỗi MSB1008).
- Không để cảnh báo biên dịch (`--warnaserror`).

## Nợ lệch so với quy định

| Vấn đề | Hiện trạng |
| --- | --- |
| CLAUDE.md thiếu | Chỉ liệt kê 3 dự án test, thiếu `Infrastructure.Tests` |
| Test quét mã nguồn | Mới có `NoNativeDialogTests` |
| Tên test hồi quy | `ManualRetestRegressionTests` đặt theo đợt test thủ công, không theo handler; chuyển test về file của handler khi chạm tới |
| Đủ mã lỗi | Chưa rà từng enum lỗi xem handler nào chưa có test cho mã lỗi nào |

## Quyết định đã chốt (2026-11-16)

- Chưa đặt ngưỡng độ phủ tối thiểu.
- [Đề xuất của AI, chủ dự án đã duyệt] Dùng chung `TestActors` và các test double có sẵn trong từng project test; thư mục test phản chiếu thư mục module của code (`Catalog`, `Purchasing`...).

## Chờ chốt (chưa có hiệu lực)

Không còn mục chờ chốt.

