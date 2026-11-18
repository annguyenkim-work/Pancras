# Hướng dẫn sử dụng tích hợp (modal trợ giúp)

> Trạng thái: **Đã chốt** (UAT sprint 1, 2026-11-18). **Chưa làm** (chưa có code). Quyền theo vai lấy từ [../04-domain/roles-and-permissions.md](../04-domain/roles-and-permissions.md), không chép lại.

**Mục đích:** Mỗi màn hình nghiệp vụ có nút trợ giúp mở modal giải thích mục tiêu, chức năng, quy trình và phân quyền, để người dùng không phải hỏi người khác.

## 1. Phạm vi đợt đầu

| Màn hình | Khóa nội dung | Nút trợ giúp đặt ở |
| --- | --- | --- |
| Kho, tab Tra cứu tồn kho | `inventory.balances` | Tab đó |
| Kho, tab Chuyển kho | `inventory.transfers` | Tab đó |
| Kho, tab Kiểm kê & điều chỉnh (gồm hủy hàng) | `inventory.stocktakes` | Tab đó |
| Mua hàng | `purchasing` | Màn hình Mua hàng |

- **Chưa làm:** Trang chủ, Thông báo, Nhật ký audit, Nhân viên, Nhà cung cấp, Danh mục sản phẩm và giá, Kho & Showroom.
- **Mở rộng sau:** Báo giá, Bán hàng và các module sau thêm một khóa nội dung mỗi màn hình, không đổi cơ chế.
- Mọi người dùng vào được màn hình đều thấy nút trợ giúp.

## 2. Nội dung mỗi modal

Bốn mục, theo thứ tự:

1. **Mục tiêu:** màn hình dùng để làm gì (mục tiêu nghiệp vụ).
2. **Chức năng:** các việc làm được trong màn hình.
3. **Quy trình:** các bước chuẩn theo thứ tự, danh sách đánh số, tên nút in đậm đúng nhãn trên giao diện.
4. **Phân quyền:** bảng đủ các vai, mỗi chức năng một dòng, **đánh dấu vai của người đang xem**. Bảng này **sinh từ cùng nguồn với kiểm quyền** (hàm `Access` trong Domain), không gõ tay, để không lệch khi đổi quyền.

## 3. Định dạng và lưu trữ

- **Lưu trong code (repo):** mỗi khóa là một file nội dung; sửa nội dung đi qua pull request cùng thay đổi tính năng. **Không có CMS**, không lưu database.
- **Văn bản thuần:** tiêu đề, danh sách, in đậm tên nút. **Không hình, không video** (ảnh chụp màn hình dễ lỗi thời khi giao diện đổi).
- Tiếng Việt, giọng văn theo [formatting-and-copy.md](formatting-and-copy.md).
- **Chỉ mở khi người dùng bấm nút.** Không tự mở lần đầu, không ghi nhận ai đã đọc.
- Nội dung ban đầu do AI soạn nháp từ docs nghiệp vụ; chủ dự án duyệt và sửa.

## 4. Giao diện

- Modal theo chuẩn [components.md](components.md); đóng được bằng Esc và nút Đóng; dùng được trên điện thoại.
- Nút trợ giúp cùng một kiểu và cùng một vị trí ở mọi màn hình.
- Chỉ tải nội dung của màn hình hiện tại, không tải nội dung mọi màn hình vào mỗi trang.

## 5. Test

- Mỗi màn hình trong bảng mục 1 có đủ nội dung bốn mục; thêm màn hình mà thiếu nội dung thì test đỏ.
- Bảng phân quyền khớp hàm `Access` tương ứng.

## Chờ chốt (chưa có hiệu lực)

- Có làm hướng dẫn cho các màn hình ngoài bảng mục 1 (Nhân viên, Nhà cung cấp, Danh mục, Kho & Showroom...) không.
- Có cần hình minh họa hoặc video về sau không.
