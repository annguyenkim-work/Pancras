# 09 – Kho: tab "Kiểm kê & điều chỉnh" (`/inventory?view=stocktakes`)

Tab gồm 3 loại phiếu trên cùng một màn hình:

| Loại | Nút mở | Hộp thoại | Các ô |
|---|---|---|---|
| **Biên bản kiểm kê** (mã KK…) | "+ Lập Biên Bản Kiểm Kê" | Lập Biên Bản Kiểm Kê Kho | Địa điểm kiểm kê, Mặt hàng kiểm đếm, dòng "Tồn sổ sách: …", Số lượng thực tế kiểm đếm được, Lý do giải trình chênh lệch, Chứng từ, File đính kèm |
| **Phiếu điều chỉnh số lượng** | "+ Phiếu điều chỉnh" | Phiếu điều chỉnh số lượng | Địa điểm, Mặt hàng, Chênh lệch (số có dấu), Lý do, Chứng từ, File đính kèm |
| **Phiếu hủy** | "+ Phiếu hủy" | Phiếu hủy một sản phẩm | Địa điểm, Mặt hàng, Lý do, Chứng từ, File đính kèm. **Mỗi phiếu hủy đúng 1 đơn vị** |

Danh sách:
- Thẻ biên bản kiểm kê: mã, kho, nhãn trạng thái, dấu vết; bảng Mã SKU · Tên · ĐVT · Tồn sổ sách · Tồn thực đếm · Chênh lệch · Lý do; nút **Duyệt biên bản**.
- Mục **Điều chỉnh số lượng**: danh sách phiếu + nút **Duyệt điều chỉnh**.
- Mục **Phiếu hủy**: danh sách phiếu + nút **Duyệt hủy**.

Trạng thái phiếu: **Chờ duyệt → Đã duyệt**. Riêng phiếu điều chỉnh có thêm **Đã ghi nhận** (tự sinh khi nhận hàng chuyển kho bị lệch – không cộng trừ tồn, không duyệt được).

Quy tắc chung:
- Admin, Giám đốc, Kế toán lập phiếu → **ghi thẳng** (Đã duyệt ngay, tồn đổi ngay).
- Trưởng kho, Thủ kho lập phiếu → **Chờ duyệt**, tồn chưa đổi.
- Cả 3 loại phiếu **không làm đổi giá vốn**.

---

## A. Biên bản kiểm kê

### A.1 Chức năng
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| STK-01 | P1 | Chọn địa điểm + mặt hàng | Dòng "Tồn sổ sách" hiện đúng tồn thực tế của mặt hàng tại kho đó; đổi kho/mặt hàng thì số đổi theo | ☐ |
| STK-02 | P1 | Số đếm **= tồn sổ sách**, không lý do, không chứng từ | Lưu được (không lệch thì không cần giải trình) | ☐ |
| STK-03 | P1 | Số đếm **≠ tồn sổ sách**, bỏ trống lý do hoặc chứng từ | "Cần lý do và chứng từ." | ☐ |
| STK-04 | P1 | Số đếm ≠ sổ sách, đủ lý do + chứng từ | Lưu được; thẻ biên bản hiện Tồn sổ sách, Tồn thực đếm, Chênh lệch (±), Lý do | ☐ |
| STK-05 | P1 | Số đếm âm (ép request) / bỏ trống | Bị từ chối | ☐ |
| STK-06 | P1 | Không chọn địa điểm / mặt hàng | Bị từ chối, không lỗi 500 | ☐ |
| STK-07 | P2 | Mặt hàng chưa từng có ở kho đó (sổ sách 0), đếm được 2 | Lưu được với đủ lý do/chứng từ; khi duyệt tồn thành 2 | ☐ |
| STK-08 | P2 | Đính kèm ảnh (JPG/PNG), PDF; file > 10 MB; file sai loại | Hợp lệ lưu được và tải lại được; còn lại báo lỗi, **không tạo** biên bản | ☐ |
| STK-09 | P2 | Lý do / Chứng từ rất dài | Báo lỗi hoặc cắt – không lỗi 500 | ☐ |
| STK-10 | P2 | Chưa có biên bản | "Chưa có biên bản kiểm kê." | ☐ |
| STK-11 | P1 | Nhãn trạng thái | Chờ duyệt (vàng) / Đã duyệt (xanh) | ☐ |

### A.2 Workflow
| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| STK-W01 | P1 | ThK-A lập (sổ 10, đếm 8) | **Chờ duyệt**; tồn kho A vẫn 10 | ☐ |
| STK-W02 | P1 | TK-A bấm "Duyệt biên bản" phiếu STK-W01 | Đã duyệt; tồn A = **8**; lịch sử mặt hàng có dòng kiểm kê −2; giá vốn không đổi | ☐ |
| STK-W03 | P1 | KT lập (sổ 8, đếm 9) | **Đã duyệt ngay**; tồn A = 9 ngay; không có nút duyệt | ☐ |
| STK-W04 | P1 | TK-A lập biên bản kho A | Chờ duyệt; TK-A **không** tự duyệt được; KT/GĐ/AD duyệt được | ☐ |
| STK-W05 | P1 | Duyệt lần 2 cùng 1 biên bản (2 tab / gửi lại request) | Lần 2 bị từ chối; tồn không đổi lần 2 | ☐ |
| STK-W06 | P1 | **Sổ sách thay đổi giữa lúc lập và lúc duyệt**: ThK lập (sổ 10, đếm 8) → nhập thêm 5 (tồn 15) → duyệt | Ghi lại kết quả: tồn thành 8 (ghi đè theo số đếm) hay 13 (áp chênh lệch −2). **Xác nhận với BA cách nào đúng** | ☐ |
| STK-W07 | P1 | Kiểm kê giảm tồn xuống **dưới số đang giữ chỗ** (đang giữ 4 cho phiếu chuyển, đếm 3) | Ghi lại: bị chặn hay cho phép. Nếu cho phép, sau đó xuất kho phiếu chuyển phải báo thiếu rõ ràng – không ra số âm | ☐ |
| STK-W08 | P2 | Lập 2 biên bản chờ duyệt cho cùng mặt hàng, cùng kho; duyệt cả hai | Ghi lại kết quả tồn cuối; không lỗi | ☐ |
| STK-W09 | P1 | Sau mỗi lần duyệt | Dấu vết trên thẻ ghi người lập, người duyệt, giờ | ☐ |

### A.3 Phân quyền
| Hành động | AD/GĐ/KT | TK | ThK | SA |
|---|---|---|---|---|
| Lập biên bản | ✔ mọi kho, ghi thẳng | ✔ kho mình, chờ duyệt | ✔ kho mình, chờ duyệt | ✘ |
| Duyệt biên bản do **Thủ kho** lập | ✔ | ✔ nếu cùng kho | ✘ | ✘ |
| Duyệt biên bản do **Trưởng kho** lập | ✔ | ✘ | ✘ | ✘ |
| Thấy biên bản | Mọi kho | Kho mình | Kho mình | ✘ |

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| STK-P01 | P1 | ThK-A | Danh sách "Địa điểm kiểm kê" | Chỉ có kho A | ☐ |
| STK-P02 | P1 | ThK-A | Ép request lập biên bản cho kho B | "Bạn không có quyền thao tác với kho này." | ☐ |
| STK-P03 | P1 | ThK-A | Danh sách biên bản | Chỉ biên bản kho A | ☐ |
| STK-P04 | P1 | ThK-A | Nút "Duyệt biên bản" | Không có; ép request `ApproveStocktake` → "Bạn không được duyệt phiếu này." | ☐ |
| STK-P05 | P1 | TK-A | Biên bản của ThK-A | Có nút duyệt; duyệt được | ☐ |
| STK-P06 | P1 | TK-A | Biên bản do **chính TK-A** lập | **Không** nên có nút duyệt. Nếu nút vẫn hiện, bấm phải bị từ chối – ghi lại (nghi vấn: nút hiện thừa) | ☐ |
| STK-P07 | P1 | TK-B | Ép request duyệt biên bản kho A (sửa `DocumentId`) | Bị từ chối; tồn A không đổi | ☐ |
| STK-P08 | P1 | KT, GĐ, AD | Duyệt biên bản của ThK và của TK, ở mọi kho | Được | ☐ |
| STK-P09 | P1 | SA | Ép request `CreateStocktake`, `ApproveStocktake` | Bị từ chối | ☐ |
| STK-P10 | P1 | Thủ kho chưa gán kho | Lập biên bản | Không có địa điểm để chọn; ép request bị từ chối | ☐ |
| STK-P11 | P1 | TK-A | Tải file đính kèm của biên bản kho B (sửa id trên link) | Không tải được | ☐ |

---

## B. Phiếu điều chỉnh số lượng

### B.1 Chức năng
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| ADJ-01 | P1 | Chênh lệch +2, có lý do + chứng từ | Lưu được | ☐ |
| ADJ-02 | P1 | Chênh lệch −2, có lý do + chứng từ | Lưu được | ☐ |
| ADJ-03 | P1 | Bỏ trống lý do **hoặc** chứng từ (kể cả khi chênh lệch dương) | "Cần lý do và chứng từ." | ☐ |
| ADJ-04 | P1 | Chênh lệch = 0 | Bị từ chối. Ghi lại câu thông báo (hiện có thể ra "Tồn không được âm." – câu chưa đúng ý, mức thấp) | ☐ |
| ADJ-05 | P1 | Chênh lệch âm lớn hơn tồn (tồn 3, nhập −5) – vai trò tài chính | "Tồn không được âm."; tồn không đổi; không có phiếu nào được tạo | ☐ |
| ADJ-06 | P2 | Nhập số thập phân / chữ vào ô Chênh lệch | Bị chặn, không lỗi 500 | ☐ |
| ADJ-07 | P2 | File đính kèm hợp lệ / không hợp lệ | Như STK-08 | ☐ |
| ADJ-08 | P1 | Phiếu **"Đã ghi nhận"** (sinh từ nhận hàng chuyển kho lệch) | Hiện trong danh sách, có mã phiếu chuyển liên quan, **không** có nút duyệt | ☐ |
| ADJ-09 | P1 | Ép request `ApproveAdjustment` với phiếu "Đã ghi nhận" | Bị từ chối; tồn không đổi | ☐ |

### B.2 Workflow
| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| ADJ-W01 | P1 | ThK-A lập +2 | Chờ duyệt; tồn chưa đổi; Admin/GĐ/KT nhận thông báo | ☐ |
| ADJ-W02 | P1 | KT bấm "Duyệt điều chỉnh" | Đã duyệt; tồn +2; lịch sử mặt hàng có dòng điều chỉnh; giá vốn không đổi | ☐ |
| ADJ-W03 | P1 | KT lập −1 | Ghi thẳng: Đã duyệt, tồn −1 ngay | ☐ |
| ADJ-W04 | P1 | ThK-A lập −5 khi tồn 6 → trong lúc chờ, xuất chuyển 3 (tồn 3) → KT duyệt | "Tồn không được âm."; phiếu vẫn Chờ duyệt; tồn giữ 3 | ☐ |
| ADJ-W05 | P1 | Duyệt 2 lần (2 tab) | Chỉ áp 1 lần | ☐ |
| ADJ-W06 | P1 | TK-A lập phiếu | Chờ duyệt như Thủ kho | ☐ |
| ADJ-W07 | P1 | Điều chỉnh âm xuống dưới số đang giữ chỗ | Ghi lại hành vi (như STK-W07) | ☐ |

### B.3 Phân quyền
| Hành động | AD/GĐ/KT | TK | ThK | SA |
|---|---|---|---|---|
| Lập phiếu | ✔ ghi thẳng, mọi kho | ✔ chờ duyệt, kho mình | ✔ chờ duyệt, kho mình | ✘ |
| Duyệt phiếu | ✔ | ✘ | ✘ | ✘ |

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| ADJ-P01 | P1 | TK-A, ThK-A | Nút "Duyệt điều chỉnh" | Không có | ☐ |
| ADJ-P02 | P1 | TK-A | Ép request `ApproveAdjustment` phiếu kho mình | "Bạn không được duyệt phiếu này."; tồn không đổi | ☐ |
| ADJ-P03 | P1 | ThK-A | Ép request lập phiếu cho kho B | "Bạn không có quyền thao tác với kho này." | ☐ |
| ADJ-P04 | P1 | ThK-A | Danh sách phiếu | Chỉ phiếu kho A | ☐ |
| ADJ-P05 | P1 | SA | Ép request lập / duyệt | Bị từ chối | ☐ |
| ADJ-P06 | P1 | KT, GĐ, AD | Lập và duyệt ở mọi kho | Được | ☐ |

---

## C. Phiếu hủy

### C.1 Chức năng
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| DSP-01 | P1 | Chọn kho, mặt hàng, nhập lý do + chứng từ → Lưu phiếu hủy | Tạo phiếu hủy cho **1 đơn vị** | ☐ |
| DSP-02 | P1 | Bỏ trống lý do hoặc chứng từ | "Cần lý do và chứng từ." | ☐ |
| DSP-03 | P1 | Mặt hàng có tồn 0 tại kho đó – vai trò tài chính (ghi thẳng) | "Tồn không được âm."; **không** có phiếu nào được lưu | ☐ |
| DSP-04 | P2 | Hộp thoại không có ô số lượng | Đúng thiết kế (hủy từng cái). Muốn hủy 3 cái phải lập 3 phiếu – ghi nhận góp ý nếu bất tiện | ☐ |
| DSP-05 | P2 | Đính kèm ảnh hàng hỏng | Lưu và tải lại được | ☐ |

### C.2 Workflow
| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| DSP-W01 | P1 | ThK-A lập | Chờ duyệt; tồn chưa đổi | ☐ |
| DSP-W02 | P1 | TK-A bấm "Duyệt hủy" | Đã duyệt; tồn −1; lịch sử mặt hàng có dòng hủy; giá vốn không đổi | ☐ |
| DSP-W03 | P1 | KT lập | Ghi thẳng; tồn −1 ngay | ☐ |
| DSP-W04 | P1 | TK-A lập | Chờ duyệt; chỉ AD/GĐ/KT duyệt được | ☐ |
| DSP-W05 | P1 | ThK lập 2 phiếu hủy khi tồn = 1 → duyệt cả hai | Phiếu 1 duyệt được; phiếu 2 "Tồn không được âm.", vẫn Chờ duyệt | ☐ |
| DSP-W06 | P1 | Duyệt 2 lần 1 phiếu (2 tab) | Chỉ trừ 1 lần | ☐ |
| DSP-W07 | P1 | Hủy cái cuối cùng đang được **giữ chỗ** cho phiếu chuyển | Ghi lại hành vi (như STK-W07) | ☐ |

### C.3 Phân quyền
| Hành động | AD/GĐ/KT | TK | ThK | SA |
|---|---|---|---|---|
| Lập phiếu hủy | ✔ ghi thẳng | ✔ chờ duyệt, kho mình | ✔ chờ duyệt, kho mình | ✘ |
| Duyệt phiếu do Thủ kho lập | ✔ | ✔ nếu cùng kho | ✘ | ✘ |
| Duyệt phiếu do Trưởng kho lập | ✔ | ✘ | ✘ | ✘ |

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| DSP-P01 | P1 | ThK-A | Ép request lập phiếu hủy ở kho B | Bị từ chối | ☐ |
| DSP-P02 | P1 | ThK-A | Ép request `ApproveDisposal` phiếu của mình | "Bạn không được duyệt phiếu này." | ☐ |
| DSP-P03 | P1 | TK-A | Duyệt phiếu do chính mình lập | Bị từ chối (nút không nên hiện – ghi lại nếu hiện) | ☐ |
| DSP-P04 | P1 | TK-B | Ép request duyệt phiếu kho A | Bị từ chối | ☐ |
| DSP-P05 | P1 | SA | Ép request lập / duyệt | Bị từ chối | ☐ |
| DSP-P06 | P1 | KT, GĐ, AD | Duyệt mọi phiếu | Được | ☐ |

---

## D. Kiểm tra chung cho cả tab

| ID | P | Kiểm tra | Kỳ vọng | KQ |
|---|---|---|---|---|
| DOC-01 | P1 | Nút đầu tab theo vai trò | AD/GĐ/KT/TK/ThK: đủ 3 nút "+ Lập Biên Bản Kiểm Kê", "+ Phiếu điều chỉnh", "+ Phiếu hủy" | ☐ |
| DOC-02 | P1 | 2 người thao tác cùng mặt hàng, cùng kho, gần như cùng lúc (duyệt kiểm kê + xuất chuyển kho) | Một bên bị từ chối với thông báo rõ; tồn cuối khớp với đúng 1 thao tác thành công; không âm | ☐ |
| DOC-03 | P1 | Sau toàn bộ file này: mở tab Tra cứu tồn | Không có số âm; cộng dồn lịch sử từng mặt hàng = tồn hiện tại | ☐ |
| DOC-04 | P1 | `/audit` | Có dòng duyệt kiểm kê / điều chỉnh / hủy | ☐ |
| DOC-05 | P2 | Hộp thoại lập phiếu khi lỗi | Mở lại, dữ liệu còn (trừ file phải chọn lại) | ☐ |
| DOC-06 | P2 | Gửi form thiếu token chống giả mạo | 400 | ☐ |
