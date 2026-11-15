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

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| `PriceListRole` | Còn chứa `ShowroomWarehouseLead`; bỏ vai này |
| Disable | Chưa có `IsActive` trên `CatalogItem`; cần thêm cột, migration, bộ lọc "đang kinh doanh" ở danh sách chọn, nút Ngừng/Kích hoạt |
| Mã lỗi | `CatalogError`/`CatalogIssue` còn trong `Common/Result.cs`; `CatalogValidation` kiểm tra viết tay, chưa dùng FluentValidation |
| Tài liệu cũ | Spec 004 ghi "ai sửa bảng giá" khác; docs mới thay thế |

## Chờ chốt (chưa có hiệu lực)

- Giá nhà phân phối và giá dự án dùng khi bán cho ai và theo quy tắc nào (chờ spec Bán hàng/Báo giá).
- Module Combo: cấu trúc thành phần của combo (hiện chỉ có cờ `IsCombo`), cách tính tồn và giá combo.
- Lịch sử thay đổi giá (xem ai đổi giá nào từ bao nhiêu sang bao nhiêu) ngoài nhật ký audit.
- Nhập mặt hàng hàng loạt từ tệp (CSV/Excel).
