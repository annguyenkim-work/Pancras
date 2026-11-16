# Test thủ công và review

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Cách viết và chạy test thủ công, quy ước dữ liệu test, và cách làm review mã.

## 1. Bộ test case (sống)

- Test case thủ công theo màn hình nằm ở `docs/testing/cases/`. Mỗi file là một màn hình hoặc một tab, gồm 3 phần: **Chức năng**, **Phân quyền**, **Workflow**.
- Kỳ vọng viết theo docs module ([04-domain](../04-domain/roles-and-permissions.md)), không theo trí nhớ. Khi luật đổi, **cập nhật test case cùng lúc**.
- Mỗi case có: **ID** (ví dụ `PUR-13`), **mức ưu tiên** (P1 chặn phát hành / P2 quan trọng / P3 phụ), bước, kỳ vọng, cột kết quả. Bước phải đối chiếu số liệu đánh dấu 🔎.
- Ký hiệu vai trò: AD, GĐ, KT (nhóm tài chính **TC**), TK (Trưởng Showroom/Kho), ThK (Thủ kho), SA (Sales), CS (CSKH); kho gắn hậu tố (TK-A, ThK-B).
- **Kết quả mỗi đợt chạy và kế hoạch fix không lưu trong docs** (là lịch sử). Cần giữ thì lưu ngoài git.

## 2. Dữ liệu test

- Dữ liệu tạo ra khi test mang **tiền tố `ZZ Test` / `ZZT-`** (mã) và email `@pancras-test.invalid`; mỗi màn hình dùng bộ mặt hàng riêng (`ZZT-<màn hình>-…`) để chạy song song không giẫm số tồn.
- **Sao lưu DB trước khi seed** (`App_Data/backups/…`, thư mục này ngoài git).
- **Mật khẩu test chỉ ở user-secrets** (khóa `ManualTest:Password`), không ghi trong docs hay repo.
- **Seed bằng chính handler của ứng dụng** (`CreateLocationCommandHandler`, `CreateEmployeeCommandHandler`...) từ một chương trình tạm **ngoài repo**; không thêm mã seed hay mật khẩu vào repo.
- **Không đụng dữ liệu thật:** không sửa/ngừng nhân viên, kho, nhà cung cấp, mặt hàng, đơn mua không mang tiền tố test.
- **Dọn dữ liệu test:** trên DB local không bắt buộc dọn (quyết định của chủ dự án 2026-11-14). Mọi môi trường khác: không seed dữ liệu test; nếu lỡ có, dọn theo tiền tố hoặc khôi phục từ bản sao lưu (xác nhận trước khi khôi phục).
- Tài khoản test gợi nhớ: 2 Admin (case "Admin cuối cùng"), 1 Giám đốc, 2 Kế toán, Trưởng kho A và B, Thủ kho A/B/C, Sales A, CSKH, 1 tài khoản đã ngừng, 1 tài khoản đã bật 2FA; 3 kho/showroom (A, B kho; C showroom).

## 3. Test phân quyền: ba lớp

Mỗi quyền phải kiểm ở **cả ba lớp**; thiếu lớp nào cũng là lỗi:

1. **Menu / nút:** vai không có quyền thì không thấy mục menu, nút, ô nhập.
2. **Gõ URL trực tiếp:** `/trang`, `/trang?form=1`, `/trang?edit=<id>`… bị từ chối (trang 403) hoặc không mở dữ liệu.
3. **Ép request:** gửi thẳng thao tác mà giao diện không cho (sao chép request bằng DevTools hoặc sửa `action` và ô ẩn của form). Kỳ vọng: thông báo từ chối tiếng Việt, **dữ liệu không đổi**, nhật ký đúng.

Với **dữ liệu bị ẩn** (giá vốn, ngân hàng nhà cung cấp, tiền đơn mua): kiểm cả **mã nguồn trang** (Ctrl+U), tab **Network** và **file xuất**, không chỉ nhìn bằng mắt.

## 4. Chạy test

- Môi trường `http://localhost:5010` (`dotnet run --project src/Pancras.Web --launch-profile http`). Dùng hai trình duyệt hoặc hai cửa sổ ẩn danh cho case đồng thời.
- Hành vi cần PostgreSQL thật (concurrency token, giao dịch, migration) kiểm ở đây vì test tự động chỉ dùng InMemory ([testing.md](../02-coding-rules/testing.md) mục 4).
- Case giao diện/JS (modal, ô tiền, xác nhận) cần trình duyệt thật, không thay bằng gọi HTTP.

## 5. Review mã

- Review cho ra **bảng phát hiện tạm**: mã (A1, B2…), **mức Cao / Trung bình / Thấp**, vị trí, mô tả, lý do, gợi ý, **quyết định của chủ dự án** (đồng ý / không / điều chỉnh).
- **Quyết định đã chốt thì ghi vào docs module** (và "Nợ lệch" nếu chưa làm); **file review không lưu lâu dài** trong repo.
- Không có file bàn giao (handoff) trong repo. Tiến độ và việc đang làm theo dõi bằng git (commit, nhánh) và mục "Nợ lệch" trong từng file docs.
- Review kiểm: phân quyền ở handler, lỗi nghiệp vụ trả bằng `Result`, đồng thời (`Version`), giao dịch, nhật ký, quy ước UI, thiếu test.

## Nợ lệch so với quy trình

| Vấn đề | Hiện trạng |
| --- | --- |
| Test case đã rà thuật ngữ, chưa rà logic | 11 file ở `docs/testing/cases/` đã rà (2026-11-16) về thuật ngữ, nhãn nút (Disable/Active, Xuất CSV, Quay lại), `VNĐ` và các câu thông báo trích dẫn so với code. Chưa chạy lại từng case trên bản chạy thật và chưa rà từng kỳ vọng nghiệp vụ so với docs module |
| Bộ test case trỏ file đã xóa | `KE-HOACH-CHAY-TEST.md` và thư mục `ket-qua/` đã xóa theo quyết định dọn docs; các quy ước dữ liệu test nay nằm ở mục 2 trên |

## Quyết định đã chốt (2026-11-16)

- Chưa cần tự động hóa test thủ công bằng Playwright.

## Chờ chốt (chưa có hiệu lực)

- Mẫu bảng review chính thức (cột, thang mức độ) nếu muốn cố định ngoài mô tả trên.
- Nơi lưu kết quả các đợt chạy (ngoài git) nếu cần đối chiếu lâu dài.
