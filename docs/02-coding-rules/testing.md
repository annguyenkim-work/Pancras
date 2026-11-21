# Quy định test

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16).

**Mục đích:** Loại test, cách đặt tên, khi nào bắt buộc, và test dùng dữ liệu thế nào.

## 1. Dự án test

Framework xUnit. Bốn dự án, mỗi dự án test đúng một tầng:

| Dự án | Test gì | Cách làm |
| --- | --- | --- |
| `Pancras.Domain.Tests` | Thực thể, bất biến, `*Access`, tính toán (giá vốn, tồn kho, mã nhân viên, TOTP) | Không phụ thuộc gì ngoài Domain |
| `Pancras.Application.Tests` | Handler: thành công, từng mã lỗi, quyền | Fake viết tay (`FakeEmployeeStore`, `FakeInventoryStore`...), không dùng thư viện mock |
| `Pancras.Infrastructure.Tests` | Repository, dấu vết bản ghi, lưu phiếu, transaction, khóa dòng, concurrency, migration | PostgreSQL thật qua Testcontainers (mục 4) |
| `Pancras.Web.Tests` | Trang, đăng nhập, phân quyền, quét mã nguồn | `WebApplicationFactory` + EF InMemory/test double |

Test Domain, Application và Web không được phụ thuộc cơ sở dữ liệu thật hay mạng. Riêng `Infrastructure.Tests` chạy trên PostgreSQL thật (mục 4).

## 2. Đặt tên

- Tên test tiếng Anh dạng **`Subject_Behavior_Condition`**, ví dụ `Create_RejectsDuplicateCode_WithoutAddingAnotherRow`.
- Comment và dữ liệu mẫu có thể là tiếng Việt.
- Class test: `<ĐốiTượng>Tests` (ví dụ `CreateCatalogItemCommandHandlerTests`), cùng tầng với đối tượng.
- Mỗi test kiểm một hành vi; không gom nhiều hành vi không liên quan vào một test.

## 3. Khi nào bắt buộc

1. **Mọi handler mới:** có test thành công, test cho **từng mã lỗi** nó có thể trả, và test thiếu quyền.
2. **Mọi bất biến Domain** (tồn không âm, luôn còn Admin hoạt động, mỗi kho một Trưởng...) có test ở `Domain.Tests`.
3. **Sửa lỗi:** viết test tái hiện lỗi trước, thấy nó fail, rồi mới sửa. Test hồi quy đặt cạnh test của handler liên quan.
. **Test giao diện kiểm vị trí và hành vi, không chỉ sự có mặt:** lỗi phải nằm **trong** `<dialog>` hoặc toast (không chỉ "có trong trang"); link và redirect phải chứa đủ tham số lọc; lớp CSS dùng phải có trong `site.css`. Tự kiểm: bỏ tính năng đi mà test vẫn xanh thì test yếu. Hộp thoại lập phiếu thì test theo từng loại phiếu (theory), không chỉ một loại.

## 4. Cơ sở dữ liệu trong test

- `Domain.Tests`, `Application.Tests`, `Web.Tests`: chỉ dùng fake hoặc EF InMemory; không dùng PostgreSQL thật.
- **`Infrastructure.Tests` dùng PostgreSQL thật** (quyết định 2026-11-20, theo review của senior): InMemory không kiểm được transaction, `SELECT ... FOR UPDATE`, concurrency token, `ILIKE`, so sánh `Guid` hay migration, nên không đủ tin cậy cho tầng này.
- Cách chạy: gói `Testcontainers.PostgreSql` khởi động **một container cho cả lần chạy test** (cần Docker; CI `ubuntu-latest` có sẵn). Migration được áp **một lần** vào một database mẫu, mỗi test tạo database riêng bằng `CREATE DATABASE ... TEMPLATE`, nên test độc lập và vẫn kiểm được migration khớp model.
- Máy không có Docker: đặt biến môi trường `PANCRAS_TEST_POSTGRES` là chuỗi kết nối tới một máy chủ PostgreSQL (tài khoản được phép tạo database); fixture dùng máy chủ đó thay cho container và chỉ tạo/xóa các database tên `pancras_test_*`. Không ghi chuỗi này vào file trong repo.
- Phiên bản PostgreSQL của container ghim trong `PostgresFixture`, giữ cùng dòng với production.
- Hành vi cần PostgreSQL thật mà nằm ngoài `Infrastructure.Tests` (luồng nhiều người dùng, deploy) vẫn kiểm bằng checklist thủ công (`05-process/manual-testing-and-review.md`).

## 5. Luật viết thành test quét mã nguồn

Đã có: `NoNativeDialogTests` quét `.cshtml`, cấm `confirm()/alert()/prompt()`; `UiStandardsTests` quét nút (đọc cả class trong biến Razor `const string`); `MoneyInputsTests` quét ô tiền VNĐ phải có `data-vnd` (USD, phần trăm, số lượng không nằm trong danh sách tên trường tiền VNĐ); `SidebarCollapseTests` kiểm khung nội dung căn giữa có min/max width.

Quy tắc đã chốt, **làm sau** khi dọn nợ UI (chưa tự động hóa ở thời điểm này):

- Domain/Application không tham chiếu ASP.NET/EF Core; Web không dùng Infrastructure ngoài `Program.cs`.
- `.cshtml` không dùng `IsInRole`, không `onclick` inline, không dùng class màu ngoài palette.
- Tiền không `ToString` tự do ở trang; chỉ dùng hàm chung (`VndMoney`).

- **Regex trong test quét viết bằng chuỗi nguyên văn** (`@"..."`, nháy kép gấp đôi): trong chuỗi thường `""` là ký tự xóa lùi, không phải ranh giới từ, nên test không bao giờ khớp và xanh giả (đã xảy ra với `UiStandardsTests`). Mỗi test quét phải có ít nhất một ca chứng minh nó **tìm được** đối tượng (đếm số mẫu quét được, hoặc một test đối chứng).

Test quét mã nguồn mới sẽ đỏ ngay với nợ hiện có, nên cần danh sách ngoại lệ tạm có hạn xử lý.

## 6. Lệnh

```bash
dotnet test Pancras.slnx --warnaserror --nologo
dotnet test tests/Pancras.Domain.Tests --filter "FullyQualifiedName~SomeTest"
```

- Trên máy dev dùng `./scripts/test.ps1` (xem [local-setup.md](../06-operations/local-setup.md)): build sạch rồi test, tự đặt `PANCRAS_TEST_POSTGRES` từ user-secrets.
- Mỗi lần gọi `dotnet test` chỉ truyền một đường dẫn dự án (hai csproj cùng lúc lỗi MSB1008).
- Không để cảnh báo biên dịch (`--warnaserror`).

## 6b. Test double

- `Pancras.Web.Tests/TestDoubles/`: máy chủ test dùng chung (`PancrasWebFactory`) và mọi test double của test trang, mỗi module một file. Không khai báo double ở cuối file test của trang.
- `tests/Shared/`: phần logic giả dùng chung cho nhiều project test (ví dụ `FakeStockDocuments` lọc phiếu kho giống repository thật), liên kết vào từng project bằng `<Compile Include="../Shared/**/*.cs" .../>`. Sửa một chỗ cho mọi project; không chép lại logic lọc vào từng double.
- Double phải giữ dữ liệu thật như repository (thêm thì lưu, tìm để sửa thì trả bản đã lưu); trả rỗng hoặc `null` cố định làm test không phủ được luồng.

## 7. Smoke test trình duyệt (Playwright)

Chủ dự án duyệt 2026-11-21 (gói `Microsoft.Playwright`).

- Nằm trong `Pancras.Web.Tests`, lớp có `[Trait("Category", "Browser")]`. App chạy thật trên Kestrel (cổng ngẫu nhiên, `WebApplicationFactory.UseKestrel`) với cùng các test double như test trang, nên **không cần DB, không cần tài khoản thật**; đăng nhập bằng nhân viên giả tạo trong test.
- Trình duyệt: Windows dùng Microsoft Edge có sẵn (`channel: msedge`), máy khác dùng Chromium của Playwright. CI cài Chromium trước khi chạy test (`playwright.ps1 install --with-deps chromium`). Đổi bằng biến môi trường `PANCRAS_BROWSER_CHANNEL` (`msedge`, `chrome`, hoặc `chromium`).
- Chỉ viết smoke test cho hành vi mà test HTML không thấy được: JS định dạng, vị trí và kích thước trên màn hình, hộp thoại mở, cuộn trang. Mỗi lỗi UAT loại này thêm một smoke test.
- Server Kestrel và trình duyệt dùng chung cho cả lớp (`BrowserFixture`); mỗi test mở một browser context riêng. CI cache thư mục trình duyệt theo phiên bản gói (`actions/cache`).
- Chạy riêng: `dotnet test tests/Pancras.Web.Tests --filter Category=Browser`.

## Nợ lệch so với quy định

| Vấn đề | Hiện trạng |
| --- | --- |
| Test quét mã nguồn | Đã có `NoNativeDialogTests`, `UiStandardsTests`, `MoneyInputsTests`, `ListStateLinksTests`, `ListStateFormsTests`, `SidebarCollapseTests`. Chưa có test quét `IsInRole` trong view, `onclick` inline, màu ngoài palette, `ToString` tiền tự do (mục 5) |
| Tên test hồi quy | `ManualRetestRegressionTests` đặt theo đợt test thủ công, không theo handler; chuyển test về file của handler khi chạm tới |
| Đủ mã lỗi | Chưa rà từng enum lỗi xem handler nào chưa có test cho mã lỗi nào |

## Quyết định đã chốt (2026-11-16)

- Chưa đặt ngưỡng độ phủ tối thiểu.
- [Đề xuất của AI, chủ dự án đã duyệt] Dùng chung `TestActors` và các test double có sẵn trong từng project test; thư mục test phản chiếu thư mục module của code (`Catalog`, `Purchasing`...).

## Chờ chốt (chưa có hiệu lực)

Không còn mục chờ chốt.

