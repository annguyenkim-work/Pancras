# 08 – Kho: tab "Chuyển kho" (`/inventory?view=transfers`)

Thành phần màn hình:
- Tiêu đề "Lịch Sử Điều Chuyển Hàng Giữa Các Kho & Showroom", nút mở hộp thoại tạo phiếu.
- **Lọc nhanh**: Tất cả · Chờ tôi duyệt · Chờ tôi xuất kho · Chờ tôi nhận hàng · Chờ xử lý chênh lệch (mỗi mục có số đếm; mục có số 0 thì ẩn).
- **Bộ lọc**: ô "Mã phiếu, mã SKU, tên hàng...", Trạng thái, Kho/showroom, Từ ngày, Đến ngày; lọc ngay khi gõ hoặc chọn (không có nút Lọc); nút **Xóa lọc** chỉ hiện khi đang lọc.
- Bảng: Mã phiếu · Kho xuất → Nơi nhận · Hàng · Trạng thái · Người lập · Ngày lập · **Việc của bạn** · nút mở phiếu.
- Hộp thoại **Tạo Phiếu Đề Nghị Chuyển Kho**: Kho xuất, Nơi nhận, danh sách mặt hàng (ô gõ mã SKU/tên + Số lượng, "+ Thêm mặt hàng"), Ghi chú (≤ 500 ký tự), nút **Gửi Đề Nghị Chuyển Kho**.
- Hộp thoại **chi tiết phiếu**: thông tin phiếu, bảng hàng (Mã SKU, Tên, Số lượng chuyển, Thực nhận, ĐVT), số seal / người vận chuyển, file đính kèm + nút **Đính kèm file**, **Lịch sử phiếu**, và tùy trạng thái/quyền:
  - Form sửa phiếu + nút **Lưu thay đổi**
  - Nút **Từ chối**, **Duyệt**
  - Ô Số seal, Người vận chuyển + nút **Xuất kho**
  - Khối **Kiểm nhận hàng**: ô Thực nhận từng dòng, nút "Nhận đủ tất cả", Lý do chênh lệch, File đính kèm, nút **Xác Nhận Đã Nhận Hàng & Nhập Kho**
  - Khối **Xử lý chênh lệch (Kế toán / Giám đốc)** + nút **Xác nhận đã xử lý**
  - Nút **Trả về kho xuất**, **Hủy phiếu**
- Hộp thoại **Xác nhận** dùng chung (có ô lý do khi cần).

---

## 1. Sơ đồ trạng thái

```
                    ┌──(người lập, lý do)──▶ ĐÃ HỦY
                    │
CHỜ DUYỆT ──duyệt──▶ ĐÃ DUYỆT ──xuất kho──▶ ĐANG VẬN CHUYỂN ──nhận đủ──▶ ĐÃ NHẬN
   │  │   (giữ hàng)   │                        │        │
   │  └─từ chối(lý do)─┼─▶ TỪ CHỐI              │        └─nhận lệch (lý do + file)─▶ NHẬN CÓ CHÊNH LỆCH
   │                   │                        │                                         │
   └──(người lập, lý do)┴─▶ ĐÃ HỦY               └─trả về kho xuất (lý do)─▶ ĐÃ TRẢ VỀ      └─xử lý chênh lệch─▶ ĐÃ NHẬN (đóng)
```

## 2. Bảng "ai làm được gì ở trạng thái nào"

Ký hiệu: **TC** = Admin, Giám đốc, Kế toán. **Nguồn** = kho xuất. **Đích** = nơi nhận. "Người lập" = đúng người đã tạo phiếu.

| Trạng thái | Thao tác | Ai được làm | Bắt buộc |
|---|---|---|---|
| Chờ duyệt | Sửa phiếu | **Chỉ người lập** | – |
| Chờ duyệt | Duyệt | TC; Trưởng kho **của kho nguồn** | đủ tồn khả dụng |
| Chờ duyệt | Từ chối | TC; Trưởng kho của kho nguồn | lý do |
| Chờ duyệt, Đã duyệt | Hủy phiếu | **Chỉ người lập** | lý do |
| Đã duyệt | Xuất kho | TC; Trưởng kho và Thủ kho **của kho nguồn** | – (seal, người vận chuyển tùy chọn) |
| Đang vận chuyển | Kiểm nhận | TC; Trưởng kho và Thủ kho **của kho đích** | nhập số thực nhận **từng dòng**; nếu lệch: lý do + ≥ 1 file |
| Đang vận chuyển | Trả về kho xuất | **Chỉ TC** | lý do |
| Nhận có chênh lệch | Xử lý chênh lệch | **Chỉ TC** | nội dung xử lý |
| Mọi trạng thái trừ Đã hủy, Từ chối | Đính kèm file | TC; nhân viên kho nguồn hoặc kho đích | – |
| Đã nhận, Từ chối, Đã hủy, Đã trả về | (không còn thao tác) | – | – |

Lưu ý nghiệp vụ đã được chủ sản phẩm chấp nhận: **không tách nhiệm vụ** – một người có thể tự lập, duyệt, xuất, nhận cùng 1 phiếu nếu có quyền ở mỗi bước.

---

## 3. Chức năng

### 3.1 Danh sách, lọc
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-01 | P2 | Chưa có phiếu | "Chưa có phiếu chuyển." | ☐ |
| TRF-02 | P2 | Lọc theo từng Trạng thái | Chỉ ra phiếu trạng thái đó; nhãn trạng thái đúng chữ, màu | ☐ |
| TRF-03 | P2 | Lọc theo Kho/showroom | Ra phiếu có kho đó là kho xuất **hoặc** nơi nhận | ☐ |
| TRF-04 | P2 | Từ ngày – Đến ngày (gồm cả 2 đầu mút; Từ > Đến) | Đúng; Từ > Đến không lỗi | ☐ |
| TRF-05 | P2 | Tìm theo mã phiếu, mã SKU, tên hàng | Đúng | ☐ |
| TRF-06 | P2 | Lọc không ra | "Không có phiếu phù hợp." + nút "Xóa lọc" | ☐ |
| TRF-07 | P1 | Lọc nhanh "Chờ tôi duyệt / xuất kho / nhận hàng / xử lý chênh lệch" | Số đếm = số phiếu thực tế mình có quyền làm bước đó; bấm vào ra đúng các phiếu ấy | ☐ |
| TRF-08 | P1 | Số đếm trên tab "Chuyển kho" | = tổng phiếu đang chờ mình | ☐ |
| TRF-09 | P1 | Cột "Việc của bạn" | Ghi đúng bước mình cần làm; trống khi không có việc | ☐ |
| TRF-10 | P2 | Lọc nhanh + bộ lọc chi tiết cùng lúc | Kết hợp đúng; "Xóa lọc" đưa về danh sách đầy đủ | ☐ |

### 3.2 Tạo phiếu đề nghị
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-20 | P1 | Kho xuất A, nơi nhận B, 2 mặt hàng, ghi chú → Gửi | Phiếu **Chờ duyệt**; tồn kho A/B **không đổi** | ☐ |
| TRF-21 | P1 | Kho xuất = nơi nhận | "Phiếu chuyển không hợp lệ: cần chọn kho xuất khác kho nhận…" | ☐ |
| TRF-22 | P1 | Không có mặt hàng nào / số lượng 0 / số lượng âm / bỏ trống số lượng | Cùng thông báo trên | ☐ |
| TRF-23 | P2 | Gõ mã SKU hoặc tên để tìm mặt hàng | Gợi ý đúng; gõ tên không có trong danh mục → không tạo được dòng hợp lệ | ☐ |
| TRF-24 | P2 | "+ Thêm mặt hàng" nhiều lần; cùng 1 mặt hàng ở 2 dòng | Thêm được; 2 dòng trùng: ghi lại hành vi (khi duyệt phải kiểm **tổng** số lượng) | ☐ |
| TRF-25 | P2 | Ghi chú 500 ký tự / 501 ký tự (ép request) | 500 ✔; 501 ✘ | ☐ |
| TRF-26 | P1 | Số lượng **lớn hơn tồn** kho xuất | Vẫn tạo được (chỉ kiểm tồn khi duyệt) | ☐ |
| TRF-27 | P2 | Tạo lỗi | Hộp thoại mở lại, dữ liệu còn | ☐ |
| TRF-28 | P1 | Sau khi tạo | Lịch sử phiếu có mốc "lập phiếu"; TC và Trưởng kho nguồn nhận thông báo; người lập không nhận | ☐ |

### 3.3 Sửa phiếu (Chờ duyệt)
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-30 | P1 | Người lập đổi số lượng, thêm/bớt mặt hàng, đổi ghi chú → Lưu thay đổi | Lưu được; lịch sử có mốc "sửa" (không ghi nội dung sửa); thông báo lại cho TC và Trưởng kho nguồn | ☐ |
| TRF-31 | P1 | Sửa thành kho xuất = nơi nhận, hoặc xóa hết mặt hàng | Bị từ chối, phiếu giữ nguyên | ☐ |
| TRF-32 | P1 | Phiếu đã duyệt | Không còn form sửa; ép request `UpdateTransfer` → báo phiếu đã đổi trạng thái | ☐ |

### 3.4 Duyệt / Từ chối
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-40 | P1 | Duyệt khi kho nguồn đủ tồn khả dụng | Hộp xác nhận nêu rõ sẽ giữ hàng ở kho nguồn → Đã duyệt; Giữ chỗ +, Khả dụng −, Tồn thực tế không đổi | ☐ |
| TRF-41 | P1 | Duyệt khi không đủ khả dụng | "Kho xuất không đủ tồn khả dụng: <mã SKU thiếu>."; phiếu vẫn Chờ duyệt; số giữ chỗ không đổi | ☐ |
| TRF-42 | P1 | Tồn 10; phiếu 1 xin 7 đã duyệt; duyệt phiếu 2 xin 5 | Bị chặn (khả dụng chỉ còn 3) | ☐ |
| TRF-43 | P1 | Phiếu nhiều dòng, chỉ 1 dòng thiếu | Không duyệt; **không dòng nào** bị giữ hàng | ☐ |
| TRF-44 | P1 | Từ chối, có lý do | Trạng thái Từ chối; lý do hiện trên phiếu và lịch sử; người lập nhận thông báo | ☐ |
| TRF-45 | P1 | Từ chối, bỏ trống lý do | Hộp xác nhận không cho gửi; ép request → "Cần nhập lý do cho thao tác này…" | ☐ |
| TRF-46 | P2 | Lý do 501 ký tự (ép request) | "Nội dung nhập quá dài…" | ☐ |
| TRF-47 | P1 | Sau duyệt | Người lập + nhân viên kho nguồn nhận thông báo; lịch sử ghi người duyệt, giờ | ☐ |

### 3.5 Xuất kho
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-50 | P1 | Nhập số seal, người vận chuyển → Xuất kho → xác nhận | Đang vận chuyển; kho nguồn: Tồn −, Giữ chỗ −; kho đích: Đang chuyển + | ☐ |
| TRF-51 | P1 | Để trống seal và người vận chuyển | Vẫn xuất được | ☐ |
| TRF-52 | P2 | Seal 101 ký tự / người vận chuyển 201 ký tự (ép request) | "Nội dung nhập quá dài…"; phiếu vẫn Đã duyệt; tồn không đổi | ☐ |
| TRF-53 | P1 | Sau xuất | Phiếu hiện số seal, người vận chuyển; lịch sử ghi "Số seal: …"; người lập + nhân viên kho đích nhận thông báo | ☐ |
| TRF-54 | P1 | Lịch sử thay đổi của mặt hàng (tab Tra cứu) | Có dòng xuất chuyển −SL ở kho nguồn, mã phiếu đúng | ☐ |
| TRF-55 | P1 | Kho đích chưa từng có mặt hàng này | Xuất được; kho đích xuất hiện dòng tồn 0, đang chuyển + | ☐ |

### 3.6 Kiểm nhận
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-60 | P1 | Bấm "Nhận đủ tất cả" → Xác nhận | Ô Thực nhận tự điền = số đã xuất; phiếu **Đã nhận**; kho đích: Tồn +, Đang chuyển − | ☐ |
| TRF-61 | P1 | Để trống 1 ô Thực nhận | "Nhập số thực nhận cho từng mặt hàng, kể cả khi nhận đủ." | ☐ |
| TRF-62 | P1 | Nhận **thiếu** (2/3), không lý do | "Cần nhập lý do…" | ☐ |
| TRF-63 | P1 | Nhận thiếu, có lý do, **không file** | "Số thực nhận khác số xuất: cần đính kèm ít nhất một file…" | ☐ |
| TRF-64 | P1 | Nhận thiếu, có lý do + file | **Nhận có chênh lệch**; kho đích Tồn +2 (theo thực nhận), Đang chuyển −3 | ☐ |
| TRF-65 | P1 | Nhận **thừa** (4/3), có lý do + file | Nhận có chênh lệch; kho đích Tồn +4 | ☐ |
| TRF-66 | P1 | Thực nhận = 0 ở 1 dòng (có lý do + file) | Chấp nhận; dòng đó không tăng tồn | ☐ |
| TRF-67 | P1 | Thực nhận âm (ép request) | "Không nhận được hàng: kiểm tra lại số thực nhận…" | ☐ |
| TRF-68 | P1 | Nhận lệch, file đính kèm không hợp lệ (quá 10 MB / sai loại) | Báo lỗi file; phiếu vẫn Đang vận chuyển; tồn không đổi; **không** có file rác được lưu | ☐ |
| TRF-69 | P2 | Nhận lệch khi phiếu **đã có sẵn** file đính kèm từ trước, không chọn file mới | Chấp nhận (đã có ≥ 1 file) | ☐ |
| TRF-70 | P2 | Nhận lỗi (thiếu lý do) | Hộp thoại phiếu mở lại, số thực nhận và lý do đã gõ còn nguyên | ☐ |
| TRF-71 | P1 | Sau nhận lệch: tab "Kiểm kê & điều chỉnh" | Có phiếu điều chỉnh trạng thái **"Đã ghi nhận"** cho phần chênh (−1 hoặc +1), lý do lấy từ lúc nhận, kho = kho đích; **tồn không bị cộng trừ lần 2** | ☐ |
| TRF-72 | P1 | Sau nhận lệch | TC + người lập nhận thông báo; lọc nhanh "Chờ xử lý chênh lệch" của TC tăng 1 | ☐ |
| TRF-73 | P1 | Cột "Thực nhận" trên phiếu | Hiện số thực nhận từng dòng; dòng lệch được làm nổi bật | ☐ |

### 3.7 Xử lý chênh lệch
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-80 | P1 | TC nhập nội dung xử lý → Xác nhận đã xử lý → xác nhận | Phiếu đóng (Đã nhận); nội dung xử lý hiện trên phiếu; **tồn không đổi** | ☐ |
| TRF-81 | P1 | Bỏ trống nội dung (ép request) | "Cần nhập lý do…" | ☐ |
| TRF-82 | P1 | Sau xử lý | Người lập + nhân viên kho đích nhận thông báo; lịch sử ghi người xử lý | ☐ |

### 3.8 Trả về kho xuất
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-90 | P1 | TC bấm Trả về kho xuất, nhập lý do | **Đã trả về**; kho nguồn Tồn +SL; kho đích Đang chuyển −SL; lịch sử mặt hàng có dòng +SL ở kho nguồn | ☐ |
| TRF-91 | P1 | Bỏ trống lý do | "Cần nhập lý do…" | ☐ |
| TRF-92 | P1 | Sau trả về | Người lập, nhân viên kho nguồn, kho đích nhận thông báo; phiếu không còn nút thao tác | ☐ |

### 3.9 Hủy phiếu
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-100 | P1 | Người lập hủy phiếu **Chờ duyệt**, có lý do | Đã hủy; tồn không đổi; Trưởng kho nguồn nhận thông báo | ☐ |
| TRF-101 | P1 | Người lập hủy phiếu **Đã duyệt**, có lý do | Đã hủy; Giữ chỗ −, Khả dụng + (nhả hàng); nhân viên kho nguồn nhận thông báo | ☐ |
| TRF-102 | P1 | Hủy bỏ trống lý do | "Cần nhập lý do…" | ☐ |
| TRF-103 | P1 | Phiếu Đang vận chuyển | Không có nút Hủy; ép request → báo phiếu đã đổi trạng thái / không hủy được | ☐ |

### 3.10 File đính kèm
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-110 | P2 | Đính kèm PDF/JPG/PNG/xlsx/docx; chọn nhiều file 1 lần | Lưu được, hiện danh sách file | ☐ |
| TRF-111 | P1 | File > 10 MB / `.exe` đổi tên `.pdf` | "File quá lớn (tối đa 10 MB)." / "File không hợp lệ…" | ☐ |
| TRF-112 | P2 | Vượt 30 file | "Phiếu đã có quá nhiều file đính kèm." | ☐ |
| TRF-113 | P1 | Tải file | Tải đúng | ☐ |
| TRF-114 | P2 | Phiếu Đã hủy / Từ chối | Không còn form đính kèm; file cũ vẫn tải được | ☐ |

---

## 4. Phân quyền

Chuẩn bị: phiếu **A→B** do U-KEEP-B lập. Chạy từng bước, ở mỗi trạng thái đăng nhập lần lượt các vai trò và đối chiếu.

### 4.1 Ma trận nút hiển thị theo vai trò (phiếu A→B, người lập = Thủ kho B)

| Trạng thái → nút | AD/GĐ/KT | TK-A (nguồn) | ThK-A (nguồn) | TK-B (đích) | ThK-B (đích, **người lập**) | TK/ThK kho C | SA |
|---|---|---|---|---|---|---|---|
| Thấy phiếu trong danh sách | ✔ | ✔ | ✔ | ✔ | ✔ | ✘ | ✘ (không có tab) |
| Chờ duyệt → Sửa | ✘ | ✘ | ✘ | ✘ | ✔ | – | – |
| Chờ duyệt → Duyệt / Từ chối | ✔ | ✔ | ✘ | ✘ | ✘ | – | – |
| Chờ duyệt, Đã duyệt → Hủy | ✘ | ✘ | ✘ | ✘ | ✔ | – | – |
| Đã duyệt → Xuất kho | ✔ | ✔ | ✔ | ✘ | ✘ | – | – |
| Đang vận chuyển → Kiểm nhận | ✔ | ✘ | ✘ | ✔ | ✔ | – | – |
| Đang vận chuyển → Trả về kho xuất | ✔ | ✘ | ✘ | ✘ | ✘ | – | – |
| Nhận có chênh lệch → Xử lý | ✔ | ✘ | ✘ | ✘ | ✘ | – | – |
| Đính kèm file | ✔ | ✔ | ✔ | ✔ | ✔ | – | – |

### 4.2 Test case – tạo phiếu
| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| TRF-P01 | P1 | SA | Không có tab Chuyển kho; ép request `RequestTransfer` | "Bạn không có quyền thao tác với kho này." | ☐ |
| TRF-P02 | P1 | ThK-A | Tạo phiếu A→B (chuyển hàng đi) | Được | ☐ |
| TRF-P03 | P1 | ThK-A | Tạo phiếu B→A (xin hàng về) | Được | ☐ |
| TRF-P04 | P1 | ThK-A | Tạo phiếu B→C (không liên quan kho mình) | "Kho xuất hoặc kho nhận phải là kho/showroom của bạn." | ☐ |
| TRF-P05 | P1 | TK-A | Lặp lại P02–P04 | Như Thủ kho | ☐ |
| TRF-P06 | P1 | AD/GĐ/KT | Tạo phiếu giữa 2 kho bất kỳ | Được | ☐ |
| TRF-P07 | P1 | Thủ kho chưa gán kho | Tạo phiếu bất kỳ | Bị từ chối | ☐ |

### 4.3 Test case – từng bước (kiểm cả "không có nút" và "ép request")
Cách ép request: đăng nhập vai trò có nút, sao chép request (DevTools → Copy as fetch), rồi chạy lại trong phiên của vai trò không có quyền; hoặc sửa `DocumentId` trong form của một phiếu khác.

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| TRF-P10 | P1 | Mỗi vai trò, mỗi trạng thái | So nút đang hiện với ma trận 4.1 | Khớp từng ô | ☐ |
| TRF-P11 | P1 | TK-C, ThK-C | Danh sách phiếu | Không có phiếu A→B; gõ id phiếu vào URL cũng không mở được | ☐ |
| TRF-P12 | P1 | KT (không phải người lập) | Ép request `UpdateTransfer` phiếu Chờ duyệt | "Chỉ người lập phiếu mới được sửa hoặc hủy phiếu này…" | ☐ |
| TRF-P13 | P1 | TK-B (cùng kho với người lập) | Ép request `UpdateTransfer`, `CancelTransfer` | Thông báo như trên | ☐ |
| TRF-P14 | P1 | AD | Ép request `CancelTransfer` phiếu người khác lập | Thông báo như trên (Admin cũng không hủy thay được) | ☐ |
| TRF-P15 | P1 | ThK-A | Ép request `DecideTransfer` (duyệt) | "Bạn không được duyệt phiếu này." | ☐ |
| TRF-P16 | P1 | TK-B (kho đích) | Ép request `DecideTransfer` | Như trên | ☐ |
| TRF-P17 | P1 | ThK-B (người lập) | Ép request `DecideTransfer` phiếu của chính mình | Như trên | ☐ |
| TRF-P18 | P1 | TK-C | Ép request `DecideTransfer` | Như trên | ☐ |
| TRF-P19 | P1 | TK-B, ThK-B | Ép request `ShipTransfer` (kho nguồn là A) | "Bạn không có quyền thao tác với kho này." | ☐ |
| TRF-P20 | P1 | TK-A, ThK-A | Ép request `ReceiveTransfer` (kho đích là B) | Như trên | ☐ |
| TRF-P21 | P1 | TK-A, ThK-A, TK-B, ThK-B | Ép request `ReturnTransfer` | Bị từ chối | ☐ |
| TRF-P22 | P1 | TK-A, TK-B, ThK | Ép request `ResolveTransfer` | "Bạn không được duyệt phiếu này." | ☐ |
| TRF-P23 | P1 | TK-C, ThK-C | Ép request `AttachTransferFile`; gõ link tải file của phiếu A→B | Bị từ chối; không tải được file | ☐ |
| TRF-P24 | P1 | SA | Ép mọi request ở trên | Tất cả bị từ chối | ☐ |
| TRF-P25 | P1 | TK-A là **người lập** phiếu A→B | Tự duyệt phiếu của mình | Được (không tách nhiệm vụ); lịch sử ghi rõ cùng một người lập và duyệt | ☐ |
| TRF-P26 | P1 | KT lập phiếu A→B | Tự duyệt → tự xuất → tự nhận | Được; lịch sử đủ 4 mốc cùng một người | ☐ |
| TRF-P27 | P1 | ThK-A đang mở phiếu Đã duyệt; AD chuyển ThK-A sang kho C; ThK-A bấm Xuất kho | Bị từ chối, tồn không đổi | ☐ |
| TRF-P28 | P1 | TK-A | Lọc nhanh "Chờ tôi duyệt" | Chỉ đếm phiếu có **kho nguồn = A**; không đếm phiếu B→A | ☐ |
| TRF-P29 | P1 | ThK-B | Lọc nhanh "Chờ tôi nhận hàng" | Chỉ đếm phiếu Đang vận chuyển có **đích = B** | ☐ |
| TRF-P30 | P1 | TK-A, ThK-A | Lọc nhanh "Chờ xử lý chênh lệch" | Không hiện / đếm 0 | ☐ |

---

## 5. Workflow – kịch bản trọn luồng

Trước mỗi kịch bản ghi lại: tồn thực tế, giữ chỗ, khả dụng, đang chuyển của mặt hàng X ở kho A và B. Sau mỗi bước đối chiếu với [07 mục 3](07-kho-tra-cuu-ton.md).

| ID | P | Kịch bản | Kỳ vọng cuối | KQ |
|---|---|---|---|---|
| TRF-W01 | P1 | **Luồng chuẩn**: ThK-B lập (xin 3 X từ A) → TK-A duyệt → ThK-A xuất (seal) → ThK-B nhận đủ | Đã nhận. A: −3; B: +3. Lịch sử phiếu 4 mốc đúng người. Giá vốn X không đổi | ☐ |
| TRF-W02 | P1 | **Từ chối**: ThK-B lập → TK-A từ chối (lý do) | Từ chối. Tồn không đổi. ThK-B nhận thông báo kèm lý do | ☐ |
| TRF-W03 | P1 | **Sửa rồi duyệt**: ThK-B lập 3 → sửa thành 5 → TK-A duyệt | Giữ chỗ 5 (không phải 3) | ☐ |
| TRF-W04 | P1 | **Hủy trước duyệt**: ThK-B lập → ThK-B hủy (lý do) | Đã hủy. Tồn không đổi | ☐ |
| TRF-W05 | P1 | **Hủy sau duyệt**: lập → duyệt → người lập hủy | Đã hủy. Giữ chỗ về như cũ | ☐ |
| TRF-W06 | P1 | **Trả hàng**: lập → duyệt → xuất → KT trả về kho xuất (lý do) | Đã trả về. A và B về đúng số ban đầu | ☐ |
| TRF-W07 | P1 | **Nhận thiếu**: lập 3 → duyệt → xuất → ThK-B nhận 2 (lý do + ảnh) → KT xử lý chênh lệch | Đã nhận (đóng). A: −3; B: +2. Có phiếu điều chỉnh "Đã ghi nhận" −1 ở B. Tổng hệ thống hụt 1 | ☐ |
| TRF-W08 | P1 | **Nhận thừa**: lập 3 → … → nhận 4 → KT xử lý | A: −3; B: +4. Phiếu điều chỉnh "Đã ghi nhận" +1 | ☐ |
| TRF-W09 | P1 | **Nhiều mặt hàng, lệch 1 dòng**: X nhận đủ, Y nhận thiếu | Chỉ Y có phiếu điều chỉnh ghi nhận | ☐ |
| TRF-W10 | P1 | **Tranh hàng**: tồn A = 5. Phiếu 1 xin 4, phiếu 2 xin 4. Duyệt phiếu 1 → duyệt phiếu 2 | Phiếu 2 bị chặn. Hủy phiếu 1 → duyệt phiếu 2 được | ☐ |
| TRF-W11 | P1 | **Giữ hàng chống tranh**: phiếu 1 (4/5) đã duyệt. Thử: phiếu hủy hàng X ở A, điều chỉnh −2 ở A, kiểm kê đếm 3 ở A | Ghi lại từng kết quả: có làm tồn < số đang giữ không. Sau đó xuất kho phiếu 1 phải được hoặc báo thiếu rõ ràng – **không** ra số âm | ☐ |
| TRF-W12 | P1 | **Vòng tròn**: A→B 3 cái, nhận đủ; rồi B→A 3 cái, nhận đủ | A và B về đúng số ban đầu | ☐ |

### Đồng thời (2 trình duyệt)
| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-C01 | P1 | TK-A và KT cùng mở phiếu Chờ duyệt: một người Duyệt, một người Từ chối | Chỉ 1 thành công; người kia: "Phiếu vừa được người khác xử lý hoặc đã đổi trạng thái. Tải lại trang…" | ☐ |
| TRF-C02 | P1 | Người lập bấm Hủy; TK-A bấm Duyệt cùng lúc | Chỉ 1 thành công; không có phiếu Đã hủy mà vẫn giữ hàng | ☐ |
| TRF-C03 | P1 | Người lập đang sửa; TK-A duyệt; người lập bấm Lưu thay đổi | Lưu bị từ chối; số lượng đã duyệt không đổi | ☐ |
| TRF-C04 | P1 | ThK-A và TK-A cùng bấm Xuất kho | Chỉ xuất 1 lần; tồn A giảm đúng 1 lần | ☐ |
| TRF-C05 | P1 | ThK-B bấm Nhận; KT bấm Trả về kho xuất cùng lúc | Chỉ 1 thành công; tổng tồn A+B không bị cộng 2 lần | ☐ |
| TRF-C06 | P1 | ThK-B và TK-B cùng bấm Nhận với số khác nhau | Chỉ 1 lần nhận được ghi | ☐ |
| TRF-C07 | P1 | 2 KT cùng xử lý chênh lệch | Chỉ 1 thành công | ☐ |
| TRF-C08 | P1 | Bấm đúp mọi nút thao tác | Mỗi thao tác chỉ ghi 1 lần | ☐ |
| TRF-C09 | P1 | Người lập hủy phiếu Đã duyệt; ThK-A bấm Xuất kho cùng lúc | Chỉ 1 thành công; không có hàng "đang chuyển" của phiếu Đã hủy | ☐ |

### Nhật ký
| ID | P | Kiểm tra | Kỳ vọng | KQ |
|---|---|---|---|---|
| TRF-L01 | P1 | Lịch sử phiếu sau TRF-W07 | Đủ: lập, duyệt, xuất, nhận có chênh lệch, xử lý – đúng người, giờ, lý do | ☐ |
| TRF-L02 | P1 | `/audit` (Admin) | Có dòng cho: lập, duyệt, từ chối, xuất, nhận (ghi chú chênh lệch), xử lý, hủy, trả hàng | ☐ |
| TRF-L03 | P2 | Thao tác bị từ chối vì thiếu quyền | Không tạo mốc lịch sử, không gửi thông báo | ☐ |
