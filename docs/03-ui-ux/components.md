# Thư viện thành phần

> Trạng thái: **Đã chốt một phần** (phiên 2026-11-16). Mục "Chờ chốt" chưa có hiệu lực.

**Mục đích:** Mẫu HTML/Tailwind chuẩn cho từng thành phần. Màn mới copy mẫu ở đây, không tự chế biến thể. Giá trị màu/font/bo góc xem [design-tokens.md](design-tokens.md).

## 1. Nút

Mọi nút `rounded-lg`. Hai cỡ.

| Cỡ | Class nền tảng | Dùng cho |
| --- | --- | --- |
| Chuẩn | `px-4 py-2 text-sm font-bold` | Nút form, nút hành động của màn |
| Nhỏ | `px-3 py-1.5 text-xs font-bold` | Nút trong dòng bảng, thanh công cụ |

| Biến thể | Màu | Dùng cho |
| --- | --- | --- |
| Chính | `bg-sky-600 text-white hover:bg-sky-700` | Hành động chính (Lưu, Tạo) |
| Phụ | `bg-slate-100 text-slate-700 hover:bg-slate-200` | Hủy, Quay lại, hành động thứ yếu |
| Nguy hiểm | `bg-rose-600 text-white hover:bg-rose-700` | Hủy phiếu, vô hiệu hóa; luôn đi kèm `_ConfirmDialog` |
| Hoàn tất | `bg-emerald-600 text-white hover:bg-emerald-700` | Duyệt, xác nhận hoàn thành |
| Phân loại | `bg-violet-600 text-white hover:bg-violet-700` | Hành động riêng của một loại nghiệp vụ (vd. Kiểm kê) |

- Nút vô hiệu: thêm `disabled:opacity-60`.
- Nút chỉ có icon phải có `aria-label`.
- Mỗi màn có đúng một nút Chính nổi bật.

```html
<button type="submit" class="rounded-lg bg-sky-600 px-4 py-2 text-sm font-bold text-white hover:bg-sky-700 disabled:opacity-60">Lưu</button>
<button type="button" class="rounded-lg bg-slate-100 px-4 py-2 text-sm font-bold text-slate-700 hover:bg-slate-200">Quay lại</button>
```

## 2. Form

- Nhãn: `block text-sm font-medium text-slate-700`.
- Ô nhập / chọn: `mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-sky-600 focus:outline-none`.
- Lỗi: ô thêm `border-rose-500` + `aria-invalid="true"`; câu lỗi `mt-1 text-xs font-medium text-rose-700` ngay dưới ô.
- Ô tiền: nhận số thô khi sửa, hiện số nhóm theo `vi-VN` khi rời ô (xem `formatting-and-copy.md`).
- Dùng một nguồn class chung (`inputClass`, `labelClass`), không viết tay lại ở từng trang.

## 3. Modal (`<dialog>`)

Mở bằng `showModal()`. Khung là cột flex, chỉ thân cuộn.

Bốn cỡ rộng (S, M, L, XL), đều `w-[min(<rộng>,calc(100%-2rem))]`. Hộp thoại luôn có trong trang dùng `open:flex` thay cho `!flex` (nếu không hộp đóng cũng hiện); hộp chỉ dựng khi cần mở thì dùng `!flex`:

| Cỡ | Rộng | Dùng cho |
| --- | --- | --- |
| S | `28rem` | Xác nhận, form 1–3 ô |
| M | `40rem` | Form thường |
| L | `56rem` | Form nhiều cột hoặc bảng dòng |
| XL | `72rem` | Form lập đơn mua (bảng dòng và chi phí) |

```html
<dialog class="m-auto !flex w-[min(40rem,calc(100%-2rem))] max-h-[calc(100%-2rem)] flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white p-0 shadow-xl backdrop:bg-slate-900/50">
  <div class="flex shrink-0 items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
    <h2 class="text-base font-bold text-slate-900">Tiêu đề</h2>
    <button type="button" class="grid h-8 w-8 place-items-center rounded-lg text-2xl leading-none text-slate-400 hover:bg-slate-100 hover:text-slate-700" aria-label="Đóng">×</button>
  </div>
  <form class="min-h-0 flex-1 overflow-y-auto px-5 py-4">...</form>
  <div class="flex shrink-0 justify-end gap-2 border-t border-slate-100 px-5 py-4">...</div>
</dialog>
```

- Header (tiêu đề + nút đóng) và footer nút là `shrink-0`, nằm ngoài vùng cuộn.
- Hộp xác nhận chung: partial `_ConfirmDialog`, kích hoạt bằng `data-confirm` trên form (`data-confirm-title`, `-message`, `-label`, `-tone="danger"`).

## 4. Trang, thẻ, bảng

- Tiêu đề trang: `font-display text-xl sm:text-2xl font-bold text-slate-900`.
- Thẻ: `rounded-xl border border-slate-200 bg-white`.
- Bảng: bọc `overflow-x-auto`; `thead` `bg-slate-50 text-xs uppercase tracking-wide text-slate-500`; dòng `border-t border-slate-100`; ô `px-3 py-3`; tiền và số lượng căn phải `font-mono tabular-nums`; dòng hover `hover:bg-slate-50`.
- **Bảng trên mobile:** giữ nguyên cấu trúc cột, không ẩn cột, không chuyển thẻ. Bọc `overflow-x-auto`, bảng `whitespace-nowrap`, cột đầu (STT, mã hoặc tên) `sticky left-0 bg-white` (kèm `z-10`; dòng hover/chọn đổi nền cho cả ô dính). Cột hành động trong dòng đứng ở vị trí **thứ 2**, ngay sau cột STT/mã/tên, để luôn thấy mà không phải cuộn.
- Trạng thái rỗng: partial `_EmptyState`. Dấu vết bản ghi: partial `_AuditStamp`.
- Hàng lọc/tìm: form GET có `data-live-filter` (script `live-filter.js`), vùng kết quả bọc trong `<div id="live-results">`; không cần nút Lọc.

## 5. Tab

- Nhóm tab trong viên thuốc `inline-flex rounded-lg bg-slate-100 p-1`.
- Tab: `rounded-md px-3 py-1.5 text-sm font-medium text-slate-600`; tab đang chọn `bg-white text-slate-900 shadow-sm`.
- Markup `role="tablist"`, `role="tab"`, `aria-selected`. Chuyển tab bằng query string để link/F5 giữ nguyên tab.

## 6. Phân trang

- Mặc định 20 dòng/trang; người dùng chọn được 20 / 50.
- Thanh dưới bảng: "Hiển thị X–Y / Z", nút Trước / Sau (nút Phụ cỡ nhỏ), số trang hiện tại; giữ nguyên bộ lọc trong query string.
- Phân trang làm ở truy vấn (Skip/Take), không cắt trong bộ nhớ.
- Danh sách chắc chắn nhỏ (vd. 6 nhóm menu, vai trò) không cần phân trang.

## 7. Thông báo: toast và banner

**Toast** — kết quả sau một thao tác POST (lưu xong, đã duyệt...):
- Góc dưới phải, `fixed bottom-4 right-4 z-[60]`, rộng tối đa `24rem`, nhiều toast xếp chồng. Mang `no-print`.
- Thành công (emerald) tự tắt sau 5 giây. Cảnh báo (amber) và lỗi hệ thống (rose) phải bấm `×` mới tắt.
- `role="status"` cho thành công, `role="alert"` cho cảnh báo/lỗi; luôn có nút `×` với `aria-label="Đóng"`.
- Một khóa `TempData` duy nhất mang cả mức độ lẫn nội dung; `_Layout` render toast. Trang không tự render banner kết quả.

**Banner trong trang/form** — lỗi nghiệp vụ hoặc cảnh báo cần người dùng đọc ngay tại chỗ (thường trong modal hoặc đầu form):
- Khung chung: `rounded-lg border border-{màu}-200 bg-{màu}-50 px-4 py-3 text-sm font-medium text-{màu}-800`.
- Ba tông: rose (lỗi, `role="alert"`), amber (cảnh báo, `role="alert"`), emerald (thông tin thành công đặc biệt như mã khôi phục, `role="status"`).
- Làm partial `_Notice` dùng chung.
- Lỗi từng ô không dùng banner: dòng chữ `text-xs font-medium text-rose-700` ngay dưới ô.

## 8. Badge

Khung chung cho badge trạng thái và badge phân loại: `rounded-full px-2 py-0.5 text-xs font-bold` + nền `-100`, chữ `-800` của một tông.

| Tông | Ý nghĩa | Trạng thái hiện có |
| --- | --- | --- |
| slate | Trung tính | Nháp, Đã hủy |
| amber | Chờ / cần chú ý | Chờ duyệt (Requested, PendingApproval), Nhận có chênh lệch, Trả về (Returned) |
| sky | Đang tiến hành | Đã duyệt, Đang vận chuyển, Nhận một phần |
| emerald | Hoàn tất | Đã nhận |
| rose | Từ chối / lỗi / xuất hủy | Từ chối, Xuất hủy |
| violet | Chỉ phân loại, không phải trạng thái | Loại Showroom |

- Không dùng yellow, orange hay màu ngoài palette.
- Domain/Application không biết class CSS. Ở Web có một helper duy nhất ánh xạ tông (`Neutral / Info / Warning / Success / Danger`) sang class; mỗi enum trạng thái chỉ ánh xạ sang tông.
- Chữ trên badge là tiếng Việt lấy từ một chỗ ánh xạ, không viết lại ở từng trang.

## 9. Chống bấm đúp khi gửi form

- **Client:** script chung `wwwroot/js/form-guard.js`. Khi một form POST được gửi: vô hiệu mọi nút submit của form, đổi nhãn nút thành "Đang xử lý…", gắn `aria-busy="true"`; mở lại khi trang hiện lại từ cache (`pageshow`). Không tự viết xử lý riêng ở từng trang. Form xác nhận qua `_ConfirmDialog` dùng chung cơ chế này.
- **Server:** lệnh quan trọng (nhận hàng, xuất kho, duyệt, tạo phiếu) phải an toàn khi bị gửi lặp: kiểm tra trạng thái hiện tại hoặc concurrency token, gửi hai lần không được ghi hai lần. Chặn ở client chỉ để tiện, không thay thế kiểm tra ở server.

## Nợ lệch so với components

| Thành phần | Hiện trạng |
| --- | --- |
| Bảng mobile | Cột đầu sticky và cột Thao tác ở vị trí 2 đã có ở Nhà cung cấp, Danh mục, Nhân viên, bảng phiếu chuyển; còn các bảng chi tiết nhỏ trong thẻ (không cần) |
| Chống bấm đúp | `form-guard.js` chưa kiểm thử trên trình duyệt thật (hộp xác nhận, form tải tệp) |
| Tab | Đã có partial `_Tabs` (viên thuốc, `role=tablist`) dùng ở Kho; Mua hàng và Danh mục chưa có tab |
| Phân trang | Partial `_Pager` (20/50, "Hiển thị X–Y / Z", giữ bộ lọc) ở Danh mục, Nhà cung cấp, Nhân viên, Nhật ký, Thông báo, danh sách đơn mua và phiếu chuyển. Thông báo cắt trang ở truy vấn (cả số chưa xem trên chuông). Nhân viên, đơn mua và phiếu chuyển còn cắt trang trong bộ nhớ: sắp xếp và tìm của chúng dựa vào tên tra chéo bảng khác (kho, nhà cung cấp) và tính toán trong Domain, nên chuyển xuống SQL là việc lớn hơn; dữ liệu các danh sách này nhỏ. Chờ chủ dự án quyết định có chấp nhận hay không. Lịch sử tồn của mặt hàng giữ bộ chọn cỡ trang riêng |
| Sắp xếp theo cột | Đã có (`_SortHeader`, `ListSort`) ở Nhà cung cấp, Danh mục, Nhân viên; Nhật ký sắp theo thời điểm. Chưa có ở đơn mua (thẻ), phiếu chuyển |
| Toast, banner | Toast sau thao tác POST ở Catalog, Locations, Purchasing, Suppliers, Profile; lỗi cần giữ form dùng `_Notice` ở mọi trang (kể cả Kho). Chưa chuyển lỗi của Kho sang toast vì lỗi đi cùng hộp thoại đang mở |
| Badge | Đã dùng `Badge` ở mọi bảng và danh sách, kể cả chip kho và bộ đếm của Kho |

## Quyết định đã chốt (2026-11-16)

- [Đã làm] Khi đang tìm, vùng kết quả của bộ lọc live mờ đi, `aria-busy` và con trỏ chờ.

## Chờ chốt (chưa có hiệu lực)

Không còn mục chờ chốt.

