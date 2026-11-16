# Module: Danh mục sản phẩm và giá (`/catalog`)

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Quyền theo vai: [../roles-and-permissions.md](../roles-and-permissions.md) mục 4.

**Mục đích:** Danh mục mặt hàng và bảng giá 5 mức: thuộc tính, quy tắc giá, giá lúc bán, tồn tối thiểu. Là gốc dữ liệu cho kho, mua hàng và (sau này) bán hàng, báo giá. Code: `Domain/Catalog`, `Application/Catalog`, `Infrastructure/Persistence/CatalogItemRepository.cs`, `Web/Pages/Catalog`.

## 1. Quyền

- **Admin, Giám đốc, Kế toán** vào trang, xem, thêm mặt hàng, sửa mặt hàng và sửa giá. Các vai khác không thấy menu và bị chặn khi gõ URL (xem [role-based-ui.md](../../03-ui-ux/role-based-ui.md)).
- Trưởng Showroom/Kho **không** có quyền sửa bảng giá (bỏ khỏi `PriceListRole`).
- Quyền Sales xem giá bán sẽ định nghĩa khi làm module Bán hàng/Báo giá.

## 2. Mặt hàng (`CatalogItem`)

| Trường | Quy tắc |
| --- | --- |
| Mã | Bắt buộc, tối đa 32, chuẩn hóa chữ hoa, **duy nhất** (`NormalizedCode`) |
| Tên | Bắt buộc, tối đa 200 |
| Ngành (`ProductCategory`) | Thiết bị vệ sinh, Nội thất phòng tắm, Vật liệu xây dựng |
| Thương hiệu, xuất xứ, đơn vị | Văn bản (tối đa 100 / 100 / 32) |
| Khối lượng (kg), thể tích (m³) | Không âm, tối đa 3 chữ số thập phân; **dùng để chia chi phí nhập theo cân nặng/thể tích** ([purchasing.md](purchasing.md)) |
| Chiều cao, dài, rộng (mm) | Số nguyên không âm, tùy chọn |
| Ảnh | Đường dẫn ảnh, tối đa 240 ký tự |
| Bảo hành (tháng), tồn tối thiểu | Không âm; tồn tối thiểu là định mức tồn để cảnh báo |
| Combo (`IsCombo`) | Cờ phân biệt mặt hàng combo |
| `Version` | Concurrency token |

- Mặt hàng kế thừa dấu vết bản ghi (ai ghi lần cuối, lúc nào).
- **Phiếu thêm mở với giá trị mặc định:** ngành Thiết bị vệ sinh, đơn vị Cái, xuất xứ Nhập khẩu, bảo hành 24 tháng, khối lượng 10, tồn tối thiểu 5, năm mức giá bằng 0.
- **Disable:** mặt hàng có cờ `IsActive`; ngừng thì ẩn khỏi danh sách chọn (đơn mua, phiếu kho, báo giá), vẫn giữ để xem lịch sử. Không xóa mặt hàng.

## 3. Bảng giá (5 mức)

Năm mức: **giá vốn**, **giá lẻ (niêm yết)**, **giá đại lý**, **giá nhà phân phối**, **giá dự án**.

- Giá bán (lẻ, đại lý, nhà phân phối, dự án) **đã gồm VAT** (`SellingPricesIncludeVat`); màn cài giá ghi rõ "giá đã gồm VAT".
- **Mọi giá là số đồng nguyên**, làm tròn `AwayFromZero`, không âm; tối đa `999.999.999.999.999.999` (`numeric(18,0)`).
- **Giá vốn chỉ sửa tay khi mặt hàng chưa có tồn.** Tồn lớn hơn 0 thì khóa (`CostLocked`); khi có tồn, giá vốn chỉ đổi qua nhận hàng mua theo bình quân gia quyền ([purchasing.md](purchasing.md)). Điều chỉnh, kiểm kê, hủy không đổi giá vốn.
- **Sửa giá dùng `Version`:** hai người sửa cùng lúc thì người sau bị từ chối (`VersionConflict`) và phải tải lại. **Mỗi lần sửa giá ghi audit.**
- Thêm/sửa mặt hàng và sửa giá cùng một phiếu, cùng các ô; lần lưu ghi mọi thuộc tính và năm mức giá.

## 4. Giá lúc bán

| Khách / nguồn | Giá lấy |
| --- | --- |
| Khách lẻ | Giá niêm yết (giá lẻ) |
| Đại lý | Giá đại lý |
| Nhập từ tệp báo giá | Giá trong tệp (thiếu giá hoặc giá âm thì lỗi) |
| Nhà phân phối, Dự án | **Chưa quyết định** (`UndecidedSalePrice`); không tự đoán, chờ spec Bán hàng/Báo giá |

## 5. Tra cứu

- Tìm theo mã, tên, thương hiệu (không phân biệt hoa thường, bỏ dấu theo `CatalogTextSearch`); lọc theo ngành (live filter).
- Phân trang theo chuẩn chung 20/50 ([components.md](../../03-ui-ux/components.md) mục 6).
- Hiển thị tiền theo `VNĐ` ([formatting-and-copy.md](../../03-ui-ux/formatting-and-copy.md)); xuất CSV tiền là số thuần.
- Cột giá vốn trong danh sách thuộc dữ liệu nhạy cảm: chỉ vai có quyền thấy.


## Combo (spec đã chốt 2026-11-16, chưa làm)

- **Thành phần:** combo là một danh sách mặt hàng thường kèm số lượng (vd. 1 bồn cầu + 1 lavabo). **Không lồng combo trong combo.** Thành phần phải là mặt hàng thường còn hoạt động khi lập; mỗi mặt hàng chỉ xuất hiện một lần trong một combo (muốn nhiều thì tăng số lượng).
- **Tồn:** combo **không có tồn riêng**. Số bộ bán được = nhỏ nhất của (tồn khả dụng từng thành phần ÷ số lượng trong combo), làm tròn xuống. Bán combo thì trừ tồn từng thành phần (khi có module Bán hàng). Combo không nhập kho, không kiểm kê, không chuyển kho.
- **Giá:** combo có năm mức giá nhập tay như mặt hàng (lẻ, đại lý, nhà phân phối, dự án, đã gồm VAT; cùng quy tắc làm tròn). **Giá vốn combo chỉ xem**, tự tính bằng tổng (giá vốn thành phần × số lượng); không sửa tay. Giá vốn thuộc dữ liệu nhạy cảm như mặt hàng.
- **Quyền:** tạo, sửa, sửa giá, ngừng combo: Admin, Giám đốc, Kế toán (như mặt hàng). Các vai khác không thấy.
- **Ngừng:** combo có cờ `IsActive` như mặt hàng, không xóa. Thành phần bị ngừng thì combo hiển thị cảnh báo và không bán được cho đến khi sửa.
- **Cần làm khi triển khai (chưa quyết, hỏi chủ dự án):** địa chỉ màn hình (hiện menu "Combo" ở `/combos` là "Sắp có"); cách lưu thành phần (bảng `catalog_combo_line`); ghi audit mỗi lần sửa thành phần và giá. Mọi chỗ hiện đang loại combo khỏi danh sách (`!IsCombo`: đơn mua, kho, bảng giá hiện tại) giữ nguyên.

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| Pickers | Đơn mua đang sửa có mặt hàng đã Disable thì ô chọn dòng đó không còn mặt hàng (danh sách chọn chỉ có mặt hàng Active); dòng đã lưu vẫn hiện đúng tên |
| Tài liệu cũ | Spec 004 ghi "ai sửa bảng giá" khác; docs mới thay thế |

## Chờ chốt (chưa có hiệu lực)

- Giá nhà phân phối và giá dự án dùng khi bán cho ai và theo quy tắc nào (chờ spec Bán hàng/Báo giá).
- **Đã chốt 2026-11-16 (chưa làm):** có lịch sử đổi giá (ai, khi nào, từ bao nhiêu sang bao nhiêu) xem được ngoài nhật ký audit; có nhập mặt hàng hàng loạt từ CSV (không Excel). Còn chờ chốt: cột CSV, cách xử lý dòng lỗi/trùng mã. Combo: xem mục "Combo (spec đã chốt, chưa làm)" bên dưới.
