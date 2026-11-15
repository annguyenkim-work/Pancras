# Kế hoạch fix – run R01 (01 Tài khoản & 02 Khung giao diện)

Nguồn: [01-02.md](01-02.md), mục "Lỗi tìm được" và "Ghi nhận khác".
Trạng thái: đã sửa trong code theo các quyết định bên dưới.

## Quyết định

### 1. LOGIN-11 – Đăng nhập không quay lại URL ban đầu

- **Quyết định:** Hướng A. Giữ hành vi đăng nhập luôn về `/`, không hỗ trợ `ReturnUrl`.
- **Lý do:** Không cần thay đổi luồng đăng nhập và 2FA. Không mở thêm bề mặt cho open redirect.
- **Việc cần làm khi fix:**
  - Không sửa `Login.cshtml.cs`. Code hiện tại đã đúng với quyết định này.
  - Sửa kỳ vọng của LOGIN-11 trong kịch bản test: sau khi đăng nhập đúng, kỳ vọng là `302 /`, không phải `302 /inventory?view=transfers`.
  - Ghi chú trong kết quả: "Thiết kế: đăng nhập luôn về `/`, không quay lại URL ban đầu."
  - Cập nhật trạng thái LOGIN-11 từ FAIL sang PASS theo kỳ vọng mới (cần người dùng xác nhận khi review).
- **Ảnh hưởng:** LOGIN-12 vẫn PASS vì `ReturnUrl` tiếp tục bị bỏ qua.

### 2. 2FA-L09 – "Quay lại đăng nhập" không hủy phiên chờ 2FA

- **Quyết định:** Hướng A. Đổi link "Quay lại đăng nhập" thành form POST có antiforgery tới handler mới `OnPostCancelAsync`.
- **Việc cần làm khi fix:**
  - Trong `VerifyTwoFactor.cshtml.cs`, handler gọi `HttpContext.SignOutAsync(PendingTwoFactor.Scheme)` rồi `RedirectToPage("/Account/Login")`.
  - Trong `VerifyTwoFactor.cshtml` (dòng 19), thay `<a href="/login">` bằng form POST có nút styled như link cũ.
  - Test: sau khi bấm hủy, `/login/verify` trả 302 về `/login`.
- **Ảnh hưởng:** Không đổi luồng đăng nhập đúng hay sai mã.

### 3. SOON-02 – Trang 404 trống

- **Quyết định:** Thêm trang 404 tiếng Việt, dùng cùng kiểu HTML tự chứa với trang 500 đang có trong `Program.cs`. Không dùng layout có sidebar.
- **Việc cần làm khi fix:**
  - Thêm `app.UseStatusCodePages` chỉ xử lý status 404, đặt sau `UseExceptionHandler` và trước `UseStaticFiles`.
  - Nội dung: tiêu đề "Không tìm thấy trang", thông điệp ngắn tiếng Việt, nút "Về trang chủ".
  - Không đổi `UseExceptionHandler` (trang 500 giữ nguyên).
  - Test: GET `/abc` khi đã đăng nhập trả 404 và có HTML tiếng Việt. Kiểm tra 500 không đổi.

## Ghi nhận khác

### 4. PROF-13 – Thông báo sai ngữ cảnh khi mật khẩu quá dài

- **Quyết định:** Hướng B. Giữ một mã lỗi `WeakPassword`, đổi thông báo thành "Mật khẩu cần từ 8 đến 128 ký tự."
- **Việc cần làm khi fix:**
  - `Profile.cshtml` (dòng 11): đổi "Mật khẩu mới cần từ 8 ký tự trở lên." thành "Mật khẩu mới cần từ 8 đến 128 ký tự."
  - `Employees/Index.cshtml` (dòng 23): đổi "Mật khẩu cần từ 8 ký tự trở lên." thành "Mật khẩu cần từ 8 đến 128 ký tự."
  - Đổi nhãn "(từ 8 ký tự)" thành "(8–128 ký tự)" ở `Profile.cshtml` (dòng 57) và `Employees/Index.cshtml` (dòng 263, 374). Không đổi Domain hay enum.
  - Kiểm tra `CreateEmployeeCommandHandler` (dòng 66) có giới hạn tối đa 128 không. Nếu không có thì quyết định có thêm hay không trước khi fix.
  - Cập nhật kỳ vọng PROF-12 và PROF-13 trong kịch bản test.

### 5. Rate limit 20 request/phút/IP trên `/login` và `/login/verify`

- **Quyết định:** Hướng B + C.
- **Việc cần làm khi fix:**
  - C: thêm `OnRejected` vào `AddRateLimiter` (`Program.cs`, dòng 61–72) để trả trang 429 tiếng Việt, tự chứa HTML, cùng kiểu với trang 404 và 500. Nội dung gợi ý: "Bạn thử đăng nhập quá nhiều lần. Vui lòng đợi một phút rồi thử lại."
  - B: đặt `RateLimiting:LoginPerMinute` = **120** trong cấu hình. Căn cứ: 50 người đăng nhập đồng thời, mỗi lần 2 request (4 nếu có 2FA). Đánh đổi: một IP có thể thử khoảng 60 mật khẩu mỗi phút trên nhiều tài khoản. Khóa 5 lần sai theo tài khoản vẫn áp dụng.
- **Ghi chú:** Nếu sau này app đặt sau reverse proxy, cần cấu hình forwarded headers để không gom mọi người dùng vào một bucket.

### 6. SESS-02 – Cookie không thu hồi phía server khi đăng xuất

- **Quyết định:** Hướng A + D.
  - A: giữ nguyên cơ chế đăng xuất. Không thu hồi theo phiên, không đổi `SecurityStamp` khi đăng xuất.
  - D: thêm giới hạn tuổi tuyệt đối của phiên, bất kể sliding expiry.
- **Việc cần làm khi fix:**
  - Trong `EmployeePrincipalValidator` (hoặc `OnValidatePrincipal`, `Program.cs` dòng 39–43), kiểm tra `IssuedUtc` của cookie. Nếu quá 12 giờ kể từ lúc đăng nhập thì đăng xuất và buộc đăng nhập lại.
  - Con số 12 giờ đã được xác nhận.
  - Cập nhật ghi chú SESS-02 trong kết quả: "Thiết kế: đăng xuất chỉ xóa cookie trên trình duyệt hiện tại. Bản sao cookie có giới hạn tuổi tuyệt đối 12 giờ."

## Các mục còn lại (chưa thảo luận)

- Lỗi script tự gây 429 khi test (Ghi nhận, không phải lỗi app, không cần fix)
