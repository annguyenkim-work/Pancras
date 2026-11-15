# 04 – Danh mục sản phẩm và giá (`/catalog`)

Thành phần màn hình:
- Lọc **Ngành**, ô tìm "Tìm theo mã, tên hoặc thương hiệu...", xuất file, phân trang (Trước / số trang / Sau).
- Bảng: Ảnh · Mã · Tên · Ngành · Thương hiệu · Đơn vị · Giá vốn · Giá lẻ · Giá đại lý · Giá nhà phân phối · Giá dự án · nút **Chi tiết**, **Sửa**.
- Hộp thoại **Thêm sản phẩm vào danh mục** và **Sửa sản phẩm** (cùng bộ trường): Mã SKU, Thương hiệu, Đơn vị tính, Tên sản phẩm, Danh mục, Xuất xứ, Bảo hành (tháng), Giá vốn, Giá lẻ, Giá đại lý, Giá nhà phân phối, Giá dự án, Khối lượng (kg), Thể tích (m³), Định mức tồn tối thiểu, Chiều cao/dài/rộng (mm), Ảnh sản phẩm.
- Hộp thoại **Chi tiết** sản phẩm.

---

## 1. Chức năng

### 1.1 Danh sách
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| CAT-01 | P2 | Lọc theo từng Ngành | Chỉ ra sản phẩm ngành đó | ☐ |
| CAT-02 | P2 | Tìm theo mã (một phần), tên có dấu, thương hiệu | Đúng kết quả. Tìm không dấu không được hỗ trợ (ví dụ "voi sen" không ra "Vòi sen") | ☐ |
| CAT-03 | P2 | Lọc ngành + từ khóa → sang trang 2 | Bộ lọc được giữ khi chuyển trang | ☐ |
| CAT-04 | P2 | Cột tiền | Dạng `1.234.567 đ`, căn phải | ☐ |
| CAT-05 | P2 | Sản phẩm chưa có ảnh | Hiện ô giữ chỗ, không vỡ bảng | ☐ |
| CAT-06 | P2 | Xuất file khi đang lọc | File chỉ chứa dòng khớp bộ lọc; mở được bằng Excel, tiếng Việt không lỗi font | ☐ |
| CAT-07 | P2 | Bấm "Chi tiết" | Hộp thoại hiện đủ thông tin; đóng được | ☐ |

### 1.2 Thêm sản phẩm
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| CAT-10 | P1 | Nhập đủ → Lưu sản phẩm | Thêm thành công; xuất hiện trong danh sách và trong danh sách chọn hàng ở Kho, Mua hàng | ☐ |
| CAT-11 | P1 | Mã SKU trùng mã đã có (thử khác hoa/thường, thêm khoảng trắng) | Báo trùng mã ngay tại ô Mã | ☐ |
| CAT-12 | P1 | Bỏ trống lần lượt các ô bắt buộc (Mã, Tên, Danh mục, Đơn vị tính…) | Báo thiếu đúng ô | ☐ |
| CAT-13 | P2 | Số âm ở: Bảo hành, các ô giá, Khối lượng, Thể tích, Định mức tồn, Chiều cao/dài/rộng | Báo không được âm, đúng ô | ☐ |
| CAT-14 | P2 | Nhập chữ vào ô số; nhập `1.234.567` và `1,5` | Báo số không hợp lệ hoặc hiểu đúng theo kiểu Việt Nam – ghi lại | ☐ |
| CAT-15 | P2 | Chuỗi rất dài ở Mã, Tên, Thương hiệu, Xuất xứ | Báo quá dài kèm giới hạn | ☐ |
| CAT-16 | P2 | Ảnh: JPG/PNG hợp lệ; file không phải ảnh; ảnh rất lớn | Hợp lệ lưu được và hiện ở bảng; còn lại báo lỗi ô Ảnh | ☐ |
| CAT-17 | P2 | Để trống Khối lượng và Thể tích | Lưu được (sau này đơn mua chia phí theo cân/thể tích sẽ báo thiếu số đo – xem 06) | ☐ |
| CAT-18 | P2 | Lưu lỗi | Hộp thoại mở lại, dữ liệu còn nguyên | ☐ |
| CAT-19 | P2 | Giá = 0 | Ghi lại hành vi (cho phép hay không) | ☐ |

### 1.3 Sửa sản phẩm
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| CAT-20 | P1 | Sửa 4 giá bán → Lưu | Bảng cập nhật giá mới | ☐ |
| CAT-21 | P1 | Sửa tên, thương hiệu, số đo, định mức tồn | Lưu được; màn hình Kho hiện thông tin mới | ☐ |
| CAT-22 | P1 | Đổi Mã SKU thành mã đã tồn tại | Báo trùng | ☐ |
| CAT-23 | P1 | Sửa **Giá vốn** bằng tay khi sản phẩm đã có tồn kho | Bị từ chối. Ô giá vốn khóa. Giá vốn chỉ đổi qua nhập kho. Mặt hàng chưa có tồn vẫn sửa được | ☐ |
| CAT-24 | P2 | Đổi ảnh / không chọn ảnh mới | Đổi ảnh được; không chọn thì giữ ảnh cũ | ☐ |
| CAT-25 | P2 | 2 người cùng mở Sửa 1 sản phẩm, lưu lần lượt | Lưu thứ nhất thành công. Lưu thứ hai báo "Sản phẩm vừa được người khác sửa. Tải lại và thử lại." Dữ liệu giữ như lần lưu thứ nhất | ☐ |
| CAT-26 | P1 | Sau khi đổi giá | `/audit` có dòng đổi giá (ai, lúc nào, mã sản phẩm). Không ghi giá cũ và giá mới | ☐ |

---

## 2. Phân quyền

| Hành động | AD | GĐ | KT | TK | ThK | SA | CS |
|---|---|---|---|---|---|---|---|
| Vào `/catalog`, xem danh sách, chi tiết, giá vốn | ✔ | ✔ | ✔ | ✘ | ✘ | ✘ | ✘ |
| Thêm sản phẩm | ✔ | ✔ | ✔ **[xác nhận]** | – | – | – | – |
| Lưu "Sửa sản phẩm" (gồm bảng giá) | ✔ | ✔ | ✔ | (code cho phép nhưng không vào được trang) | – | – | – |

Kế toán được sửa giá và tên. Trưởng kho vẫn không vào được `/catalog`.

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| CAT-P01 | P1 | TK, ThK, SA, CS | Gõ `/catalog`, `/catalog?handler=Export` | Từ chối truy cập, không tải được file | ☐ |
| CAT-P02 | P1 | KT | Bấm "Sửa" → đổi giá lẻ → Lưu | Lưu được. `/audit` ghi "Đổi giá sản phẩm" | ☐ |
| CAT-P03 | P1 | KT | Sửa → chỉ đổi Tên (không đổi giá) → Lưu | Lưu được | ☐ |
| CAT-P04 | P1 | KT | Thêm sản phẩm mới có nhập giá | Tạo được | ☐ |
| CAT-P05 | P1 | AD, GĐ | Thêm, sửa, đổi giá | Làm được | ☐ |
| CAT-P06 | P1 | TK | Ép request POST `UpdatePrices` (không vào được trang) | Bị từ chối ở tầng trang | ☐ |
| CAT-P07 | P1 | SA, ThK, TK | Giá vốn có lộ qua màn hình khác không (Kho → chi tiết mặt hàng; đơn mua) | Không lộ | ☐ |

---

## 3. Workflow liên màn hình

| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| CAT-W01 | P1 | Thêm sản phẩm mới → lập đơn mua có sản phẩm đó → nhập kho → quay lại Danh mục | Giá vốn ở Danh mục = giá vốn bình quân sau nhập | ☐ |
| CAT-W02 | P1 | Sản phẩm thiếu Khối lượng → lập đơn mua chia phí "Theo khối lượng" | Đơn mua báo "Có mặt hàng chưa khai báo thể tích hoặc cân nặng…". Bổ sung Khối lượng ở Danh mục rồi tạo lại thì lưu được | ☐ |
| CAT-W03 | P2 | Đặt "Định mức tồn tối thiểu" = 10, tồn thực = 5 | Màn hình Kho có dấu hiệu cảnh báo dưới định mức (nếu có) – ghi lại | ☐ |
| CAT-W04 | P2 | Đổi tên/mã sản phẩm đang nằm trong phiếu chuyển, đơn mua cũ | Phiếu cũ hiển thị tên/mã mới nhất quán, không lỗi | ☐ |
