# 06 – Mua hàng, Container nhập khẩu & Giá vốn (`/purchasing`)

Thành phần màn hình:
- Đầu trang: nút **+ Tạo Đơn Mua Hàng & Container**, ô tìm "Tìm theo mã đơn, nhà cung cấp, container, B/L, cảng...".
- Cột trái: **Danh sách đơn mua container (N)** – mỗi thẻ: mã đơn, số mặt hàng, nhãn trạng thái, NCC, Cont, B/L, cảng, ETA, kho nhận, tổng giá vốn.
- Khung chi tiết đơn đang chọn:
  - Nút: **Xuất CSV**, **Sửa đơn**, **Nhập Kho & Cập Nhật Giá Vốn (N món)**.
  - Nút trạng thái: **Gửi duyệt**, **Duyệt đơn**, **Trả về nháp** (kèm ô lý do), **Hàng đang về**, **Hủy đơn** (kèm ô lý do).
  - Bảng **Danh sách mặt hàng trong container**: Mã SKU, Tên, SL đặt, SL đã nhận, **SL nhận lần này** (khi đơn còn nhận được), Giá FOB (USD), Thuế NK, Trị giá hải quan, Giá tính VAT, VAT, Chi phí phân bổ, (Dự toán) Giá vốn dòng, (Dự toán) Giá vốn/đơn vị. Bấm vào dòng xem **Công thức giá vốn**.
  - Cạnh nút nhập kho: ô **Đóng đơn dù nhận thiếu**.
  - **Bảng chi phí nhập khẩu & phân bổ**.
  - **Điều chỉnh chi phí sau khi nhận** + form: Tên khoản, Số tiền (đ, có dấu), Cách chia, Lý do, nút **Ghi điều chỉnh**.
  - **Chứng từ đính kèm**: loại chứng từ, chọn file, nút **Đính kèm**, nút **Xóa**.
  - **Lịch sử đơn**.
- Hộp thoại **Lập Phiếu Mua Hàng & Container Nhập Khẩu** / **Sửa Đơn Mua Hàng (chưa nhập kho)**: Mã đơn, Nhà cung cấp, Kho tiếp nhận đích, Tỷ giá, Số hiệu Container, Số vận đơn, Cảng đến, Ngày dự kiến cập cảng; bảng mặt hàng (Sản phẩm, Số lượng, Đơn giá FOB USD, Thuế NK %, Thuế khác đ, Giá vốn/đơn vị); bảng **Chi phí chung của cả đơn** (Tên khoản, Số tiền, Nhóm, Cách chia); Suất VAT, ô chọn "VAT đầu vào được khấu trừ"; bảng xem trước công thức ①–⑩.

Trạng thái đơn: **Nháp → Chờ duyệt → Đã duyệt → Đang về → Đã nhận một phần → Đã nhận**; nhánh **Đã hủy**. Nhận thiếu mà chưa đóng đơn thì dừng ở **Đã nhận một phần**.

---

## 1. Chức năng

### 1.1 Danh sách & tìm kiếm
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-01 | P2 | Tìm theo mã đơn, tên NCC, số container, B/L, cảng | Đúng kết quả; không ra thì "Không có đơn mua khớp từ khóa." | ☐ |
| PUR-02 | P2 | Chưa có đơn nào (vai trò quản lý) | "Chưa có đơn mua." + nút tạo đơn | ☐ |
| PUR-03 | P2 | Bấm thẻ đơn | Thẻ được viền xanh, khung chi tiết đổi theo | ☐ |
| PUR-04 | P2 | Nhãn trạng thái | Đúng chữ và màu cho cả 6 trạng thái | ☐ |
| PUR-05 | P2 | Mở từ Nhà cung cấp (lọc theo NCC) | Chỉ hiện đơn của NCC đó; tìm kiếm vẫn giữ bộ lọc NCC | ☐ |

### 1.2 Lập đơn
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-10 | P1 | Nhập đủ, 2 mặt hàng, 2 khoản chi phí → Lưu | Tạo đơn trạng thái **Nháp**, thông báo đã tạo, đơn mới được chọn | ☐ |
| PUR-11 | P1 | Mã đơn trùng | "Mã đơn đã được dùng." | ☐ |
| PUR-12 | P1 | Không chọn NCC / không chọn kho nhận / không có mặt hàng nào | Báo đơn không được tạo, nêu cần kiểm tra mã đơn, NCC, kho nhận, mặt hàng | ☐ |
| PUR-13 | P1 | Số lượng 0 hoặc âm | "Số lượng mỗi mặt hàng phải lớn hơn 0…" | ☐ |
| PUR-14 | P1 | Đơn giá FOB = 0 | "Mỗi mặt hàng cần có đơn giá FOB lớn hơn 0…" | ☐ |
| PUR-15 | P1 | Thuế khác / số tiền chi phí âm | "Số tiền không được âm." | ☐ |
| PUR-16 | P1 | Chia chi phí "Theo khối lượng" hoặc "Theo thể tích" khi có mặt hàng chưa khai số đo | Báo thiếu số đo, gợi ý chia theo FOB/số lượng | ☐ |
| PUR-17 | P2 | Chọn cùng 1 mặt hàng ở 2 dòng | Ghi lại hành vi (gộp, cho phép hay báo lỗi) | ☐ |
| PUR-18 | P2 | "+ Thêm Mặt Hàng Nhập", xóa dòng bằng nút thùng rác | Bộ đếm "(N MẶT HÀNG)" đúng; bảng xem trước cập nhật | ☐ |
| PUR-19 | P2 | "+ Thêm dòng chi phí", xóa dòng chi phí | Hoạt động; chi phí bị xóa không còn trong bảng xem trước | ☐ |
| PUR-20 | P1 | Tỷ giá = 0 / để trống / chữ | Không lưu được, báo lỗi | ☐ |
| PUR-21 | P2 | Ô tỷ giá, ô tiền đ | Tự chèn dấu chấm nhóm nghìn khi gõ; lưu ra đúng số | ☐ |
| PUR-22 | P2 | Lưu lỗi | Hộp thoại mở lại, **toàn bộ** dòng hàng và chi phí còn nguyên, lỗi hiện trong hộp thoại (không nằm sau lớp nền) | ☐ |
| PUR-23 | P2 | Hộp thoại lập đơn | Có nền mờ, tiêu đề đứng yên, thân cuộn; Esc/× đóng được; không thao tác được trang phía sau | ☐ |
| PUR-24 | P2 | Chỉ chọn được NCC đang hợp tác; kho nhận là kho/showroom hiện có | Đúng | ☐ |

### 1.3 Tính giá vốn (đối chiếu tay bằng Excel)
Chuẩn bị 1 đơn mẫu: tỷ giá 25.000; hàng A SL 10 × 100 USD, thuế NK 10%; hàng B SL 5 × 400 USD, thuế NK 20%, thuế khác 1.000.000 VNĐ; cước biển 6.000.000 VNĐ nhóm *Trị giá hải quan* chia theo FOB; phí THC 3.000.000 VNĐ nhóm *Logistics* chia theo số lượng; VAT 10%.

| ID | P | Kiểm tra | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-30 | P1 | ① FOB quy đổi = SL × đơn giá USD × tỷ giá | A = 25.000.000 VNĐ; B = 50.000.000 VNĐ | ☐ |
| PUR-31 | P1 | ② Cước chia theo FOB (1:2) | A = 2.000.000; B = 4.000.000; tổng đúng 6.000.000 | ☐ |
| PUR-32 | P1 | ③ Trị giá hải quan = ① + ② | A = 27.000.000; B = 54.000.000 | ☐ |
| PUR-33 | P1 | ④ Thuế NK = ③ × % | A = 2.700.000; B = 10.800.000 | ☐ |
| PUR-34 | P1 | ⑥ Giá tính VAT = ③ + ④ + ⑤; ⑦ VAT = ⑥ × 10% | A: 29.700.000 → 2.970.000; B: 65.800.000 → 6.580.000 | ☐ |
| PUR-35 | P1 | ⑧ THC chia theo số lượng (10:5) | A = 2.000.000; B = 1.000.000 | ☐ |
| PUR-36 | P1 | ⑨ Giá vốn dòng, ô "VAT được khấu trừ" **tắt** (mặc định): VAT **cộng** vào giá vốn | A = 34.670.000; B = 73.380.000 | ☐ |
| PUR-37 | P1 | ⑨ khi **bật** "VAT được khấu trừ": VAT không vào giá vốn | A = 31.700.000; B = 66.800.000 | ☐ |
| PUR-38 | P1 | ⑩ Giá vốn/đơn vị = ⑨ ÷ SL, làm tròn đồng | Khớp; trường hợp lẻ 0,5 VNĐ làm tròn **lên** | ☐ |
| PUR-39 | P1 | Số ở hộp thoại (xem trước) = số ở khung chi tiết sau khi lưu = số trong file CSV | Khớp từng ô | ☐ |
| PUR-40 | P1 | Tổng tiền các dòng phân bổ của 1 khoản chi phí | Bằng đúng số tiền khoản đó, không lệch 1 đồng (thử số chia không hết: 1.000.000 VNĐ chia 3 dòng) | ☐ |
| PUR-41 | P2 | Nhóm *Thuế không khấu trừ* | Cộng vào giá vốn | ☐ |
| PUR-42 | P2 | Nhóm *Thuế được khấu trừ* | Không cộng vào giá vốn | ☐ |
| PUR-43 | P2 | Bấm 1 dòng hàng ở khung chi tiết | Hiện "Công thức giá vốn · <mã>" với các con số khớp bảng | ☐ |
| PUR-44 | P2 | Tiêu đề cột khi chưa nhận hàng | "Dự toán giá vốn dòng / đơn vị"; sau khi nhận đổi thành "Giá vốn dòng / đơn vị" | ☐ |

> **Cần xác nhận với BA**: tài liệu dự án ghi "VAT không tính vào giá vốn", nhưng màn hình mặc định **cộng** VAT vào giá vốn ("theo khách hàng, VAT nhập khẩu tính vào giá vốn"). PUR-36/37 viết theo hành vi màn hình.

### 1.4 Sửa đơn
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-50 | P1 | Sửa đơn Nháp: đổi SL, thêm/bớt mặt hàng, đổi chi phí → Lưu thay đổi | Lưu được; giá vốn tính lại; lịch sử/dấu vết ghi người sửa | ☐ |
| PUR-51 | P1 | Sửa đơn ở Chờ duyệt / Đã duyệt / Đang về | Vẫn sửa được (nút "Sửa đơn" còn hiện) | ☐ |
| PUR-52 | P1 | Đơn Đã nhận / Đã hủy | Không có nút "Sửa đơn"; gõ `/purchasing?edit=<id>` rồi lưu → "Đơn đã nhập kho." hoặc "…không sửa được nữa." | ☐ |
| PUR-53 | P1 | Đổi mã đơn thành mã đã có | Báo trùng | ☐ |
| PUR-54 | P2 | Đơn có khoản chi phí **chia tay**: thêm hoặc bớt mặt hàng | Báo phải xóa khoản chia tay trước | ☐ |
| PUR-55 | P1 | 2 người cùng mở Sửa 1 đơn, lưu lần lượt | Người sau nhận "Đơn vừa được người khác sửa. Tải lại và thử lại."; dữ liệu người trước giữ nguyên | ☐ |
| PUR-56 | P2 | Đổi kho nhận khi đơn Đã duyệt | Lưu được; Thủ kho kho cũ không còn thấy đơn, Thủ kho kho mới thấy | ☐ |

### 1.5 Chứng từ đính kèm
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-60 | P1 | Đính kèm PDF, JPG, PNG, xlsx, docx (mỗi loại 1 file), chọn từng "Loại chứng từ" | Lưu được; hiện tên, loại; bộ đếm tiêu đề tăng | ☐ |
| PUR-61 | P1 | File > 10 MB | "File quá lớn. Tối đa 10 MB mỗi file." | ☐ |
| PUR-62 | P1 | File `.exe` đổi tên thành `.pdf`; file `.txt` đổi thành `.jpg` | "File không hợp lệ…" | ☐ |
| PUR-63 | P2 | File `.zip`, `.html`, không có đuôi | Từ chối | ☐ |
| PUR-64 | P2 | Đính kèm file thứ 31 | "Đơn đã đủ số file đính kèm tối đa (30)." | ☐ |
| PUR-65 | P1 | Tải file đã đính kèm | Tải đúng file, mở được, tên file đúng (kể cả tên tiếng Việt) | ☐ |
| PUR-66 | P1 | Xóa file khi đơn còn sửa được | Xóa được | ☐ |
| PUR-67 | P1 | Đơn Đã nhận | Không có nút Xóa; ép request xóa → "Đơn đã nhập kho." | ☐ |
| PUR-68 | P2 | Đính kèm thêm file khi đơn Đã nhận (hóa đơn về muộn) | Ghi lại hành vi (form đính kèm vẫn hiện với vai trò quản lý) | ☐ |
| PUR-69 | P2 | Tên file có `../`, ký tự lạ, rất dài | Không lỗi; tên hiển thị an toàn | ☐ |

### 1.6 Xuất CSV
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-70 | P2 | Xuất CSV (vai trò quản lý) | Mở bằng Excel không lỗi font; số liệu khớp màn hình | ☐ |
| PUR-71 | P1 | Xuất CSV (Thủ kho/Trưởng kho) | File **không** có cột giá FOB, thuế, chi phí, giá vốn | ☐ |

---

## 2. Workflow trạng thái đơn mua

### 2.1 Bảng chuyển trạng thái (nút nào hiện ở trạng thái nào)
Người thao tác: Admin, Giám đốc, Kế toán (trừ "Nhập kho" – xem mục 3).

| Trạng thái hiện tại | Sửa đơn | Gửi duyệt | Duyệt đơn | Trả về nháp | Hàng đang về | Nhập kho | Hủy đơn | Ghi điều chỉnh |
|---|---|---|---|---|---|---|---|---|
| Nháp | ✔ | ✔ → Chờ duyệt | ✘ | ✘ | ✘ | ✘ | ✔ | ✘ |
| Chờ duyệt | ✔ | ✘ | ✔ → Đã duyệt | ✔ → Nháp | ✘ | ✘ | ✔ | ✘ |
| Đã duyệt | ✔ | ✘ | ✘ | ✘ | ✔ → Đang về | ✔ → Đã nhận | ✔ | ✘ |
| Đang về | ✔ | ✘ | ✘ | ✘ | ✘ | ✔ → Đã nhận | ✔ | ✘ |
| Đã nhận | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✔ |
| Đã hủy | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ |

### 2.2 Bước hợp lệ
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-W01 | P1 | Mỗi trạng thái: so các nút đang hiện với bảng 2.1 | Khớp từng ô | ☐ |
| PUR-W02 | P1 | Nháp → Gửi duyệt | Trạng thái Chờ duyệt; Lịch sử đơn có dòng "gửi duyệt" + người + giờ | ☐ |
| PUR-W03 | P1 | Chờ duyệt → Duyệt đơn | Đã duyệt; lịch sử ghi người duyệt | ☐ |
| PUR-W04 | P1 | Chờ duyệt → Trả về nháp, có lý do | Về Nháp; lý do hiện trong lịch sử | ☐ |
| PUR-W05 | P1 | Trả về nháp, **bỏ trống** lý do (xóa thuộc tính `required` bằng DevTools) | "Cần ghi lý do." Đơn không đổi trạng thái. | ☐ |
| PUR-W06 | P1 | Đã duyệt → Hàng đang về | Đang về | ☐ |
| PUR-W07 | P1 | Hủy đơn có lý do, lần lượt từ Nháp / Chờ duyệt / Đã duyệt / Đang về | Đã hủy; lý do trong lịch sử; mọi nút thao tác biến mất | ☐ |
| PUR-W08 | P1 | Hủy đơn bỏ trống lý do / chỉ nhập khoảng trắng (ép request) | "Cần ghi lý do." | ☐ |
| PUR-W09 | P2 | Lý do dài hơn 500 ký tự (ép request), hủy hoặc trả về nháp | "Lý do chỉ được tối đa 500 ký tự." Đơn không đổi. | ☐ |
| PUR-W10 | P1 | Đơn bị trả về nháp → sửa → gửi duyệt lại → duyệt | Chạy được; lịch sử đủ các mốc theo thứ tự | ☐ |

### 2.3 Bước không hợp lệ (ép request: sửa hidden `action` hoặc gửi lại request cũ)
Mã `action` nằm trong hidden input của từng form. Đổi giá trị rồi submit.

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-W20 | P1 | Nháp → Duyệt đơn (bỏ qua gửi duyệt) | "Trạng thái hiện tại của đơn không cho phép bước này…" | ☐ |
| PUR-W21 | P1 | Nháp → Hàng đang về | Như trên | ☐ |
| PUR-W22 | P1 | Chờ duyệt → Hàng đang về | Như trên | ☐ |
| PUR-W23 | P1 | Đã duyệt → Trả về nháp | Như trên | ☐ |
| PUR-W24 | P1 | Đã nhận → Hủy / Gửi duyệt / Duyệt | Như trên | ☐ |
| PUR-W25 | P1 | Đã hủy → bất kỳ bước nào | Như trên | ☐ |
| PUR-W26 | P1 | Nhập kho đơn Nháp / Chờ duyệt / Đã hủy (ép request Receive) | "Đơn chưa được duyệt, đã nhận đủ hoặc đã hủy nên chưa nhận hàng được." | ☐ |
| PUR-W27 | P1 | `orderId` không tồn tại | Thông báo chung, không lỗi 500 | ☐ |

### 2.4 Đồng thời
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-W30 | P1 | 2 tab cùng mở đơn Chờ duyệt: tab 1 Duyệt, tab 2 Trả về nháp | Chỉ 1 thành công; tab còn lại báo trạng thái không cho phép, gợi ý tải lại | ☐ |
| PUR-W31 | P1 | Tab 1 Hủy đơn Đang về; tab 2 (Thủ kho) bấm Nhập kho gần như cùng lúc | Chỉ 1 thành công; không có đơn Đã hủy mà tồn kho vẫn tăng | ☐ |
| PUR-W32 | P1 | Tab 1 đang mở hộp thoại Sửa; tab 2 Nhập kho; tab 1 Lưu | Tab 1 bị từ chối; giá vốn đã nhập không bị thay đổi | ☐ |
| PUR-W33 | P2 | Một người vừa Gửi duyệt vừa tự Duyệt đơn của mình | Thiết kế: được phép. Lịch sử ghi người thực hiện từng bước. | ☐ |

---

## 3. Nhập kho & cập nhật giá vốn

| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-R01 | P1 | Nút nhập kho | Chữ đúng: "Nhập Kho & Cập Nhật Giá Vốn (N món)" – N = số dòng hàng | ☐ |
| PUR-R02 | P1 | Bấm nút | Hộp xác nhận "Ghi số thực nhận vào tồn… không hoàn tác được." – bấm Quay lại thì không có gì xảy ra | ☐ |
| PUR-R03 | P1 | Xác nhận nhập kho đơn Đã duyệt (chưa bấm "Hàng đang về") | Nhận được; đơn → Đã nhận | ☐ |
| PUR-R04 | P1 | Xác nhận nhập kho đơn Đang về | Đơn → Đã nhận; cột "SL đã nhận" = SL đặt; lịch sử ghi người nhận, giờ | ☐ |
| PUR-R05 | P1 | Kiểm tra Kho sau nhập | Tồn thực tế tại **kho nhận** tăng đúng SL từng mặt hàng; kho khác không đổi; Lịch sử thay đổi của mặt hàng có dòng nhập | ☐ |
| PUR-R06 | P1 | Giá vốn bình quân, hàng **chưa có tồn** | Giá vốn = giá vốn/đơn vị của đơn | ☐ |
| PUR-R07 | P1 | Giá vốn bình quân, hàng đã có tồn: kho A 6 cái + kho B 4 cái @ 100.000 VNĐ; nhập 10 cái @ 200.000 VNĐ vào kho A | Giá vốn mới = (10×100.000 + 10×200.000) ÷ 20 = **150.000 VNĐ** (tính trên tổng tồn **mọi kho**) | ☐ |
| PUR-R08 | P1 | Trường hợp ra số lẻ: tồn 1 @ 100.000; nhập 2 @ 100.001 | (100.000 + 200.002) ÷ 3 = 100.000,67 → **100.001 VNĐ** | ☐ |
| PUR-R09 | P1 | Bấm đúp nút / gửi lại request Receive lần 2 / mở 2 tab cùng bấm | Chỉ nhập 1 lần ("Đơn đã nhập kho."); tồn không tăng gấp đôi | ☐ |
| PUR-R10 | P2 | Nhận **một phần** | Ô "SL nhận lần này" mặc định bằng số còn phải nhận. Nhận 6/10 (không tick đóng đơn) → **Đã nhận một phần**. Nhận nốt 4 → **Đã nhập kho**. Tick "Đóng đơn dù nhận thiếu" mà không ghi lý do → bị từ chối. | ☐ |
| PUR-R11 | P1 | Đơn còn khoản chi phí chưa gán nhóm → Nhập kho | "Còn khoản chi phí chưa gán nhóm…", không nhập | ☐ |
| PUR-R12 | P1 | Sau khi nhập: nút Sửa đơn, Hủy đơn, các nút trạng thái | Biến mất; xuất hiện form "Ghi điều chỉnh" | ☐ |
| PUR-R13 | P1 | Nhập kho trong lúc mặt hàng đang được giữ chỗ cho phiếu chuyển | Số giữ chỗ không đổi; tồn và khả dụng tăng đúng | ☐ |
| PUR-R14 | P1 | `/audit` | Có dòng nhận hàng đơn mua | ☐ |

### Điều chỉnh chi phí sau khi nhận
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-A01 | P1 | Đơn Đã nhận, hàng **còn nguyên trong kho**: ghi +1.000.000 VNĐ, chia theo FOB, có lý do | Dòng mới trong bảng điều chỉnh: "Vào giá vốn hàng còn tồn" = 1.000.000; "Chênh lệch" = 0; giá vốn bình quân các mặt hàng tăng tương ứng | ☐ |
| PUR-A02 | P1 | Hàng đã chuyển đi/hủy/điều chỉnh giảm một phần (ví dụ còn 60%) rồi ghi +1.000.000 | Phần vào giá vốn nhỏ hơn 1.000.000; phần còn lại nằm ở "Chênh lệch (hàng đã xuất)"; hai số cộng lại = 1.000.000 | ☐ |
| PUR-A03 | P1 | Ghi số **âm** (−500.000) | Giá vốn giảm; không làm giá vốn âm | ☐ |
| PUR-A04 | P1 | Số tiền 0 / bỏ trống tên khoản / bỏ trống lý do (ép request) | "Khoản điều chỉnh không hợp lệ…" | ☐ |
| PUR-A05 | P1 | Ép request `method` = Nhập tay | Bị từ chối | ☐ |
| PUR-A06 | P1 | Ép request điều chỉnh cho đơn chưa nhận / đã hủy | Bị từ chối | ☐ |
| PUR-A07 | P2 | Chia theo cân nặng khi có mặt hàng thiếu số đo | Báo thiếu số đo | ☐ |
| PUR-A08 | P2 | Nhập `1.000.000` (có dấu chấm), `abc`, hoặc số 23 chữ số | "abc" và rỗng bị từ chối. Số 23 chữ số: "Số tiền vượt quá giới hạn cho phép.", không lỗi 500. `999.999.999.999.999.999` vẫn lưu được. | ☐ |
| PUR-A09 | P1 | Sau khi ghi | Lịch sử đơn có mốc điều chỉnh; cột "Ai, lúc nào" đúng; `/audit` có dòng | ☐ |
| PUR-A10 | P2 | Ghi 2 khoản điều chỉnh liên tiếp | Cả hai được cộng dồn đúng | ☐ |

---

## 4. Phân quyền

### 4.1 Ma trận
| Hành động | AD / GĐ / KT | TK, ThK (kho nhận của đơn) | TK, ThK (kho khác) | SA / CS |
|---|---|---|---|---|
| Vào `/purchasing` | ✔ | ✔ | ✔ (không thấy đơn đó) | ✘ |
| Thấy đơn Nháp / Chờ duyệt / Đã hủy | ✔ | ✘ | ✘ | – |
| Thấy đơn Đã duyệt / Đang về / Đã nhận | ✔ | ✔ | ✘ | – |
| Thấy giá FOB, thuế, chi phí, giá vốn, tổng tiền trên thẻ | ✔ | ✘ | ✘ | – |
| Tạo, Sửa, Gửi duyệt, Duyệt, Trả về nháp, Hàng đang về, Hủy | ✔ | ✘ | ✘ | – |
| Nhập Kho & Cập Nhật Giá Vốn | ✔ | ✔ | ✘ | – |
| Ghi điều chỉnh chi phí | ✔ | ✘ | ✘ | – |
| Đính kèm / Xóa chứng từ | ✔ | ✘ | ✘ | – |
| Tải chứng từ | ✔ | ✔ (đơn mình thấy được) | ✘ | – |
| Xuất CSV | ✔ (đủ cột) | ✔ (không có cột tiền) | ✘ | – |

### 4.2 Test case
| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| PUR-P01 | P1 | SA, CS | Gõ `/purchasing` | Từ chối truy cập | ☐ |
| PUR-P02 | P1 | ThK kho A | Gõ `/purchasing` (không có mục ở sidebar – xem nghi vấn ở README) | Vào được; không có nút tạo đơn | ☐ |
| PUR-P03 | P1 | ThK kho A | Danh sách đơn | Chỉ có đơn **kho nhận = A** ở trạng thái Đã duyệt, Đang về, Đã nhận | ☐ |
| PUR-P04 | P1 | ThK kho A | Kho A chưa có đơn nào được duyệt | "Chưa có đơn mua nào về kho của bạn." | ☐ |
| PUR-P05 | P1 | ThK kho A | Gõ `/purchasing?order=<id đơn Nháp kho A>` và `<id đơn kho B>` | Không hiện chi tiết đơn | ☐ |
| PUR-P06 | P1 | ThK kho A | Khung chi tiết đơn của kho mình | **Không** có cột Giá FOB, Thuế NK, Trị giá hải quan, VAT, Chi phí phân bổ, Giá vốn; không có bảng chi phí; không có số tiền trên thẻ đơn; không có công thức giá vốn | ☐ |
| PUR-P07 | P1 | ThK kho A | Xem mã nguồn trang + Network | Không có con số tiền nào của đơn trong phản hồi | ☐ |
| PUR-P08 | P1 | ThK kho A | Không có: Sửa đơn, nút trạng thái, form điều chỉnh, form đính kèm, nút Xóa file | Đúng | ☐ |
| PUR-P09 | P1 | ThK kho A | Gõ `/purchasing?form=1`, `?edit=<id>` | Không mở hộp thoại lập/sửa đơn | ☐ |
| PUR-P10 | P1 | ThK kho A | Ép request: Create, Update, ChangeStatus (mọi action), AdjustCost, Upload, DeleteAttachment | Tất cả: "Bạn không có quyền thực hiện thao tác này."; dữ liệu không đổi | ☐ |
| PUR-P11 | P1 | ThK kho A | Nhập kho đơn Đang về của kho A | Được; tồn kho A tăng; sau đó ThK **vẫn không thấy** giá vốn | ☐ |
| PUR-P12 | P1 | ThK kho A | Ép request Receive với `orderId` đơn của kho B | Bị từ chối; tồn kho B không đổi | ☐ |
| PUR-P13 | P1 | ThK kho A | Ép request Receive đơn Nháp của kho A | Bị từ chối | ☐ |
| PUR-P14 | P1 | ThK kho A | Tải chứng từ của đơn kho mình | Tải được | ☐ |
| PUR-P15 | P1 | ThK kho A | Gõ link tải chứng từ của đơn kho B / đơn Nháp kho A | "Không tìm thấy chứng từ." | ☐ |
| PUR-P16 | P1 | ThK kho A | Xuất CSV đơn kho mình; ép `orderId` đơn kho B | Đơn mình: file không có cột tiền. Đơn kho B: bị từ chối | ☐ |
| PUR-P17 | P1 | TK kho A | Lặp lại PUR-P03 … P16 | Kết quả giống Thủ kho | ☐ |
| PUR-P18 | P1 | Thủ kho **chưa gán kho** | Gõ `/purchasing` | Không thấy đơn nào, không lỗi 500; ép Receive bị từ chối | ☐ |
| PUR-P19 | P1 | AD, GĐ, KT | Toàn bộ thao tác | Làm được; thấy đơn mọi kho, mọi trạng thái | ☐ |
| PUR-P20 | P1 | KT đang mở đơn; AD đổi KT thành Thủ kho kho A | KT bấm Duyệt đơn → bị từ chối | ☐ |
| PUR-P21 | P1 | ThK kho A đang thấy đơn Đang về; KT đổi kho nhận sang B | ThK tải lại: đơn biến mất; bấm Nhập kho trên trang cũ → bị từ chối | ☐ |
| PUR-P22 | P2 | Mọi vai trò | Gửi form thiếu token chống giả mạo | 400 | ☐ |
| PUR-U01 | P1 | KT mở form lập đơn, nhập "Thuế khác (VNĐ)" = 200000, tab ra ngoài; lưu rồi sửa lại | Ô hiện `200.000` khi gõ và khi mở lại; lưu đúng 200000 | ☐ |
| PUR-U02 | P1 | Gõ từ khóa và chọn bộ lọc trạng thái trong danh sách, bấm chọn một đơn | Từ khóa, trạng thái và trang vẫn còn; thẻ đơn đang chọn được tô | ☐ |
| PUR-U03 | P1 | Sau PUR-U02 thực hiện một thao tác (gửi duyệt, duyệt, hủy) | Quay lại đúng đơn, danh sách vẫn giữ bộ lọc | ☐ |
| PUR-U04 | P1 | Lọc theo từng trạng thái (Nháp, Chờ duyệt, Đã duyệt, Đang về, Đã nhận, Đã hủy); thử `?status=abc` | Chỉ ra đơn đúng trạng thái; giá trị lạ coi như không lọc | ☐ |
| PUR-U05 | P1 | Nhận hàng với số thực nhận vượt số còn phải nhận, lần lượt: nhận một phần, có tick "Đóng đơn dù nhận thiếu", và lần nhận cuối; cuộn giữa trang trước khi bấm | Cả ba lần đều bị từ chối, tồn và đơn không đổi; toast lỗi góc dưới phải nêu mã mặt hàng, số đã nhập, số còn phải nhận; không có banner ở đầu trang. Ô số lượng không cho gõ quá số còn phải nhận (trình duyệt báo) | ☐ |
| PUR-U06 | P2 | Mở chi tiết đơn ở mọi trạng thái, menu mở và thu nhỏ, màn 1024 và 1920 | Trạng thái nằm cạnh mã đơn sát phải; các nút cùng cỡ, chữ không xuống dòng, tối đa 2 hàng | ☐ |

---

## 5. Workflow đầu–cuối trên màn hình này

| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| PUR-E01 | P1 | KT lập đơn → KT gửi duyệt → GĐ duyệt → KT bấm Hàng đang về → ThK kho nhận nhập kho → KT ghi điều chỉnh chi phí | Trọn luồng; Lịch sử đơn có đủ 6 mốc đúng thứ tự, đúng người | ☐ |
| PUR-E02 | P1 | KT lập đơn → gửi duyệt → GĐ trả về nháp (lý do) → KT sửa → gửi lại → GĐ duyệt → KT hủy (lý do) | Đơn Đã hủy; tồn kho không đổi; NCC có thể ngừng hợp tác nếu không còn đơn khác | ☐ |
| PUR-E03 | P1 | Trong suốt PUR-E01, ThK kho nhận tải lại trang ở mỗi bước | Chỉ thấy đơn từ lúc Đã duyệt; chưa bao giờ thấy tiền | ☐ |
| PUR-E04 | P1 | Sau PUR-E01: mở Kho → chi tiết mặt hàng | Lịch sử thay đổi có dòng nhập từ đơn mua; Danh mục hiện giá vốn mới | ☐ |
