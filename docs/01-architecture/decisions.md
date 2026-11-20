# Nhật ký quyết định kiến trúc (ADR)

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Mỗi quyết định có **bối cảnh, quyết định, lý do, hệ quả**. Quyết định nghiệp vụ nền "đừng đảo lại không hỏi chủ dự án".

**Cách dùng:** muốn đổi một quyết định ở đây, hỏi chủ dự án rồi thêm ADR mới thay thế (ghi "Thay thế ADR-n"), không sửa âm thầm. Đánh số tăng dần.

## A. Kiến trúc và dữ liệu

### ADR-01 — Clean Architecture bốn tầng
- **Bối cảnh:** ERP có nhiều luật nghiệp vụ phức tạp (giá vốn, kho, phân quyền) cần test được mà không cần web hay database.
- **Quyết định:** Domain ← Application ← Infrastructure ← Web; Web chỉ dùng Infrastructure để đăng ký DI.
- **Lý do:** luật nghiệp vụ độc lập framework; test nhanh; đổi hạ tầng không đụng luật.
- **Hệ quả:** nhiều lớp và file hơn; mỗi tính năng đi qua cả bốn tầng; xem [layering-and-dependencies.md](../02-coding-rules/layering-and-dependencies.md).

### ADR-02 — PostgreSQL, tên `snake_case`
- **Quyết định:** PostgreSQL qua Npgsql; `EFCore.NamingConventions` đặt tên bảng/cột `snake_case`.
- **Lý do:** miễn phí, đáng tin cậy, hợp triển khai VPS; `snake_case` là quy ước PostgreSQL.
- **Hệ quả:** thuộc tính C# vẫn `PascalCase`; migration dùng project Infrastructure làm startup.

### ADR-03 — Id là Guid v7 do ứng dụng sinh
- **Quyết định:** `Guid.CreateVersion7()` trong Domain, không để database sinh.
- **Lý do:** Id có sẵn trước khi lưu (tạo đồ thị đơn, khóa yêu cầu chống lập trùng); v7 có thứ tự theo thời gian nên chỉ mục hiệu quả.
- **Hệ quả:** cấu hình `ValueGeneratedNever` cho thực thể con; mã hiển thị chứng từ dùng 8 ký tự đầu của Id (xem [inventory.md](../04-domain/modules/inventory.md)).

### ADR-04 — Concurrency bằng `Version`, không retry
- **Quyết định:** thực thể có thể bị hai người sửa cùng lúc mang `Version` (concurrency token); lần ghi thua bị từ chối và trả lỗi Conflict.
- **Lý do:** tồn kho không được ghi đè âm thầm; tự retry có thể áp dụng hai lần.
- **Hệ quả:** UI phải báo "tải lại trang"; xem [data-access.md](../02-coding-rules/data-access.md).

### ADR-05 — Lỗi nghiệp vụ bằng `Result`, không exception
- **Quyết định:** handler trả `Result<T, TError>` với enum lỗi theo module; Domain/Application không tạo chữ hiển thị.
- **Lý do:** lỗi nghiệp vụ là kết quả bình thường, không phải ngoại lệ; tách chữ tiếng Việt khỏi lõi.
- **Hệ quả:** PageModel map mã lỗi sang câu; không `try/catch` nghiệp vụ trong trang.

### ADR-06 — Giao dịch mở tường minh bằng `IUnitOfWork` (đổi 2026-11-20)
- **Quyết định:** handler ghi dữ liệu mở một scope `IUnitOfWork`, repository chỉ đọc và staging, `CommitAsync` lưu cả dữ liệu lẫn nhật ký trong một giao dịch. Thay quyết định cũ (2026-11-16): "một phương thức repository = một giao dịch; không `IUnitOfWork`".
- **Lý do:** review của senior: giao dịch ẩn trong repository làm ranh giới giao dịch khó thấy, nhật ký ghi ngoài giao dịch có thể mất, và handler phải tự hoàn nguyên (`Restore`) đối tượng khi lưu lỗi.
- **Hệ quả:** xem [use-cases-and-results.md](../02-coding-rules/use-cases-and-results.md) mục "Giao dịch" và "Audit".

## B. Nghiệp vụ nền

### ADR-07 — Giá vốn bình quân gia quyền trên tổng tồn mọi địa điểm
- **Quyết định:** khi nhận hàng mua, giá vốn mới = `(tồn × giá hiện tại + số nhận × đơn giá vốn) ÷ (tồn + số nhận)`, tồn tính trên **tất cả** kho/showroom, làm tròn đồng (`AwayFromZero`). Điều chỉnh, kiểm kê, hủy không đổi giá vốn.
- **Lý do:** một mặt hàng một giá vốn toàn công ty; tránh ghi đè bằng đơn giá dòng cuối (lỗi từng gặp).
- **Hệ quả:** xem [purchasing.md](../04-domain/modules/purchasing.md); không sửa công thức khi test ca còn tồn.

### ADR-08 — VAT nhập khẩu tính vào giá vốn (theo quyết định của khách hàng)
- **Quyết định:** mỗi đơn có công tắc "VAT được khấu trừ"; **mặc định tắt (VAT không được khấu trừ → VAT cộng vào giá vốn)**. Được khấu trừ thì VAT không vào giá vốn.
- **Lý do:** khách hàng quyết định cách hạch toán; công tắc theo đơn để linh hoạt.
- **Hệ quả:** giá vốn dòng gồm VAT khi không khấu trừ; thuế bổ sung, tiêu thụ đặc biệt, môi trường cộng vào giá vốn.

### ADR-09 — `FobUsd` là tổng USD của dòng
- **Quyết định:** trường FOB trên dòng hàng là tổng USD của dòng, không phải đơn giá; UI nhân đơn giá × số lượng trước khi lưu.
- **Lý do:** tránh sai số làm tròn khi chia; khớp cách khai trên tờ khai.

### ADR-10 — Không tách nhiệm vụ trong luồng phê duyệt
- **Quyết định:** một người có thể lập, duyệt, xuất và nhận cùng một phiếu nếu có quyền ở mỗi bước (chuyển kho, mua hàng).
- **Lý do:** công ty nhỏ, ít người (Product Owner đã chấp nhận).
- **Hệ quả:** bù bằng lịch sử sự kiện ở mọi bước và nhật ký audit.

### ADR-11 — Tồn kho ba số, nhận hàng lệch không cộng trừ lại
- **Quyết định:** `OnHand`, `Reserved`, `InTransit`; khả dụng = `OnHand − Reserved`; duyệt phiếu chuyển giữ hàng; nhận lệch tự lập điều chỉnh "Đã ghi nhận" và không cộng trừ tồn lần nữa.
- **Lý do:** hàng đang chuyển không được bán; tránh trừ hai lần.

## C. Bảo mật và vận hành

### ADR-12 — Cookie auth và 2FA TOTP tự cài
- **Quyết định:** xác thực cookie; 2FA TOTP (RFC 6238) tự cài, tùy chọn cho mọi vai; 10 mã khôi phục băm SHA-256; khóa TOTP mã hóa bằng Data Protection.
- **Lý do:** không phụ thuộc dịch vụ trả phí; đủ cho quy mô hiện tại.
- **Hệ quả:** hiện chưa có quên mật khẩu qua email; Admin đặt lại và reset 2FA. **Cập nhật 2026-11-16:** chủ dự án chốt sẽ thêm quên mật khẩu qua email (chưa làm, chờ chọn dịch vụ gửi email).

### ADR-13 — Không seed tài khoản; Admin đầu bằng `create-admin`
- **Quyết định:** không có tài khoản mẫu trong mã nguồn; Admin đầu tiên tạo bằng lệnh `create-admin` đọc mật khẩu từ biến môi trường.
- **Lý do:** seed mật khẩu từng lọt vào lịch sử git (`AdminEmployeeSeed`); app public không được có tài khoản mặc định.

### ADR-14 — Không tự migrate lúc khởi động
- **Quyết định:** áp migration chủ động khi deploy (script/bundle), sao lưu trước.
- **Lý do:** migration lỗi không được làm ứng dụng không lên; kiểm soát dữ liệu.

### ADR-15 — Commit `site.css` đã build
- **Quyết định:** `wwwroot/css/site.css` nằm trong git; build bằng `npm run build:css` trước khi commit.
- **Lý do:** VPS không cần cài Node.
- **Hệ quả:** quên build thì giao diện thiếu class; ghi vào Definition of Done.

### ADR-16 — Thông báo trong phần mềm, không email; tự host phông
- **Quyết định:** thông báo hiển thị trong phần mềm (chuông + `/notifications`); chưa gửi email/Zalo/push. Phông chữ tự host, bỏ Google Fonts.
- **Lý do:** đơn giản, không phụ thuộc dịch vụ ngoài; giảm nguồn trong CSP.

## D. Giao diện

### ADR-17 — Razor Pages + Tailwind; code Razor hiện tại là chuẩn UI
- **Bối cảnh:** UI ban đầu chuyển từ demo React; code đã lệch demo (header không có bộ chọn kho, có badge "Sắp có", banner thay vì toast).
- **Quyết định:** code Razor hiện tại là chuẩn UI, **không** theo demo React; modal `<dialog>`; không thêm dashboard hay nhóm menu mới.
- **Hệ quả:** xem [03-ui-ux](../03-ui-ux/principles.md); docs chỉ mô tả cái đang chạy và nợ lệch.

## E. Quy trình

### ADR-18 — Docs-first nhẹ thay spec-kit
- **Quyết định:** luật được chốt trong docs module trước khi code; bỏ `specs/`, `.specify/`, skill `speckit-*`; docs là nguồn duy nhất.
- **Lý do:** nhiều nguồn (`.cursorrules`, `.mdc`, `CLAUDE.md`, spec) từng lẫn lộn và mâu thuẫn.
- **Hệ quả:** xem [../05-process](../05-process/new-module-checklist.md).

## Mẫu ADR mới

```
### ADR-nn — Tiêu đề
- Bối cảnh: …
- Quyết định: …
- Lý do: …
- Hệ quả: …
- Thay thế: ADR-xx (nếu có)
```
