# Module: Kho (`/inventory`)

> Trạng thái: **Đã chốt** (phiên 2026-11-16). Quyền theo vai: xem [../roles-and-permissions.md](../roles-and-permissions.md) mục 3, không lặp lại ở đây.

**Mục đích:** Luật nghiệp vụ của tra cứu tồn, chuyển kho, kiểm kê, điều chỉnh, hủy hàng. Code: `Domain/Inventory`, `Application/Inventory`, `Infrastructure/Persistence/InventoryRepository.cs`, `Web/Pages/Inventory`.

## 1. Thực thể

| Thực thể | Vai trò |
| --- | --- |
| `StockLocation` | Kho hoặc showroom (`StockLocationKind`); có tên, `Kind`, cờ vô hiệu |
| `StockBalance` | Tồn của một mặt hàng tại một địa điểm: `OnHand`, `Reserved`, `InTransit`, `Version` |
| `StockMovement` | Dòng biến động (`Receipt`, `Issue`, `Transfer`, `Adjustment`): mặt hàng, địa điểm, số lượng, chứng từ, thời điểm |
| `TransferSlip` + `TransferLine` | Phiếu chuyển kho và các dòng hàng |
| `TransferEvent` | Lịch sử từng bước của phiếu chuyển (ai, lúc nào, lý do) |
| `StocktakeSheet` | Biên bản kiểm kê: số sổ sách, số đếm, chênh lệch |
| `QuantityAdjustment` | Phiếu điều chỉnh số lượng (`Delta`) |
| `DisposalSlip` | Phiếu hủy hàng |
| `Notification` | Thông báo trong phần mềm cho từng người nhận |
| `StockDocumentAttachment`, `TransferAttachment` | Tệp đính kèm chứng từ |

- Mã hiển thị chứng từ: tiền tố + 8 ký tự hex lấy từ phần ngẫu nhiên của Id, ví dụ `KK-1A2B3C4D` (`StockDocumentCode`), không có ràng buộc duy nhất ở database. **Phiếu chuyển kho dùng số tuần tự duy nhất** `CK-000123` (xem mục "Phiếu chuyển kho in được" bên dưới); kiểm kê `KK`, điều chỉnh `DC`, hủy `HY` giữ nguyên.
- Chứng từ kế thừa dấu vết bản ghi (`AuditStamped`): ai ghi lần cuối và lúc nào.

## 2. Luật tồn kho (bất biến)

1. **Ba số tồn và số khả dụng:** `OnHand` (tồn thực), `Reserved` (đang giữ chỗ), `InTransit` (đang chuyển). **Khả dụng = `OnHand` − `Reserved`.** Hàng đang chuyển không tính vào khả dụng của kho nào.
2. **Tồn không bao giờ âm.** Thao tác làm tồn âm bị từ chối, không đổi số.
3. **Điều chỉnh số lượng không được đưa tồn dưới số đang giữ chỗ.** **Kiểm kê thì được** (khả dụng có thể âm, giao diện hiện 0), nhưng vẫn không được âm tồn thực.
4. **Mọi thay đổi tồn đi qua chứng từ** (phiếu chuyển, kiểm kê, điều chỉnh, hủy, nhận hàng mua) và **đều có dòng `StockMovement`**. Không sửa tồn trực tiếp.
5. **Ghi đồng thời an toàn:** `StockBalance.Version` là concurrency token; lần ghi thua bị từ chối, không retry (xem [data-access.md](../../02-coding-rules/data-access.md)).
6. **Tạo phiếu chuyển và các bước ghi tồn nằm trong một giao dịch repository**; thất bại thì phiếu và số tồn không đổi.
7. **Nhập kho từ mua hàng** cập nhật giá vốn bình quân trên tổng tồn mọi địa điểm, làm tròn đồng (xem [purchasing.md](purchasing.md)). Điều chỉnh, kiểm kê và hủy **không đổi giá vốn**.

## 3. Phiếu chuyển kho

Vòng đời (`TransferStatus`):

```
Requested ──duyệt──▶ Approved ──xuất kho──▶ InTransit ──nhận đủ──▶ Received
    │                   │                       │
    │ từ chối           │ hủy (nhả giữ chỗ)     ├─ nhận lệch ─▶ ReceivedWithDiscrepancy ─xử lý─▶ Received
    ▼                   ▼                       └─ trả hàng ──▶ Returned
 Rejected            Cancelled
```

- `Draft` còn trong enum nhưng **không dùng** (luồng nháp đã bỏ).
- **Lập phiếu:** kho xuất ≠ nơi nhận; ít nhất một dòng, số lượng > 0. Kho xuất hoặc nơi nhận phải là kho của người lập (trừ tài chính). Sửa phiếu chỉ khi còn `Requested`, chỉ người lập.
- **Duyệt:** phải đủ tồn khả dụng ở kho xuất; **duyệt giữ hàng** (tăng `Reserved`, chưa trừ `OnHand`). Từ chối bắt buộc lý do.
- **Hủy** (chỉ trước khi xuất, bắt buộc lý do): phiếu đã duyệt thì nhả giữ chỗ; tồn thực không đổi.
- **Xuất kho:** trừ `OnHand` và `Reserved` ở kho xuất đúng số trên phiếu, cộng `InTransit` ở nơi nhận.
- **Nhận hàng:** nhập số thực nhận từng dòng (không nhập = nhận đủ). `InTransit` giảm đúng số đã xuất; `OnHand` nơi nhận tăng đúng số thực nhận.
- **Nhận lệch** (khác số xuất): bắt buộc lý do và tệp đính kèm; tự lập `QuantityAdjustment` trạng thái `Recorded` ("Đã ghi nhận") cho phần chênh, **không cộng trừ tồn lần nữa**; phiếu sang `ReceivedWithDiscrepancy` chờ Admin, Giám đốc, Kế toán xử lý; xử lý xong phiếu thành `Received`.
- **Trả hàng đang vận chuyển** về kho xuất (chỉ khi `InTransit`, bắt buộc lý do): bỏ phần đang chuyển ở nơi nhận, cộng lại tồn kho xuất → `Returned`.
- **Lịch sử:** mỗi bước ghi người làm, thời điểm, lý do; không ghi nội dung đã sửa (chi tiết ở nhật ký audit).
- **Chống lập trùng:** phiếu mang khóa yêu cầu (`RequestKey`) để gửi lặp không tạo hai phiếu.

### Phiếu chuyển kho in được (đã làm 2026-11-18)

Nguồn: UAT sprint 1. Thay quyết định 2026-11-16 "chưa cần phiếu giao in được" **chỉ cho phiếu chuyển kho `CK`**. Kiểm kê (`KK`), điều chỉnh (`DC`), hủy (`HY`) vẫn chỉ lưu dữ liệu, không in.

- **Trạng thái được in:** `Approved`, `InTransit`, `Received`, `ReceivedWithDiscrepancy`, `Returned`. **Không in** `Requested`, `Rejected`, `Cancelled`. Kiểm ở cả UI (`CanPrint`) và handler.
- **Ai in:** Admin, Giám đốc, Kế toán in phiếu bất kỳ (ở trạng thái được in). Thủ kho và Trưởng Showroom/Kho **chỉ in khi là nhân viên của kho xuất** của phiếu; nhân viên kho nhận vẫn xem được phiếu nhưng không in. Các vai khác không in. Quyền xem phiếu không đổi (liên quan kho xuất hoặc nơi nhận).
- **Số lượng:** in số lượng trên phiếu (`Quantity`). **Không thêm "số lượng thực xuất"**; vẫn xuất đúng số trên phiếu.
- **Nội dung bản in (mẫu đơn giản, chưa có mẫu chuẩn của công ty):** một bản khổ A4; tiêu đề phiếu xuất kho (chuyển kho nội bộ), mã phiếu, ngày, kho xuất, nơi nhận, ghi chú, người vận chuyển (`Carrier`, ô ghi chú tự do, giữ nguyên), số seal nếu có, bảng hàng (STT, mã, tên, đơn vị, số lượng, ghi chú, cột "Thực nhận" để trống cho người nhận ghi tay), khối chữ ký: Người lập, Thủ kho xuất, Người vận chuyển, Người nhận. **Không in giá vốn** (và không đưa giá vốn vào dữ liệu của trang in).
- **Không có:** mã vạch/QR, chữ ký số, ghi nhận số lần in hay người in.
- **Mã phiếu tuần tự:** thêm số thứ tự duy nhất cho phiếu chuyển lấy từ sequence PostgreSQL, có unique index; mã hiển thị dạng `CK-000123` (số có đệm 0). Sequence có thể bỏ số nếu một giao dịch thất bại nhưng không bao giờ trùng. Migration gán số cho phiếu cũ theo thứ tự thời gian tạo, nên **mã hiển thị của phiếu chuyển cũ đổi**; thông báo cũ đã ghi mã cũ sẽ không còn khớp. Không xóa dữ liệu.
- **Cách làm (đã làm):** trang Razor `/inventory/transfers/{id}/print` (`TransferPrint`, `GetTransferPrintHandler`, CSS `print.css`, in bằng nút "In phiếu"; nút "In phiếu xuất kho" ở chi tiết phiếu); trang Razor in được (CSS `@media print`, người dùng dùng "Lưu thành PDF" của trình duyệt nếu cần), không thêm thư viện PDF.

## 4. Kiểm kê, điều chỉnh, hủy

| Chứng từ | Nội dung | Trạng thái (`ApprovalStatus`) | Tác động tồn |
| --- | --- | --- | --- |
| Kiểm kê (`StocktakeSheet`) | Số sổ sách, số đếm; `Variance = đếm − sổ sách` | `Pending` → `Approved` (tài chính lập thì `Approved` ngay) | Khi duyệt: áp chênh lệch lên `OnHand` (cho phép khả dụng âm, không âm tồn thực) |
| Điều chỉnh (`QuantityAdjustment`) | `Delta` ≠ 0 | `Pending` → `Approved`; hoặc `Recorded` (tự sinh khi nhận lệch) | Khi duyệt: cộng/trừ `OnHand`, không xuống dưới `Reserved` |
| Hủy (`DisposalSlip`) | Một mặt hàng tại một kho | `Pending` → `Approved` | Khi duyệt: **trừ đúng số lượng trên phiếu** (từ 1, không vượt tồn khả dụng) |

- Chênh lệch kiểm kê và mọi điều chỉnh/hủy **bắt buộc lý do và chứng từ** (số chứng từ hoặc ảnh).
- Tài chính ghi thẳng (không qua duyệt). Thủ kho và Trưởng kho lập phiếu chờ duyệt; người duyệt xem [roles-and-permissions.md](../roles-and-permissions.md) mục 3.
- Duyệt hủy ghi thông báo cho nhân viên kho; lập điều chỉnh chờ duyệt báo cho Admin, Giám đốc, Kế toán (chi tiết ở [notifications-and-audit.md](notifications-and-audit.md)).

## 5. Tra cứu tồn

- Mỗi dòng hiện bốn số: tồn thực, đang giữ, khả dụng, đang chuyển, và tồn từng địa điểm. Khả dụng không hiện số âm (hiện 0).
- Lọc theo địa điểm, ngành hàng, tìm theo mã/tên/thương hiệu (live filter). Xuất CSV đúng theo bộ lọc hiện tại.
- Giá vốn chỉ vai tài chính thấy; handler loại khỏi kết quả khi thiếu quyền ([role-based-ui.md](../../03-ui-ux/role-based-ui.md)).

## 6. Kho & Showroom (địa điểm, `/locations`)

- **Xem:** Admin, Giám đốc, Kế toán. **Quản lý (thêm, sửa, Disable):** Admin, Giám đốc.
- Địa điểm có **tên duy nhất**, loại Kho hoặc Showroom (`StockLocationKind`), tên tối đa 200 ký tự.
- **Disable** (thay cho xóa, giữ lịch sử) **chỉ được khi**: không còn tồn (`HasStock`), không còn nhân viên đang hoạt động gán vào (`HasActiveEmployees`), và không còn đơn mua (nháp, chờ duyệt, đã duyệt, đang về, nhận một phần) nhận về kho này hoặc phiếu chuyển đang mở có kho này (`HasOpenDocuments`).
- Phiếu chuyển chặn kho xuất hoặc nơi nhận không tồn tại hoặc đã Disable.
- Hiển thị dạng bảng, phân trang chuẩn 20/50 ([components.md](../../03-ui-ux/components.md)); thêm/sửa mở modal.

## Nợ lệch so với luật

| Vấn đề | Hiện trạng |
| --- | --- |
| `StockMovement` đủ chứng từ | Cần rà từng chứng từ (kiểm kê, điều chỉnh, hủy, nhận lệch, trả hàng) đều ghi dòng `StockMovement` đúng loại; hiện chỉ có 4 loại `Receipt/Issue/Transfer/Adjustment` |

## Quyết định đã chốt (2026-11-16)

- Giữ chỗ cho nguồn khác (ví dụ đơn bán khi có module Bán hàng) dùng chung cột `Reserved` với phiếu chuyển; khả dụng = tồn − giữ chỗ.
- Chưa cần duyệt theo ngưỡng; người duyệt thay thế khi Trưởng kho vắng vẫn là tài chính.
- Chưa cần thông tin vận chuyển có cấu trúc (người vận chuyển, số seal): giữ ô ghi chú tự do `Carrier` và `SealNumber`. ~~Chưa cần phiếu giao in được~~: **đổi 2026-11-18** theo UAT, chỉ cho phiếu chuyển kho (xem mục "Phiếu chuyển kho in được").

## Chờ chốt (chưa có hiệu lực)

Không còn mục chờ chốt.

