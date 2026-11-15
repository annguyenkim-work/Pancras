# 03 – Nhân sự & Phân quyền (`/employees`)

Thành phần màn hình:
- Thanh lọc: ô tìm "Tìm theo tên, mã hoặc email...", lọc **Vai trò**, lọc **Kho/showroom**, phân trang.
- Bảng: Nhân viên · Vai trò · Kho/showroom · Liên hệ · Chức vụ/Phòng ban · Thao tác.
- Nút trên mỗi dòng: **Mở khóa** (khi tài khoản đang bị khóa), **Tắt 2FA** (khi NV đã bật 2FA), **Ngừng hoạt động / Kích hoạt**, **Sửa** (mở "Hồ sơ nhân viên").
- Hộp thoại **Thêm nhân viên**: Họ tên*, Mã nhân viên (tự sinh), Email*, Điện thoại*, Địa chỉ, Vai trò*, Kho/showroom, Chức vụ*, Phòng ban, Mật khẩu* (8–128 ký tự).
- Hộp thoại **Hồ sơ nhân viên**: khối thông tin cá nhân, khối phân công (vai trò, kho, chức vụ, phòng ban), ô "Đặt lại mật khẩu".

---

## 1. Chức năng

### 1.1 Danh sách, tìm, lọc
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| EMP-01 | P2 | Tìm theo tên có dấu / không dấu / một phần mã / email | Ra đúng nhân viên | ☐ |
| EMP-02 | P2 | Lọc Vai trò, lọc Kho, kết hợp cả hai + từ khóa | Kết quả là giao của các điều kiện | ☐ |
| EMP-03 | P2 | Lọc không ra kết quả | "Chưa có nhân viên khớp bộ lọc." + nút "Bỏ lọc" | ☐ |
| EMP-04 | P2 | Sang trang 2 rồi bấm Sửa / Ngừng hoạt động | Sau khi lưu vẫn ở trang 2 và giữ bộ lọc | ☐ |
| EMP-05 | P3 | NV đã ngừng hoạt động | Hiển thị phân biệt được (nhãn/màu), vẫn nằm trong danh sách | ☐ |
| EMP-06 | P1 | Danh sách và hồ sơ | Không hiển thị mật khẩu hay hash ở bất kỳ đâu (kể cả mã nguồn trang) | ☐ |

### 1.2 Thêm nhân viên
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| EMP-10 | P1 | Nhập đủ, họ tên "Nguyễn Kim An" → Lưu | Tạo thành công, mã tự sinh `AnNK` | ☐ |
| EMP-11 | P1 | Tạo thêm 1 người cùng họ tên | Mã có hậu tố số (ví dụ `AnNK2`) | ☐ |
| EMP-12 | P2 | Gõ họ tên → ô Mã nhân viên | Xem trước mã ngay khi gõ; ô mã không sửa tay được | ☐ |
| EMP-13 | P2 | Họ tên 1 từ ("An"), có dấu ("Đỗ Thị Ánh"), có số/ký tự lạ, toàn ký tự đặc biệt | Sinh mã hợp lý hoặc báo "tên không dùng được" – không lỗi 500 | ☐ |
| EMP-14 | P1 | Bỏ trống lần lượt Họ tên / Email / Điện thoại / Vai trò / Chức vụ / Mật khẩu | Báo thiếu, không tạo | ☐ |
| EMP-15 | P1 | Vai trò Trưởng kho / Thủ kho / Sales, **không chọn kho** | Báo bắt buộc chọn kho | ☐ |
| EMP-16 | P2 | Vai trò Admin / Giám đốc / Kế toán / CSKH, không chọn kho | Tạo được | ☐ |
| EMP-17 | P1 | Tạo **Trưởng kho thứ 2** cho kho đã có Trưởng kho | Báo kho đã có Trưởng | ☐ |
| EMP-18 | P1 | Mật khẩu 7 ký tự | Báo mật khẩu quá ngắn | ☐ |
| EMP-19 | P1 | Tạo xong → đăng nhập bằng tài khoản mới | Đăng nhập được, đúng vai trò và kho | ☐ |
| EMP-20 | P2 | Lưu lỗi | Hộp thoại mở lại, dữ liệu còn (trừ mật khẩu), lỗi hiện trong hộp thoại | ☐ |

### 1.3 Sửa hồ sơ nhân viên
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| EMP-30 | P1 | Admin sửa họ tên/email/điện thoại/địa chỉ của NV khác | Lưu được; mã NV không đổi | ☐ |
| EMP-31 | P1 | Admin đổi vai trò Thủ kho → Sales | Lưu được; NV đó ở request kế tiếp có quyền mới | ☐ |
| EMP-32 | P1 | Đổi vai trò sang Thủ kho mà bỏ trống kho | Báo bắt buộc chọn kho | ☐ |
| EMP-33 | P1 | Đổi NV thành Trưởng kho của kho đã có Trưởng | Bị chặn | ☐ |
| EMP-34 | P1 | Đổi kho của Thủ kho từ A sang B | NV chỉ còn thấy phiếu kho B | ☐ |
| EMP-35 | P1 | Ô "Đặt lại mật khẩu" để trống → Lưu | Mật khẩu giữ nguyên | ☐ |
| EMP-36 | P1 | Đặt lại mật khẩu ≥ 8 ký tự | NV đăng nhập bằng mật khẩu mới; mật khẩu cũ không dùng được | ☐ |
| EMP-37 | P1 | Đặt lại mật khẩu 7 ký tự | Báo quá ngắn | ☐ |
| EMP-38 | P2 | Sau khi Admin đặt lại mật khẩu, NV đăng nhập lần đầu | Theo tài liệu: phải đổi mật khẩu **[Cần xác nhận – ghi lại hành vi thực tế]** | ☐ |

### 1.4 Ngừng hoạt động / Kích hoạt / Mở khóa / Tắt 2FA
| ID | P | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|
| EMP-40 | P1 | Ngừng hoạt động 1 NV | NV không đăng nhập được; phiên đang mở bị đăng xuất | ☐ |
| EMP-41 | P1 | Kích hoạt lại | Đăng nhập lại được | ☐ |
| EMP-42 | P1 | Ngừng hoạt động **Admin cuối cùng** | Bị chặn | ☐ |
| EMP-43 | P1 | Đổi vai trò **Admin cuối cùng** sang vai trò khác | Bị chặn | ☐ |
| EMP-44 | P1 | Làm NV bị khóa (sai mật khẩu 5 lần) → Admin bấm "Mở khóa" | NV đăng nhập được ngay | ☐ |
| EMP-45 | P2 | Nút "Mở khóa" | Chỉ hiện ở dòng NV đang bị khóa | ☐ |
| EMP-46 | P1 | NV đã bật 2FA → Admin bấm "Tắt 2FA" | NV đăng nhập không cần mã | ☐ |
| EMP-47 | P2 | Nút "Tắt 2FA" | Chỉ hiện ở dòng NV đã bật 2FA | ☐ |
| EMP-48 | P1 | Không có nút "Xóa nhân viên" ở bất kỳ đâu | Đúng (chỉ ngừng hoạt động) | ☐ |

---

## 2. Phân quyền

### 2.1 Ma trận
| Hành động | AD | GĐ | KT | TK | ThK / SA / CS |
|---|---|---|---|---|---|
| Vào `/employees` | ✔ | ✔ | ✔ | ✔ | ✘ |
| Thấy nhân viên | Tất cả | Tất cả | Tất cả | Chỉ NV kho mình | – |
| Nút "Thêm nhân viên" | ✔ | ✔ | ✘ | ✔ | – |
| Vai trò được chọn khi thêm | Tất cả 7 | 6 (không có Admin) | – | Chỉ Thủ kho, Sales | – |
| Kho được chọn khi thêm | Tất cả | Tất cả | – | Chỉ kho mình | – |
| Sửa thông tin cá nhân người khác | ✔ | ✘ | ✘ | ✘ | – |
| Sửa vai trò/chức vụ/phòng ban/kho | ✔ | ✔ (trừ tài khoản Admin; không gán vai trò Admin) | ✘ | ✘ | – |
| Đặt lại mật khẩu, Mở khóa, Tắt 2FA | ✔ | ✘ | ✘ | ✘ | – |
| Ngừng hoạt động / Kích hoạt | ✔ | ✔ (trừ tài khoản Admin) | ✘ | ✘ | – |
| Tự sửa vai trò/kho/trạng thái của **chính mình** | ✘ | ✘ | ✘ | ✘ | – |

### 2.2 Test case phân quyền
Mỗi case kiểm cả hai lớp: (a) nút/ô **không hiển thị**, (b) **ép request** (dùng DevTools sửa form hoặc gửi lại request của vai trò khác) vẫn bị từ chối.

| ID | P | Vai trò | Bước | Kỳ vọng | KQ |
|---|---|---|---|---|---|
| EMP-P01 | P1 | ThK, SA, CS | Gõ `/employees` | Từ chối truy cập | ☐ |
| EMP-P02 | P1 | TK kho A | Mở danh sách | Chỉ thấy NV kho A (không thấy Admin, GĐ, KT, NV kho B); ô lọc kho không cho xem kho khác | ☐ |
| EMP-P03 | P1 | TK kho A | Sửa URL `?location=<id kho B>` | Không ra NV kho B | ☐ |
| EMP-P04 | P1 | TK kho A | Thêm NV: danh sách vai trò | Chỉ có Thủ kho, Sales | ☐ |
| EMP-P05 | P1 | TK kho A | Ép request tạo NV vai trò Kế toán / Admin / Trưởng kho | Bị từ chối, không tạo | ☐ |
| EMP-P06 | P1 | TK kho A | Ép request tạo Thủ kho vào **kho B** | Bị từ chối | ☐ |
| EMP-P07 | P1 | TK kho A | Bấm Sửa NV kho mình | Chỉ xem; không có ô sửa, không có nút Lưu; có câu giải thích không có quyền | ☐ |
| EMP-P08 | P1 | TK kho A | Ép request Update / SetActive / Unlock / ResetTwoFactor cho NV kho mình | Bị từ chối | ☐ |
| EMP-P09 | P1 | KT | Mở danh sách | Thấy tất cả; không có nút Thêm; không có nút Sửa/Ngừng/Mở khóa/Tắt 2FA có hiệu lực | ☐ |
| EMP-P10 | P1 | KT | Ép request Create / Update / SetActive | Bị từ chối | ☐ |
| EMP-P11 | P1 | GĐ | Thêm NV: danh sách vai trò | Không có "Admin" | ☐ |
| EMP-P12 | P1 | GĐ | Ép request tạo NV vai trò Admin | Bị từ chối | ☐ |
| EMP-P13 | P1 | GĐ | Mở hồ sơ NV thường | Khối thông tin cá nhân **chỉ đọc**; khối phân công sửa được; **không** có ô đặt lại mật khẩu | ☐ |
| EMP-P14 | P1 | GĐ | Ép request sửa họ tên/email NV khác | Thông tin cá nhân không đổi | ☐ |
| EMP-P15 | P1 | GĐ | Ép request đặt lại mật khẩu NV khác | Mật khẩu không đổi | ☐ |
| EMP-P16 | P1 | GĐ | Mở hồ sơ tài khoản **Admin** | Không sửa được gì; không có nút Ngừng hoạt động | ☐ |
| EMP-P17 | P1 | GĐ | Ép request đổi vai trò tài khoản Admin / ngừng hoạt động Admin | Bị từ chối | ☐ |
| EMP-P18 | P1 | GĐ | Nâng NV thường lên **Admin** (ép request) | Bị từ chối | ☐ |
| EMP-P19 | P1 | GĐ | Ngừng hoạt động NV thường | Được **[tài liệu ghi "Cần xác nhận"]** | ☐ |
| EMP-P20 | P1 | GĐ | Bấm "Mở khóa" / "Tắt 2FA" | Không có nút; ép request bị từ chối | ☐ |
| EMP-P21 | P1 | AD | Mở hồ sơ **chính mình** từ `/employees` | Sửa được thông tin cá nhân; **không** sửa được vai trò/kho của mình; không có nút tự ngừng hoạt động | ☐ |
| EMP-P22 | P1 | AD, GĐ | Ép request đổi vai trò / ngừng hoạt động **chính mình** | Bị từ chối | ☐ |
| EMP-P23 | P1 | AD | Hệ thống có 2 Admin: ngừng hoạt động 1 Admin, rồi thử ngừng Admin còn lại | Lần 1 được, lần 2 bị chặn | ☐ |
| EMP-P24 | P1 | AD (2 tab) | Có 2 Admin A1, A2. Tab 1 (A1) ngừng A2; đồng thời tab 2 (A2) ngừng A1 | Không bao giờ còn 0 Admin hoạt động | ☐ |
| EMP-P25 | P1 | TK kho A | Gõ id NV kho B vào URL sửa (`?edit=<id>`) | Không mở được hồ sơ | ☐ |

---

## 3. Workflow: vòng đời tài khoản

```
Tạo  →  Hoạt động  ⇄  Bị khóa tạm (sai 5 lần, 15 phút / Admin mở khóa)
              ⇅
        Ngừng hoạt động (Admin, GĐ)  →  Kích hoạt lại
```

| ID | P | Kịch bản | Kỳ vọng | KQ |
|---|---|---|---|---|
| EMP-W01 | P1 | AD tạo Thủ kho kho A → NV đăng nhập → đổi mật khẩu → bật 2FA → đăng xuất → đăng nhập bằng mã 2FA | Trọn luồng không lỗi | ☐ |
| EMP-W02 | P1 | NV đang mở màn hình Kho (tab Chuyển kho, kho A). AD đổi kho của NV sang B. NV bấm "Xuất kho" một phiếu của kho A | Bị từ chối ("không có quyền thao tác với kho này") hoặc bị buộc đăng nhập lại; **không** xuất được | ☐ |
| EMP-W03 | P1 | NV đang mở hộp thoại tạo phiếu chuyển. AD ngừng hoạt động NV. NV bấm "Gửi Đề Nghị Chuyển Kho" | Bị đăng xuất, phiếu không được tạo | ☐ |
| EMP-W04 | P1 | AD hạ GĐ xuống Sales trong lúc GĐ đang mở `/audit` | GĐ tải lại: bị từ chối | ☐ |
| EMP-W05 | P2 | NV mất điện thoại: AD "Tắt 2FA" → NV đăng nhập → tự bật lại 2FA | Bật lại được, khóa bí mật mới; mã cũ không còn dùng được | ☐ |
| EMP-W06 | P2 | Ngừng hoạt động Trưởng kho A → tạo Trưởng kho A mới | Tạo được (kho không còn Trưởng đang hoạt động) **[xác nhận: ràng buộc 1 Trưởng/kho có tính NV đã ngừng không]** | ☐ |
| EMP-W07 | P1 | Mọi thao tác trên (tạo, đổi vai trò, đặt lại mật khẩu, ngừng, mở khóa, tắt 2FA) | Đều có dòng tương ứng ở `/audit`, không chứa mật khẩu | ☐ |
