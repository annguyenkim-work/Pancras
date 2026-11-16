# 11 – Workflow liên màn hình (kịch bản đầu–cuối)

Chạy sau khi các file 01–10 đã qua. Mỗi kịch bản đi qua nhiều màn hình và nhiều vai trò; mục đích là bắt lỗi ở **chỗ nối** giữa các màn hình: số liệu, quyền, thông báo, nhật ký.

Cách ghi: trước mỗi kịch bản lập bảng "số đầu" (tồn thực tế, giữ chỗ, đang chuyển, giá vốn của từng mặt hàng ở từng kho); sau mỗi bước có dấu 🔎 thì đối chiếu.

---

## E2E-01 – Container nhập khẩu từ lúc lập đơn tới khi hàng lên showroom

Vai trò: KT, GĐ, ThK-A, TK-A, ThK-B, AD. Mặt hàng X (chưa có tồn), Y (đã có tồn ở A và B).

| # | Vai trò | Màn hình | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| 1 | KT | Nhà cung cấp | Thêm NCC mới | Có trong danh sách | ☐ |
| 2 | KT | Danh mục | Thêm mặt hàng X (đủ khối lượng, thể tích) | Có trong danh sách | ☐ |
| 3 | KT | Mua hàng | Lập đơn: NCC mới, kho nhận A, X × 10, Y × 5, 2 khoản chi phí, đính kèm tờ khai | Đơn Nháp | ☐ |
| 4 | ThK-A | Mua hàng | Mở `/purchasing` | **Không** thấy đơn | ☐ |
| 5 | KT | Mua hàng | Gửi duyệt | Chờ duyệt | ☐ |
| 6 | GĐ | Mua hàng | Duyệt đơn | Đã duyệt | ☐ |
| 7 | ThK-A | Mua hàng | Tải lại | Thấy đơn, **không** thấy tiền | ☐ |
| 8 | ThK-B | Mua hàng | Mở `/purchasing` | Không thấy đơn (kho nhận là A) | ☐ |
| 9 | KT | Mua hàng | Hàng đang về | Đang về | ☐ |
| 10 | KT | Nhà cung cấp | Thử Disable NCC | Bị chặn (còn lô chưa nhập kho) | ☐ |
| 11 | ThK-A | Mua hàng | Nhập Kho & Cập Nhật Giá Vốn | Đã nhận | ☐ |
| 12 🔎 | KT | Kho → Tra cứu | X, Y tại kho A | X: +10; Y: +5. Giá vốn X = giá vốn/đơn vị của đơn; giá vốn Y = bình quân trên tổng tồn mọi kho | ☐ |
| 13 🔎 | KT | Danh mục | Cột Giá vốn của X, Y | Khớp bước 12 | ☐ |
| 14 | ThK-A | Kho → Tra cứu | X, Y | Thấy số lượng, không thấy giá vốn | ☐ |
| 15 | KT | Mua hàng | Ghi điều chỉnh chi phí +500.000 VNĐ | Giá vốn X, Y tăng; chênh lệch = 0 (hàng còn nguyên) | ☐ |
| 16 | ThK-B | Kho → Chuyển kho | Lập đề nghị A→B: X × 4 | Chờ duyệt; TK-A và KT có thông báo | ☐ |
| 17 | TK-A | Kho → Chuyển kho | Lọc nhanh "Chờ tôi duyệt" → Duyệt | Đã duyệt; X ở A: giữ chỗ 4 | ☐ |
| 18 | ThK-A | Kho → Chuyển kho | "Chờ tôi xuất kho" → nhập seal → Xuất kho | Đang vận chuyển | ☐ |
| 19 | ThK-B | Kho → Chuyển kho | "Chờ tôi nhận hàng" → nhận 3 (thiếu 1), lý do + ảnh | Nhận có chênh lệch | ☐ |
| 20 | KT | Kho → Chuyển kho | "Chờ xử lý chênh lệch" → nhập cách xử lý → xác nhận | Đã nhận (đóng) | ☐ |
| 21 🔎 | KT | Kho → Tra cứu | X | A: 6; B: 3; giữ chỗ 0; đang chuyển 0. Giá vốn X **không đổi** so với sau bước 15 | ☐ |
| 22 | KT | Kho → Kiểm kê & điều chỉnh | Tìm phiếu điều chỉnh "Đã ghi nhận" | Có phiếu −1 tại B gắn với phiếu chuyển; không có nút duyệt | ☐ |
| 23 | KT | Mua hàng | Ghi điều chỉnh chi phí +300.000 VNĐ lần nữa | Ghi lại phần "vào giá vốn hàng còn tồn" và "chênh lệch" – tổng hai phần = 300.000 | ☐ |
| 24 | KT | Nhà cung cấp | Disable NCC | Được (không còn lô chưa nhập) | ☐ |
| 25 | AD | Nhật ký | Tìm theo mã đơn, mã phiếu | Đủ dòng cho các bước 1–3, 5, 6, 9, 11, 15–20, 23, 24 đúng người | ☐ |

## E2E-02 – Một ngày của kho: kiểm kê, hao hụt, hủy hàng

Vai trò: ThK-A, TK-A, KT. Mặt hàng Y tại kho A, số đầu = N.

| # | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | ThK-A | Lập biên bản kiểm kê Y: đếm N−2, lý do + chứng từ + ảnh | Chờ duyệt; tồn vẫn N | ☐ |
| 2 | ThK-A | Lập phiếu hủy Y (hàng vỡ) | Chờ duyệt; tồn vẫn N | ☐ |
| 3 | ThK-A | Lập phiếu điều chỉnh Y +1 (tìm lại được hàng) | Chờ duyệt; KT nhận thông báo | ☐ |
| 4 | TK-A | Duyệt biên bản kiểm kê | Tồn = N−2 | ☐ |
| 5 | TK-A | Duyệt phiếu hủy | Tồn = N−3 | ☐ |
| 6 | TK-A | Tìm nút duyệt phiếu điều chỉnh | Không có; ép request bị từ chối | ☐ |
| 7 | KT | Duyệt điều chỉnh | Tồn = N−2 | ☐ |
| 8 🔎 | KT | Tra cứu tồn → lịch sử thay đổi của Y | 3 dòng: kiểm kê −2, hủy −1, điều chỉnh +1; giá vốn Y không đổi | ☐ |
| 9 | TK-A | Tự lập biên bản kiểm kê Y (đếm N−3) → tự duyệt | Không tự duyệt được | ☐ |
| 10 | KT | Duyệt biên bản của TK-A | Tồn = N−3 | ☐ |

## E2E-03 – Tranh chấp hàng giữa 2 showroom

Tồn X tại kho A = 5. Vai trò: ThK-B, ThK-C, TK-A, ThK-A, KT.

| # | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | ThK-B | Đề nghị A→B: X × 4 | Chờ duyệt | ☐ |
| 2 | ThK-C | Đề nghị A→C: X × 4 | Chờ duyệt | ☐ |
| 3 | TK-A | Duyệt phiếu của B | Đã duyệt; khả dụng A = 1 | ☐ |
| 4 | TK-A | Duyệt phiếu của C | Bị chặn: không đủ tồn khả dụng, nêu mã X | ☐ |
| 5 | ThK-C | Sửa phiếu thành X × 1 | Lưu được | ☐ |
| 6 | TK-A | Duyệt phiếu của C | Đã duyệt; khả dụng A = 0 | ☐ |
| 7 | ThK-B | Hủy phiếu của mình (lý do) | Đã hủy; khả dụng A = 4 | ☐ |
| 8 | ThK-A | Xuất kho phiếu của C | Đang vận chuyển; A tồn 4 | ☐ |
| 9 | KT | Trả về kho xuất phiếu của C (giao nhầm) | Đã trả về; A tồn 5 | ☐ |
| 10 🔎 | KT | Tra cứu tồn X | A: 5, giữ chỗ 0; B, C không đổi, đang chuyển 0 | ☐ |

## E2E-04 – Thay đổi nhân sự giữa chừng luồng công việc

| # | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| 1 | ThK-B | Lập đề nghị A→B | Chờ duyệt | ☐ |
| 2 | AD | Chuyển ThK-B sang kho C | – | ☐ |
| 3 | ThK-B (giờ ở kho C) | Mở tab Chuyển kho | Không còn thấy phiếu A→B; không sửa/hủy được (ép request bị từ chối vì không còn thuộc kho của phiếu) | ☐ |
| 4 | TK-A | Duyệt; ThK-A xuất | Đang vận chuyển | ☐ |
| 5 | TK-B | Nhận hàng | Được (cùng kho đích) | ☐ |
| 6 | AD | Ngừng hoạt động TK-A | – | ☐ |
| 7 | ThK-A | Lập biên bản kiểm kê kho A | Chờ duyệt; không còn Trưởng kho → chỉ AD/GĐ/KT duyệt được | ☐ |
| 8 | AD | Tạo Trưởng kho A mới | Tạo được **[xác nhận: NV đã ngừng có còn chiếm suất Trưởng kho không]** | ☐ |
| 9 | TK-A mới | Duyệt biên bản ở bước 7 | Được | ☐ |
| 10 | AD | Hạ KT xuống Thủ kho A trong lúc KT đang mở đơn mua Chờ duyệt; KT bấm Duyệt | Bị từ chối; sau khi tải lại KT chỉ còn thấy đơn về kho A từ Đã duyệt, không thấy tiền | ☐ |

## E2E-05 – Kiểm tra chéo toàn hệ thống (chạy cuối cùng)

| ID | P | Kiểm tra | Kỳ vọng | KQ |
|---|---|---|---|---|
| X-01 | P1 | Với 3 mặt hàng bất kỳ: Σ (nhập mua) − Σ (hủy) ± Σ (kiểm kê, điều chỉnh đã duyệt) ± chênh lệch nhận chuyển kho | = tổng tồn thực tế mọi kho | ☐ |
| X-02 | P1 | Tổng "Đang giữ chỗ" từng kho | = tổng số lượng của các phiếu chuyển **Đã duyệt** có kho đó là nguồn | ☐ |
| X-03 | P1 | Tổng "Đang chuyển" từng kho | = tổng số lượng của các phiếu **Đang vận chuyển** có kho đó là đích | ☐ |
| X-04 | P1 | Không còn số âm ở bất kỳ cột nào | Đúng | ☐ |
| X-05 | P1 | Giá vốn ở Danh mục = giá vốn ở Kho (vai trò tài chính) cho mọi mặt hàng | Khớp | ☐ |
| X-06 | P1 | Số đơn / lô chưa nhập ở Nhà cung cấp | Khớp Mua hàng | ☐ |
| X-07 | P1 | Lọc nhanh của từng vai trò ở tab Chuyển kho | Tất cả = 0 khi không còn phiếu dở | ☐ |
| X-08 | P1 | Đăng nhập lại 7 vai trò, chạy lại bảng sidebar ở [02](02-khung-giao-dien.md) | Không vai trò nào thấy thêm/bớt mục so với đầu đợt test | ☐ |
