# Thuật ngữ

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Từ điển thống nhất Việt–Anh cho code, cơ sở dữ liệu và giao diện. **Một khái niệm một cách gọi.** Khi viết giao diện và docs mới, dùng đúng cột "Gọi là"; không dùng cột "Không dùng".

## 1. Người và vai trò

| Gọi là (tiếng Việt) | Trong code | Ghi chú |
| --- | --- | --- |
| Nhân viên | `Employee` | Người có tài khoản đăng nhập |
| Mã nhân viên | `Code`, `NormalizedCode` | Server sinh từ tên (`AnNK`); không sửa |
| Vai trò | `Role` (`EmployeeRole`) | Admin, Giám đốc, Kế toán, Sales, Thủ kho, Trưởng Showroom/Kho, CSKH |
| Tài chính | `InventoryRoles.IsFinance` | Nhóm gọi chung Admin, Giám đốc, Kế toán |
| Người thao tác | `EmployeeActor` | Người đang đăng nhập; lấy từ phiên, không từ form |

## 2. Trạng thái bật/tắt (thống nhất cho mọi đối tượng)

Một cách gọi cho mọi đối tượng (nhân viên, nhà cung cấp, mặt hàng, kho/showroom):

| Gọi là | Trong code | Ghi chú |
| --- | --- | --- |
| **Disable** | `IsActive = false` | Nhãn trên giao diện giữ tiếng Anh `Disable`. Dữ liệu và lịch sử giữ nguyên, không xóa |
| **Active** | `IsActive = true` | Nhãn giao diện `Active`; dùng cả khi bật lại |
| Bật lại | | Hành động đưa về `Active` |

- Đây là **ngoại lệ có chủ ý** cho nguyên tắc "toàn bộ chữ trên UI là tiếng Việt" ([principles.md](../03-ui-ux/principles.md)). Tài liệu mô tả khái niệm có thể viết "vô hiệu hóa".
- Không dùng: "Ngừng hợp tác", "Ngừng kinh doanh", "Xóa mềm", "Khóa" (khóa là việc khác, xem mục 7) làm tên trạng thái.

## 3. Danh mục và giá

| Gọi là | Trong code | Ghi chú |
| --- | --- | --- |
| Mặt hàng | `CatalogItem` | |
| Mã SKU / mã mặt hàng | `Code` | |
| Ngành hàng | `ProductCategory` | Thiết bị vệ sinh, Nội thất phòng tắm, Vật liệu xây dựng |
| Giá vốn | `CostPrice` | Bình quân gia quyền; vai tài chính mới thấy |
| Giá niêm yết / giá lẻ | `RetailPrice` | Đã gồm VAT |
| Giá đại lý | `DealerPrice` | |
| Giá nhà phân phối | `DistributorPrice` | Chưa quyết định cách dùng khi bán |
| Giá dự án | `ProjectPrice` | Chưa quyết định cách dùng khi bán |
| Tồn tối thiểu | `MinimumStock` | Định mức tồn |
| Combo | `IsCombo` | Mặt hàng ghép; module chưa làm |

## 4. Kho

| Gọi là | Trong code | Ghi chú |
| --- | --- | --- |
| Kho, showroom (địa điểm) | `StockLocation` (`StockLocationKind`) | |
| Tồn thực tế | `OnHand` | |
| Đang giữ | `Reserved` | Đã duyệt phiếu chuyển, chưa xuất |
| Khả dụng | `Available = OnHand − Reserved` | UI không hiện số âm |
| Đang chuyển | `InTransit` | Không tính vào khả dụng của kho nào |
| Phiếu đề nghị chuyển kho | `TransferSlip` | Mã `CK-xxxxxxxx` |
| Kho xuất | `SourceLocation` | |
| Nơi nhận | `DestinationLocation` | |
| Nhận có chênh lệch | `ReceivedWithDiscrepancy` | |
| Kiểm kê (biên bản kiểm kê) | `StocktakeSheet` | |
| Phiếu điều chỉnh (số lượng) | `QuantityAdjustment` | |
| Phiếu hủy | `DisposalSlip` | |
| Dòng biến động tồn | `StockMovement` | Receipt, Issue, Transfer, Adjustment |
| Ghi thẳng | | Vai tài chính tạo chứng từ không qua duyệt |

**Không dùng:** "đề xuất chuyển hàng", "kho đích", "điểm tiếp nhận", "kho đi/kho đến".

## 5. Mua hàng

| Gọi là | Trong code | Ghi chú |
| --- | --- | --- |
| Nhà cung cấp (NCC) | `Supplier` | |
| Đơn mua | `PurchaseOrder` | "Đơn mua hàng & container" |
| Kho nhận | `ReceiveLocation` | Kho của đơn mua |
| FOB (USD) | `FobUsd` | **Tổng USD của dòng**, không phải đơn giá |
| Tỷ giá | `ExchangeRate` | Kèm ngày và nguồn |
| Trị giá hải quan | | FOB quy đổi + điều chỉnh trị giá hải quan |
| Giá tính VAT | | Trị giá hải quan + thuế nhập khẩu + thuế khác |
| VAT được khấu trừ | `VatDeductible` | Mặc định tắt: VAT vào giá vốn |
| Giá vốn dòng / đơn giá vốn | `LandedCost`, `UnitCost` | Landed cost: giá vốn nhập khẩu |
| Chi phí (khoản chi phí) | `PurchaseCharge` | Chia theo giá trị FOB, số lượng, cân nặng, thể tích, nhập tay |
| Nhóm chi phí | `ChargeGroup` | Điều chỉnh trị giá hải quan, Logistics, Thuế được khấu trừ, Thuế không được khấu trừ |
| Nhận hàng | `ReceiveAsync` | Nút ghi **"Nhập Kho & Cập Nhật Giá Vốn"** (test cấm chuỗi `Nhập kho` trần) |
| Điều chỉnh chi phí | `PurchaseCostAdjustment` | Sau khi đã nhận đủ |
| Chứng từ (đính kèm) | `PurchaseAttachment` | Tờ khai, hóa đơn NCC, vận đơn, packing list, hóa đơn logistics |

## 6. Hệ thống

| Gọi là | Trong code | Ghi chú |
| --- | --- | --- |
| Nhật ký hệ thống (audit) | `AuditEntry`, `IAuditLog` | Mã hành động `nhóm.sự_kiện` |
| Dấu vết bản ghi | `AuditStamped` | Ai ghi lần cuối, lúc nào |
| Thông báo | `Notification` | Trong phần mềm |
| Security stamp | `SecurityStamp` | Đổi thì phiên cũ hết hiệu lực |
| Xác thực hai bước (2FA) | `TwoFactorEnabled`, TOTP | Tùy chọn mọi vai |
| Mã khôi phục | `RecoveryCodeHashes` | 10 mã dùng một lần |
| Use case / handler | `*CommandHandler`, `*Handler` | Xem [naming.md](../02-coding-rules/naming.md) |

## 7. Khóa

- **Khóa tài khoản** (`LockedUntil`): tạm thời sau 5 lần đăng nhập sai; khác với **Disable**.
- **Giá vốn bị khóa** (`CostLocked`): không sửa tay khi mặt hàng có tồn.

## 8. Tiền và định dạng

| Quy ước | |
| --- | --- |
| VND | Hậu tố **`VNĐ`**, ví dụ `1.234.567 VNĐ` |
| USD | Hậu tố `USD`, ví dụ `1.234,56 USD` |
| Ngày giờ | `dd/MM/yyyy HH:mm`, giờ Việt Nam |

Chi tiết ở [formatting-and-copy.md](../03-ui-ux/formatting-and-copy.md). Dòng cấm `VND/VNĐ` trong `CLAUDE.md` cũ **không còn hiệu lực**.

## Chờ chốt (chưa có hiệu lực)

- Thuật ngữ cho module Bán hàng, Báo giá, Công nợ, Bảo hành khi làm các module đó.
- Nhãn trạng thái đơn mua và phiếu chuyển kho bằng tiếng Việt (bảng ánh xạ trạng thái → chữ) nên ghi ở đây khi chốt.
