# Định nghĩa hoàn thành (Definition of Done)

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Điều kiện để coi một việc là **xong** và được commit/merge.

## Điều kiện bắt buộc

- [ ] **Luật đã chốt trong docs.** Docs module phản ánh đúng hành vi; không còn quyết định nằm trong chat hay trong đầu.
- [ ] **Build sạch:** `dotnet build Pancras.slnx --warnaserror` không cảnh báo.
- [ ] **Test xanh:** `dotnet test Pancras.slnx --warnaserror --nologo`. Có test mới theo [testing.md](../02-coding-rules/testing.md) (handler mới: thành công, từng mã lỗi, thiếu quyền; bất biến Domain; lỗi sửa thì có test hồi quy; trang phân quyền).
- [ ] **UI đúng chuẩn:** modal, bảng, nút, toast, định dạng VNĐ/ngày theo `03-ui-ux`; không tạo nợ UI mới (inline JS, màu ngoài palette, `IsInRole` trong view).
- [ ] **CSS đã build:** đổi class thì chạy `npm run build:css` và commit `site.css`.
- [ ] **Phân quyền ba lớp:** menu/nút, gõ URL trực tiếp, ép request; dữ liệu nhạy cảm không nằm trong HTML ([role-based-ui.md](../03-ui-ux/role-based-ui.md)).
- [ ] **An toàn dữ liệu:** có `Version` nếu hai người sửa cùng lúc; thao tác nhiều bước nằm trong một phương thức repository (một giao dịch); gửi lặp không ghi hai lần.
- [ ] **Migration** (nếu có) đã tạo, đã áp lên DB local, nêu rõ khi có cột/bảng bị xóa.
- [ ] **Nhật ký:** thao tác ghi dữ liệu và sự kiện bảo mật có ghi audit; không ghi mật khẩu/mã/PII.
- [ ] **Test thủ công** của module đã cập nhật và chạy các case P1 liên quan; lỗi tìm thấy được sửa hoặc ghi rõ.
- [ ] **Không có secret** (mật khẩu, chuỗi kết nối, token) trong thay đổi.
- [ ] **Docs cập nhật:** trạng thái module ở `00-overview/product.md`, mục "Nợ lệch" của các file bị ảnh hưởng (xóa dòng đã xử lý, thêm dòng mới nếu có).

## Không tính là xong

- Chỉ chụp màn hình một màn hình mà chưa kiểm hành vi.
- Test pass nhưng cảnh báo biên dịch còn.
- Còn `[CHỜ CHỐT]` ảnh hưởng đến hành vi vừa làm mà chưa được hỏi chủ dự án.

## Báo cáo khi xong

Nêu ngắn: đã làm gì, test nào chạy và kết quả, điều gì **chưa** kiểm hoặc chưa làm, nợ mới phát sinh.
