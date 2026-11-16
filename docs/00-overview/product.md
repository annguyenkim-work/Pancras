# Tổng quan sản phẩm

> Trạng thái: **Đã chốt** (phiên 2026-11-16).

**Mục đích:** Nói ngắn phần mềm này làm gì, cho ai, đang ở đâu, và những gì không nằm trong phạm vi.

## 1. PancrasERP là gì

PancrasERP là phần mềm ERP tiếng Việt cho doanh nghiệp thương mại thiết bị vệ sinh và nội thất, có nhập khẩu container. Phục vụ nhân viên **Admin, Giám đốc, Kế toán, Trưởng Showroom/Kho, Thủ kho, Sales, CSKH**. Giai đoạn đầu quản lý: nhân sự và phân quyền, danh mục sản phẩm và giá, nhà cung cấp nhập khẩu, mua hàng container và giá vốn, kho và điều chuyển giữa kho/showroom. Các module bán hàng, báo giá, công nợ, bảo hành làm sau.

- Giao diện hoàn toàn tiếng Việt; URL tính năng bằng tiếng Anh (`/inventory`, `/purchasing`, `/employees`, `/catalog`, `/suppliers`, `/locations`, `/audit`).
- Công nghệ và kiến trúc: [../01-architecture/overview.md](../01-architecture/overview.md), [../01-architecture/tech-stack.md](../01-architecture/tech-stack.md).

## 2. Người dùng

Bảy vai trò và phạm vi dữ liệu: [../04-domain/roles-and-permissions.md](../04-domain/roles-and-permissions.md).

## 3. Module và trạng thái

Sáu nhóm menu cố định (xem [../03-ui-ux/app-shell.md](../03-ui-ux/app-shell.md)). Trạng thái tính đến 2026-11-16:

| Nhóm menu | Module | Route | Trạng thái | Docs |
| --- | --- | --- | --- | --- |
| Hệ thống | Tổng quan điều hành (lối tắt theo vai) | `/` | Đã làm | [app-shell.md](../03-ui-ux/app-shell.md) |
| Hệ thống | Tài khoản (đăng nhập, 2FA, hồ sơ) | `/login`, `/profile` | Đã làm | [accounts-and-employees.md](../04-domain/modules/accounts-and-employees.md) |
| Vận hành kho bãi | Kho & điều chuyển | `/inventory` | Đã làm | [inventory.md](../04-domain/modules/inventory.md) |
| Vận hành kho bãi | Mua hàng & container nhập | `/purchasing` | Đã làm | [purchasing.md](../04-domain/modules/purchasing.md) |
| Vận hành kho bãi | Nhà cung cấp nhập khẩu | `/suppliers` | Đã làm | [suppliers.md](../04-domain/modules/suppliers.md) |
| Kinh doanh & phân phối | Danh mục sản phẩm & giá | `/catalog` | Đã làm | [catalog.md](../04-domain/modules/catalog.md) |
| Kinh doanh & phân phối | Danh bạ khách hàng | `/customers` | Chưa làm ("Sắp có") | — |
| Kinh doanh & phân phối | Bán hàng (lẻ, đại lý, dự án) | `/sales` | Chưa làm ("Sắp có") | — |
| Kinh doanh & phân phối | Báo giá & catalog | `/quotes` | Chưa làm ("Sắp có") | — |
| Kinh doanh & phân phối | Quản lý combo sản phẩm | `/combos` | Chưa làm ("Sắp có") | — |
| Tài chính | Công nợ & hạn mức | `/receivables` | Chưa làm ("Sắp có") | — |
| Dịch vụ khách hàng | Bảo hành điện tử | `/warranty` | Chưa làm ("Sắp có") | — |
| Quản trị nội bộ | Nhân sự & phân quyền | `/employees` | Đã làm | [accounts-and-employees.md](../04-domain/modules/accounts-and-employees.md) |
| Quản trị nội bộ | Kho & Showroom (địa điểm) | `/locations` | Đã làm | [inventory.md](../04-domain/modules/inventory.md) |
| Quản trị nội bộ | Nhật ký hệ thống | `/audit` | Đã làm (cơ bản) | [notifications-and-audit.md](../04-domain/modules/notifications-and-audit.md) |
| (xuyên module) | Thông báo | `/notifications` (dự kiến) | Mới có cho kho | [notifications-and-audit.md](../04-domain/modules/notifications-and-audit.md) |

- Module chưa làm dùng chung trang `ComingSoon` ("Chức năng đang được triển khai") và nhãn "Sắp có" ở menu.
- Quyền của module chưa làm **định nghĩa khi làm module đó**, không đoán trước ([roles-and-permissions.md](../04-domain/roles-and-permissions.md)).

## 4. Ngoài phạm vi

- **Bán hàng trực tuyến (website, sàn thương mại điện tử)**: không thuộc phạm vi, không dự định làm.

## 5. Chưa làm, chờ chốt

Các nhóm sau **chưa làm, chưa quyết định phạm vi**, không đoán; sẽ có docs khi chủ dự án chốt: kế toán sổ sách và xuất hóa đơn điện tử, thanh toán và công nợ phải trả nhà cung cấp, khách hàng/bán hàng/báo giá/combo/công nợ phải thu/bảo hành (đã có chỗ trong menu).

## Quyết định đã chốt (2026-11-16)

- Chưa cần ghi thông tin quy mô doanh nghiệp vào docs.
- Thứ tự làm module "Sắp có": Khách hàng → Bán hàng → Công nợ phải thu; sau đó Báo giá, Combo, Bảo hành (thứ tự ba mục sau chưa chốt).

## Chờ chốt (chưa có hiệu lực)

Không còn mục chờ chốt.

