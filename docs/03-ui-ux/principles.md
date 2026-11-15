# Nguyên tắc UI/UX

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16). Mục "Chờ chốt" chưa có hiệu lực.

**Mục đích:** Triết lý giao diện và những điều không được làm. Giá trị cụ thể (màu, font, cỡ) nằm ở [design-tokens.md](design-tokens.md).

## Nguồn chuẩn

- **Code Razor hiện tại là chuẩn UI.** Demo React (`PancrasERPDesign`) chỉ là tài liệu lịch sử, không dùng để phán đúng/sai. Màn mới phải giống các màn đã làm.

## Nguyên tắc đã chốt

1. **Không thêm dashboard hay nhóm menu mới** ngoài 6 nhóm sidebar cố định. Module chưa làm vào trang `ComingSoon` với badge "Sắp có".
2. **Tạo / sửa / nhập / phân bổ mở modal `<dialog>`**, không sang trang riêng. Dùng `showModal()`, không dùng thuộc tính `open` trần. Cấu trúc modal xem [components.md](components.md).
3. **Nút ẩn theo vai, nhưng handler vẫn kiểm vai.** Ẩn nút chỉ để gọn UI, không phải bảo mật. Thao tác nguy hiểm dùng hộp xác nhận chung `_ConfirmDialog` (thuộc tính `data-confirm`), không dùng `confirm()` của trình duyệt.
4. **Toàn bộ chữ trên UI là tiếng Việt.** Không lộ mã lỗi, tên enum hay stack trace. PageModel map mã lỗi sang câu tiếng Việt; route giữ tiếng Anh. **Ngoại lệ có chủ ý:** nhãn trạng thái bật/tắt giữ tiếng Anh `Disable` / `Active` cho mọi đối tượng ([glossary.md](../00-overview/glossary.md) mục 2).
5. **Dữ liệu nhạy cảm theo vai (giá vốn, FOB, thuế) không được đưa vào HTML** khi người xem không có quyền. Không chỉ ẩn bằng CSS.
6. **Mọi form POST có anti-forgery token; POST thành công thì redirect (PRG).**
7. **Trạng thái rỗng dùng partial `_EmptyState`; dấu vết bản ghi ("Cập nhật ... bởi ...") dùng partial `_AuditStamp`.** Không tự viết lại.
8. **Mobile dùng được:** dưới 1024px sidebar trượt từ trái, nền mờ; padding nội dung `p-4 sm:p-6 lg:p-8`.
9. **In ấn:** shell, nút và phần không thuộc nội dung nghiệp vụ mang class `no-print`.
10. **Truy cập cơ bản:** nút chỉ có icon có `aria-label`; ô lỗi có `aria-invalid`; modal dùng `<dialog>` gốc để Esc đóng được.
11. **JavaScript tối thiểu, thuần vanilla, không framework.** Script dài đặt ở `wwwroot/js`, không viết inline trong `.cshtml`.
12. **Kết quả sau thao tác POST dùng toast góc dưới phải** (thành công emerald, cảnh báo amber, lỗi hệ thống rose). Lỗi nhập liệu/nghiệp vụ hiện inline: dòng chữ dưới từng ô, hoặc banner `_Notice` trong form/modal. Chi tiết ở [components.md](components.md) mục 7.

## Nợ lệch so với nguyên tắc (code chưa theo)

| Nguyên tắc | Hiện trạng | Việc cần làm |
| --- | --- | --- |
| 12 (toast) | Chưa có toast; kết quả đang là banner qua `TempData` (6 loại) | Làm thành phần toast và đổi các banner |
| 11 (JS tối thiểu) | `Inventory/Index.cshtml` còn 16 `onclick` inline; `_Layout` có 2; `_ConfirmDialog` có script inline | Chuyển sang `wwwroot/js` |

## Chờ chốt (chưa có hiệu lực)

- Chỉ dùng Tailwind (3.4), không thêm thư viện CSS khác.
- Vai trò của bộ lọc live (`data-live-filter`) và quy tắc bố cục hàng lọc/tìm/đếm.
- Còn sót gì khác: sẽ rà tiếp khi viết `components.md` và `app-shell.md`.
