# 07 – Kho: tab "Tra cứu tồn kho" (`/inventory`)

Trang "Quản Lý Đa Kho & Điều Chuyển Hàng Hóa" có 3 tab: **Tra cứu tồn kho** (file này), **Chuyển kho** ([08](08-kho-chuyen-kho.md)), **Kiểm kê & điều chỉnh** ([09](09-kho-kiem-ke-dieu-chinh-huy.md)).

Thành phần tab Tra cứu tồn kho:
- Khối **Thông báo kho** (nếu có) – mỗi thông báo có nút "Mở phiếu".
- Bộ lọc: ô tìm "Tìm theo mã SKU, tên thiết bị, thương hiệu...", chọn **Kho hoặc showroom**, chọn **Ngành hàng**, nút **Xuất CSV**.
- Bảng: Mã SKU · Tên hàng hóa & Quy cách · ĐVT · **Giá vốn niên yết** (chỉ vai trò tài chính) · Tồn thực tế · Đang giữ chỗ · Khả dụng · Đang chuyển · Tồn tại các điểm · Thao tác.
- Khung **chi tiết mặt hàng**: Thông tin hàng hóa, Tồn theo điểm (Điểm, Tồn, Giữ chỗ, Khả dụng, Đang chuyển tới), Lịch sử thay đổi.

Định nghĩa cần nhớ khi đối chiếu:
- **Khả dụng = Tồn thực tế − Đang giữ chỗ.**
- **Đang giữ chỗ**: hàng của phiếu chuyển đã duyệt nhưng chưa xuất kho (tính ở kho xuất).
- **Đang chuyển**: hàng đã xuất kho, chưa được kho nhận xác nhận (tính ở kho nhận).

---

## 1. Chức năng

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| BAL-01 | P1 | Mở tab | Mỗi mặt hàng 1 dòng; cột "Tồn tại các điểm" liệt kê từng kho/showroom | ☐ |
| BAL-02 | P1 | Cộng tay tồn từng điểm của 1 mặt hàng | = Tồn thực tế | ☐ |
| BAL-03 | P1 | Khả dụng | = Tồn thực tế − Đang giữ chỗ ở mọi dòng | ☐ |
| BAL-04 | P2 | Tìm theo mã SKU, tên có dấu/không dấu, thương hiệu | Đúng kết quả | ☐ |
| BAL-05 | P2 | Lọc 1 kho | Các cột số chỉ tính riêng kho đó | ☐ |
| BAL-06 | P2 | Lọc Ngành hàng; kết hợp với kho và từ khóa | Đúng | ☐ |
| BAL-07 | P2 | Mặt hàng chưa từng nhập | Tồn 0 hoặc không hiện – ghi lại; không lỗi | ☐ |
| BAL-08 | P1 | Không dòng nào có Tồn, Giữ chỗ, Khả dụng, Đang chuyển **âm** | Đúng (kiểm lại sau khi chạy xong 08 và 09) | ☐ |
| BAL-09 | P1 | Bấm 1 mặt hàng → khung chi tiết | Thông tin khớp Danh mục; bảng "Tồn theo điểm" khớp dòng trên bảng | ☐ |
| BAL-10 | P1 | Lịch sử thay đổi của mặt hàng | Liệt kê theo thời gian: nhập mua, xuất chuyển, nhận chuyển, kiểm kê, điều chỉnh, hủy; có số lượng ±, kho, mã phiếu | ☐ |
| BAL-11 | P1 | Cộng dồn lịch sử từ đầu | = tồn hiện tại | ☐ |
| BAL-12 | P2 | Xuất CSV khi đang lọc | File `ton-kho.csv` chỉ chứa dòng khớp bộ lọc; mở bằng Excel không lỗi font tiếng Việt | ☐ |
| BAL-13 | P2 | Giá vốn | Dạng `1.234.567 VNĐ` | ☐ |

### Thông báo kho
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| NOTI-01 | P1 | Có phiếu chờ mình xử lý | Tab "Chuyển kho" có số đếm tròn xanh = số phiếu chờ mình | ☐ |
| NOTI-02 | P1 | Bấm "Mở phiếu" ở 1 thông báo | Nhảy đúng phiếu (mở hộp thoại phiếu / cuộn tới phiếu); thông báo được đánh dấu đã xem | ☐ |
| NOTI-03 | P1 | Người thao tác | **Không** nhận thông báo về việc chính mình vừa làm | ☐ |
| NOTI-04 | P1 | Ép request `OpenNotice` với id thông báo của người khác | Không mở, không đánh dấu của người khác | ☐ |
| NOTI-05 | P2 | Thông báo trỏ tới phiếu đã bị hủy | Mở được phiếu ở trạng thái Đã hủy, không lỗi | ☐ |
| NOTI-06 | P1 | Mở `/notifications` có cả thông báo mới và đã xem | Sắp theo thời điểm tạo, mới nhất ở trên; thông báo chưa xem có nhãn "Mới", nền xanh nhạt; thông báo đã xem chữ xám, nhãn "Đã xem" | ☐ |
| NOTI-07 | P1 | Bấm "Mở" một thông báo rồi quay lại `/notifications` | Thông báo đó chuyển sang "Đã xem" **nhưng giữ nguyên vị trí** (không nhảy xuống cuối); số chuông giảm 1 | ☐ |
| NOTI-08 | P2 | Nhiều thông báo cùng loại (vd. 4 phiếu điều chỉnh chờ duyệt) | Mỗi dòng có mã phiếu riêng (`DC-…`); mã khớp mã trên thẻ phiếu ở tab Kiểm kê và điều chỉnh | ☐ |
| NOTI-09 | P2 | Thông báo phiếu kiểm kê, điều chỉnh, hủy: bấm "Mở" khi phiếu nằm ở trang 2+ của tab | Nhảy đúng trang chứa phiếu | ☐ |

---

## 2. Phân quyền

| Hành động | AD / GĐ / KT | TK | ThK | SA | CS |
|---|---|---|---|---|---|
| Vào `/inventory` | ✔ | ✔ | ✔ | ✔ | ✘ |
| Thấy số lượng ở **mọi** kho/showroom | ✔ | ✔ | ✔ | ✔ | – |
| Thấy cột Giá vốn (bảng + khung chi tiết) | ✔ | ✘ | ✘ | ✘ | – |
| Nút Xuất CSV | ✔ (mọi kho) | ✔ (chỉ kho mình) | ✘ | ✘ | – |
| Thấy tab "Chuyển kho", "Kiểm kê & điều chỉnh" | ✔ | ✔ | ✔ | ✘ | – |

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| BAL-P01 | P1 | CS | Gõ `/inventory` | Từ chối truy cập | ☐ |
| BAL-P02 | P1 | SA | Mở `/inventory` | Chỉ có tab "Tra cứu tồn kho"; thấy số lượng mọi kho; không có cột Giá vốn, không có nút Xuất CSV | ☐ |
| BAL-P03 | P1 | SA | Gõ `/inventory?view=transfers` và `?view=stocktakes` | Không hiện danh sách phiếu (quay về tab tra cứu hoặc trống) | ☐ |
| BAL-P04 | P1 | SA | Ép request Export | Không trả về dữ liệu (file rỗng hoặc bị từ chối) | ☐ |
| BAL-P05 | P1 | SA, ThK, TK | Xem mã nguồn trang, khung chi tiết mặt hàng, Network | Không có giá vốn ở bất kỳ đâu | ☐ |
| BAL-P06 | P1 | ThK | Mở `/inventory` | Thấy 3 tab; không có cột Giá vốn; không có nút Xuất CSV | ☐ |
| BAL-P07 | P1 | ThK | Ép request Export | Không trả về dữ liệu | ☐ |
| BAL-P08 | P1 | TK kho A | Xuất CSV (không lọc; rồi lọc kho B) | File chỉ có số liệu **kho A**; **không** có cột giá vốn | ☐ |
| BAL-P09 | P1 | AD, GĐ, KT | Xuất CSV | Có đủ mọi kho, có giá vốn | ☐ |
| BAL-P10 | P1 | Thủ kho **chưa gán kho** | Mở `/inventory` | Tra cứu tồn xem được; tab phiếu không có phiếu nào; không lỗi 500 | ☐ |
| BAL-P11 | P2 | ThK kho A | Lịch sử thay đổi của mặt hàng | Ghi lại: thấy biến động của mọi kho hay chỉ kho A **[xác nhận]** | ☐ |

---

## 3. Đối chiếu số liệu theo workflow (làm sau khi chạy 06, 08, 09)

| ID | P | Sự kiện | Tồn thực tế kho xuất | Giữ chỗ kho xuất | Khả dụng kho xuất | Đang chuyển kho nhận | Tồn thực tế kho nhận | KQ |
|---|---|---|---|---|---|---|---|---|
| BAL-W01 | P1 | Lập phiếu đề nghị chuyển 3 cái | không đổi | không đổi | không đổi | không đổi | không đổi | ☐ |
| BAL-W02 | P1 | Duyệt phiếu | không đổi | +3 | −3 | không đổi | không đổi | ☐ |
| BAL-W03 | P1 | Hủy phiếu đã duyệt | không đổi | −3 | +3 | không đổi | không đổi | ☐ |
| BAL-W04 | P1 | Xuất kho | −3 | −3 | không đổi (so với sau duyệt) | +3 | không đổi | ☐ |
| BAL-W05 | P1 | Trả về kho xuất | +3 | không đổi | +3 | −3 | không đổi | ☐ |
| BAL-W06 | P1 | Nhận đủ 3 | – | – | – | −3 | +3 | ☐ |
| BAL-W07 | P1 | Nhận thiếu (2/3) | – | – | – | −3 | +2 | ☐ |
| BAL-W08 | P1 | Nhận thừa (4/3) | – | – | – | −3 | +4 | ☐ |
| BAL-W09 | P1 | Nhập kho đơn mua 10 cái | (kho nhận của đơn) +10 | không đổi | +10 | không đổi | – | ☐ |
| BAL-W10 | P1 | Duyệt kiểm kê (sổ 10, đếm 8) | tồn = 8 | không đổi | giảm 2 | – | – | ☐ |
| BAL-W11 | P1 | Duyệt điều chỉnh +2 / −2 | ±2 | không đổi | ±2 | – | – | ☐ |
| BAL-W12 | P1 | Duyệt phiếu hủy | −1 | không đổi | −1 | – | – | ☐ |
| BAL-W13 | P1 | Giá vốn sau BAL-W01…W08, W10…W12 | **Không đổi** (chỉ nhập mua và điều chỉnh chi phí đơn mua mới đổi giá vốn) | | | | | ☐ |
