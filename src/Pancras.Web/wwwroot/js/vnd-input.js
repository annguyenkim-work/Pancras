// Ô nhập tiền VNĐ dùng chung cho mọi trang (docs/03-ui-ux/formatting-and-copy.md mục 1): mọi ô có thuộc tính data-vnd
// hiện số nhóm kiểu 200.000 khi mở trang và ngay khi gõ; dữ liệu gửi lên máy chủ là số thô (200000), ô trên màn hình giữ nguyên.
// Chỉ cho số đồng không âm. Script trang khác dùng window.PancrasVnd thay vì tự định dạng.
(function () {
    "use strict";

    const money = new Intl.NumberFormat("vi-VN");
    const digitsOf = (text) => String(text ?? "").replace(/\D/g, "");

    /** Số đồng của một ô data-vnd (ô trống là 0). */
    function parse(input) {
        const digits = digitsOf(input.value);
        return digits === "" ? 0 : Number(digits);
    }

    /** Định dạng lại một ô, giữ con trỏ đứng sau đúng chữ số người dùng vừa gõ. */
    function format(input) {
        const digits = digitsOf(input.value);
        const formatted = digits === "" ? "" : money.format(Number(digits));
        if (input.value === formatted) {
            return;
        }

        const focused = document.activeElement === input;
        const caret = focused ? input.selectionStart ?? input.value.length : 0;
        const digitsBeforeCaret = digitsOf(input.value.slice(0, caret)).length;
        input.value = formatted;
        if (!focused) {
            return;
        }

        let position = 0;
        let seen = 0;
        while (position < formatted.length && seen < digitsBeforeCaret) {
            if (/\d/.test(formatted[position])) {
                seen++;
            }

            position++;
        }

        input.setSelectionRange(position, position);
    }

    function formatAll(root) {
        (root ?? document).querySelectorAll("[data-vnd]").forEach(format);
    }

    window.PancrasVnd = { parse, format, formatAll };

    document.addEventListener("input", (event) => {
        if (event.target instanceof HTMLInputElement && event.target.hasAttribute("data-vnd")) {
            format(event.target);
        }
    });

    // Gửi số thô mà không đổi ô trên màn hình (form bị hộp xác nhận chặn lại vẫn hiện số nhóm).
    document.addEventListener("formdata", (event) => {
        event.target.querySelectorAll("[data-vnd][name]").forEach((input) => {
            if (!input.disabled && event.formData.has(input.name)) {
                event.formData.set(input.name, digitsOf(input.value));
            }
        });
    }, true);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => formatAll());
    } else {
        formatAll();
    }
})();
