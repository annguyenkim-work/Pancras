# Module: Mua hàng và container nhập (`/purchasing`)

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Quyền theo vai: [../roles-and-permissions.md](../roles-and-permissions.md) mục 4. Nhập kho và tồn: [inventory.md](inventory.md).

**Mục đích:** Luật nghiệp vụ của đơn mua nhập khẩu, phân bổ chi phí, tính giá vốn (landed cost), nhận hàng và điều chỉnh chi phí. Code: `Domain/Purchasing`, `Application/Purchasing`, `Infrastructure/Persistence/PurchaseOrderRepository.cs`, `Web/Pages/Purchasing`.

## 1. Thực thể

| Thực thể | Vai trò |
| --- | --- |
| `PurchaseOrder` | Đơn mua: nhà cung cấp, kho nhận, container/B-L/cảng, ngày dự kiến, tỷ giá, VAT, trạng thái |
| `PurchaseLine` | Dòng hàng: mặt hàng, số lượng, **FOB USD của dòng**, thuế suất nhập khẩu và các thuế riêng, số đã nhận, giá vốn đã vào kho |
| `PurchaseCharge` + `PurchaseChargeShare` | Khoản chi phí của đơn, cách chia, phần chia cho từng dòng |
| `PurchaseEvent` | Lịch sử: ai làm gì, lúc nào, ghi chú |
| `PurchaseCostAdjustment` | Điều chỉnh chi phí sau khi đã nhận đủ |
| `PurchaseAttachment` | Chứng từ: tờ khai, hóa đơn NCC, vận đơn, packing list, hóa đơn logistics, khác |

- **Nhà cung cấp được chụp lại trên đơn** (mã, tên tại lúc lập đơn): đổi hồ sơ NCC sau đó không đổi chứng từ đã lập.
- **Tỷ giá** ghi kèm ngày và nguồn (ví dụ tờ khai hải quan); sửa tỷ giá làm tính lại chi phí.
- `Version` là concurrency token: form đang mở bị từ chối nếu đơn đã được sửa.
- **Thuế:** mỗi dòng một suất thuế nhập khẩu (`DutyRate`); cả đơn một suất VAT (`VatRate`). Không gộp hai thuế thành một khoản chung của container rồi chia xuống dòng.
- **Trùng container/B-L:** sau khi lưu đơn, nếu số container hoặc B/L trùng một đơn khác chưa hủy thì hiện cảnh báo (không chặn); so khớp không phân biệt hoa thường và bỏ khoảng trắng.
- **Tỷ giá chốt lúc lập đơn**; chỉ sửa được khi đơn còn sửa được (trước lần nhận đầu tiên).
- **FOB phải lớn hơn 0** khi tạo, sửa và nhận hàng; số lượng mỗi dòng phải lớn hơn 0 (dòng đã chọn mặt hàng mà số lượng ≤ 0 bị từ chối, dòng trống hoàn toàn thì bỏ qua).
- Làm tròn: USD 2 chữ số, đồng 0 chữ số (`AwayFromZero`), thuế suất 4 chữ số. Mọi số phải nằm trong giới hạn cột (`PurchaseMoney.FitsLimits`) trước khi lưu hoặc tính.

## 2. Vòng đời đơn (`PurchaseStatus`)

```
Draft ──gửi duyệt──▶ PendingApproval ──duyệt──▶ Approved ──đang về──▶ InTransit
  ▲                        │                       │                     │
  └──────trả về nháp───────┘                       └──nhận hàng──────────┤
                                                                         ▼
                                      PartiallyReceived ──nhận tiếp──▶ Received
```

- **Hủy** (bắt buộc lý do): khi đơn còn sửa được (`Draft`, `PendingApproval`, `Approved`, `InTransit`) và **chưa nhận món nào**.
- **Còn sửa được** thông tin, mặt hàng, chi phí: trước lần nhận đầu tiên và chưa hủy.
- **Nhận hàng được** khi đơn `Approved`, `InTransit` hoặc `PartiallyReceived`.
- Mọi bước ghi một `PurchaseEvent`. **Không tách nhiệm vụ:** Admin, Giám đốc, Kế toán làm được mọi bước, kể cả tự duyệt đơn mình lập; bù bằng lịch sử sự kiện mọi bước.

## 3. Công thức giá vốn (landed cost)

Mỗi dòng, `T` = tỷ giá của đơn:

| Đại lượng | Công thức |
| --- | --- |
| FOB quy đổi | `round(FobUsd × T)` (FobUsd là tổng USD của dòng, **không phải đơn giá**) |
| Trị giá hải quan | FOB quy đổi + phần chia nhóm "điều chỉnh trị giá hải quan" |
| Thuế nhập khẩu | trị giá hải quan × thuế suất dòng |
| Thuế khác | thuế bổ sung + thuế tiêu thụ đặc biệt + thuế bảo vệ môi trường |
| Giá tính VAT | trị giá hải quan + thuế nhập khẩu + thuế khác |
| VAT | giá tính VAT × `VatRate` |
| VAT vào giá vốn | `0` nếu `VatDeductible` (được khấu trừ); bằng VAT nếu không được khấu trừ |
| **Giá vốn dòng** | trị giá hải quan + thuế nhập khẩu + thuế khác + phần chia nhóm "thuế không được khấu trừ" + phần chia nhóm logistics + VAT vào giá vốn |
| **Đơn giá vốn** | giá vốn dòng ÷ số lượng, làm tròn đồng |

- **VAT được khấu trừ không vào giá vốn.** Nhóm "thuế được khấu trừ" chỉ ghi nhận, không cộng vào giá vốn.
- **Chi phí chia 4 nhóm:** điều chỉnh trị giá hải quan, logistics, thuế được khấu trừ, thuế không được khấu trừ.
- **Mỗi khoản chi phí chỉ thuộc một nhóm**; người lập đơn gắn nhóm cho từng khoản.
- **Gợi ý mặc định khi lập đơn:** cước biển → nhóm *Điều chỉnh trị giá hải quan* (có vào giá tính VAT); THC, D/O, kiểm định, kéo xe, vệ sinh container → nhóm *Logistics* (không vào giá tính VAT, có vào giá vốn). Mặc định chia theo **thể tích**; thiếu cân nặng/thể tích của mặt hàng thì báo ngay và từ chối (`MissingMeasure`), không tự đổi sang cách chia khác.
- **Chia chi phí theo 5 cách:** giá trị FOB, số lượng, cân nặng, thể tích, nhập tay. Phần lẻ do làm tròn dồn về dòng có thứ tự lớn nhất (`SortOrder`); chia nhập tay cần chia lại khi tập dòng đổi.

## 4. Nhận hàng và giá vốn bình quân

- Nhận theo **số thực nhận từng dòng**, có thể nhiều lần.
- **Lần chưa phải cuối:** mỗi món vào kho theo giá vốn dự toán (đơn giá vốn dòng); **không nhận quá số đặt**.
- **Lần cuối:** phần giá vốn còn lại của dòng dồn hết vào số nhận lần này, nên **tổng giá vốn vào kho đúng bằng giá vốn dòng** dù nhận thiếu hay thừa. Dòng không nhận thêm nhưng đã nhận trước đó thì phần còn lại dồn vào hàng đã nhận; dòng không nhận món nào thì không vào tồn kho.
- **Lần cuối lệch số đặt phải có ghi chú lý do.** Đơn đã `Received` không nhận thêm được.
- **Giá vốn bình quân gia quyền** trên **tổng tồn mọi địa điểm**, làm tròn đồng (`AwayFromZero`): `(tồn × giá hiện tại + số nhận × đơn giá vốn lần nhận) ÷ (tồn + số nhận)`. Hết tồn thì lấy đơn giá lần nhận.
- Nhận hàng ghi `StockMovement` loại `Receipt` tại **kho nhận của đơn**; thủ kho và Trưởng Showroom/Kho nhận đúng đơn về kho mình.
- Nhận hàng là một giao dịch trọn vẹn; lưu thất bại thì hoàn lại cả đơn lẫn tồn (`UndoReceipt`).
- Nút nhận hàng ghi **"Nhập Kho & Cập Nhật Giá Vốn"** (test cấm chuỗi `Nhập kho` trần trên nút này).

## 5. Điều chỉnh chi phí sau khi nhận đủ

Dùng cho hóa đơn về muộn, tờ khai điều chỉnh:

- Chỉ vai quản lý (Admin, Giám đốc, Kế toán); **bắt buộc lý do**; ghi `PurchaseEvent` loại `CostAdjusted`.
- Số tiền **có dấu** (dương = tăng giá vốn), kèm nhãn và cách chia.
- **Phần ứng với hàng còn tồn** cộng vào giá vốn bình quân (`AppliedToStock`); **phần ứng với hàng đã xuất** là chênh lệch ghi nhận (`Variance = Amount − AppliedToStock`), **không sửa ngược** giá vốn đã xuất.

## 6. Chứng từ và quyền xem

- Chứng từ đính kèm lưu ngoài database; tải xuống qua handler có kiểm quyền (xem [security-and-authorization.md](../../02-coding-rules/security-and-authorization.md) mục 4).
- **Giá FOB, thuế, giá vốn chỉ vai quản lý thấy** (kể cả file xuất). Thủ kho và Trưởng Showroom/Kho chỉ thấy đơn đã duyệt trở đi của kho mình, không thấy giá.
- Xuất danh sách đơn ra CSV (nút ghi "Xuất Excel" nhưng là CSV).

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| Thứ tự enum | `PurchaseStatus` đánh số theo thời điểm thêm (`Received = 2`, `PendingApproval = 3`...), không theo vòng đời; chỉ đổi khi có migration |
| Hiển thị giá vốn đơn chưa nhập | Cần phân biệt số dự toán và số đã ghi nhận |

## Chờ chốt (chưa có hiệu lực)

Các mục từ review 14/11/2026 chưa xác minh trạng thái trong code, chờ rà khi làm lại module:
- **Đã chốt 2026-11-16 (chưa làm):** trước khi nhận kho hiện bản xem trước tác động giá vốn (giá vốn cũ → mới từng mặt hàng) để xác nhận; nút giữ nhãn `Nhập Kho & Cập Nhật Giá Vốn`.
- **Đã chốt 2026-11-16:** không bắt buộc chứng từ (tờ khai, hóa đơn NCC) trước khi nhận kho; đính kèm tùy chọn.
- Nối đơn mua với công nợ phải trả nhà cung cấp và thanh toán.
- **Đã chốt 2026-11-16:** chưa cần duyệt theo ngưỡng giá trị hay người duyệt thay thế; giữ Admin, Giám đốc, Kế toán duyệt.
- Lọc danh sách đơn theo trạng thái/kho/ngày (phân trang 20/50 đã có).
