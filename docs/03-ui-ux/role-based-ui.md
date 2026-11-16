# UI theo vai trò

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Ma trận quyền cụ thể từng vai nằm ở [../04-domain/roles-and-permissions.md](../04-domain/roles-and-permissions.md), không lặp lại ở đây.

**Mục đích:** Cách giao diện phản ánh quyền: ai thấy nút nào, cột nào, trang nào, và code phải viết ra sao.

## 1. Cơ chế

- **`<Module>Access` (Domain) là nguồn duy nhất** quyết định vai nào được làm gì (ví dụ `PurchaseAccess`, `InventoryAccess`, `CatalogAccess`, `SupplierAccess`, `LocationAccess`, `EmployeeAccess`).
- **PageModel** chuyển quyết định đó thành thuộc tính `bool CanX` (ví dụ `CanManage`, `CanSeeCost`, `CanApproveDisposal`).
- **`.cshtml` và `_Layout` chỉ đọc `Model.CanX`.** Không gọi `User.IsInRole(...)`, không so sánh tên vai trò trong view. Menu và lối tắt trang chủ lấy từ `Navigation/NavigationMenu.cs`, điều kiện hiện là hàm `Access` (vd. `EmployeeAccess.CanList(role)`).
- **Handler dùng chính hàm `Access` đó để kiểm tra lại** (ẩn nút không phải bảo mật; xem [principles.md](principles.md) nguyên tắc 3).
- Thêm hay đổi quyền = sửa `Access` + ma trận quyền + test; không sửa rải rác ở view.

## 2. Ẩn, vô hiệu, chặn

| Tình huống | Cách hiện |
| --- | --- |
| Vai trò **không có quyền** dùng hành động | **Ẩn hẳn** nút/cột/mục menu. Không gợi ý có chức năng mà người dùng không dùng được. |
| Có quyền nhưng **sai trạng thái** (phiếu đã duyệt, hết tồn...) | **Vô hiệu** (mờ `disabled:opacity-60`) kèm `title` nêu lý do ngắn. |
| Gõ URL trực tiếp vào trang không được phép | Trả **trang 403** "Bạn không có quyền truy cập." Không đá về đăng nhập, không về trang chủ. |
| Chưa đăng nhập | Về trang đăng nhập (fallback policy). |

- Mục sidebar của module người dùng không có quyền không hiện; module chưa làm vẫn hiện badge "Sắp có" như bình thường.

## 3. Dữ liệu nhạy cảm theo vai

Áp dụng cho giá vốn, FOB, thuế, ngân hàng nhà cung cấp và các dữ liệu tương tự:

- **Handler loại dữ liệu theo vai** trước khi trả về (DTO không chứa trường đó khi thiếu quyền; trường là `null`/vắng, không phải chỉ bị ẩn). View không có gì để lộ.
- View chỉ kiểm `Model.CanSeeCost` (hoặc tương đương) để quyết định có vẽ cột/ô hay không.
- Quy tắc này ở mức handler; chưa quy định riêng cho xuất file, in, nhật ký (xem "Chờ chốt").

## 4. Mẫu code

```csharp
// PageModel: chuyển quyết định từ Access thành bool
public bool CanSeeCost => InventoryAccess.CanSeeCost(Role());
```

```cshtml
@* View: chỉ đọc bool, không biết tên vai trò *@
@if (Model.CanSeeCost)
{
    <th class="px-3 py-3 text-right font-semibold">Giá vốn</th>
}
```

## Nợ lệch so với quy tắc

| Vấn đề | Hiện trạng |
| --- | --- |
| Trang 403 | Đã có trang từ chối `/access-denied` (`Pages/Account/Denied`) và handler `Forbid()` chuyển về đó; cần kiểm tra mọi trang đều đi qua đường này khi gõ URL trực tiếp, và đổi chữ cho khớp "Bạn không có quyền truy cập." |

## Chờ chốt (chưa có hiệu lực)

- **Đã chốt 2026-11-16:** xuất CSV/Excel, in và thông báo theo cùng quy tắc loại dữ liệu nhạy cảm theo vai như màn hình; nhật ký hệ thống chỉ Admin và Giám đốc xem.
- **Đã chốt 2026-11-16:** mỗi trang có dữ liệu nhạy cảm (giá, giá vốn) phải có test tự động "vai X không thấy giá trong HTML trả về" (kiểm bằng giá trị thật, không chỉ nhãn). Đã có: Kho (giá vốn theo từng vai; CSV không vai nào có giá vốn), Mua hàng (tỷ giá/FOB theo vai), Danh mục (giá bán), Nhà cung cấp (ngân hàng). Trang mới có dữ liệu nhạy cảm phải thêm test cùng kiểu.
- **Đã chốt 2026-11-16:** giữ tên vai trò tiếng Việt hiện tại; đổi tên chỉ qua sửa mã và docs, không có màn hình đổi tên.
