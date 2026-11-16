// Chống bấm đúp khi gửi form POST: sau khi form được gửi, vô hiệu các nút submit và đổi nhãn thành "Đang xử lý…".
// Mở lại khi trang hiện lại từ cache (nút Quay lại) hoặc sau 15 giây (form trả về tệp tải xuống không rời trang).
// Bỏ qua: form GET, form có target, form có thuộc tính data-no-guard.
// Server vẫn phải an toàn khi lệnh bị gửi lặp (xem docs/02-coding-rules/use-cases-and-results.md).
(function () {
    var BUSY_TEXT = 'Đang xử lý…';
    var RELEASE_AFTER_MS = 15000;

    function submitButtons(form) {
        return form.querySelectorAll('button[type="submit"], button:not([type]), input[type="submit"]');
    }

    function lock(form) {
        form.dataset.guard = '1';
        submitButtons(form).forEach(function (button) {
            if (button.disabled) {
                return;
            }

            button.dataset.guardLocked = '1';
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
            if (button.tagName === 'BUTTON') {
                button.dataset.guardLabel = button.innerHTML;
                button.textContent = BUSY_TEXT;
            }
        });
        window.setTimeout(function () { unlock(form); }, RELEASE_AFTER_MS);
    }

    function unlock(form) {
        delete form.dataset.guard;
        submitButtons(form).forEach(function (button) {
            if (button.dataset.guardLocked !== '1') {
                return;
            }

            delete button.dataset.guardLocked;
            button.disabled = false;
            button.removeAttribute('aria-busy');
            if (button.dataset.guardLabel !== undefined) {
                button.innerHTML = button.dataset.guardLabel;
                delete button.dataset.guardLabel;
            }
        });
    }

    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement)
            || form.method.toLowerCase() !== 'post'
            || form.target
            || form.hasAttribute('data-no-guard')) {
            return;
        }

        if (form.dataset.guard === '1') {
            event.preventDefault();
            return;
        }

        // Chờ mọi trình xử lý khác (ví dụ hộp xác nhận chung) chạy xong; nếu form bị chặn thì không khóa.
        // Khóa nút sau khi trình duyệt đã dựng dữ liệu form để không mất giá trị của nút bấm (name/value).
        window.setTimeout(function () {
            if (!event.defaultPrevented) {
                lock(form);
            }
        }, 0);
    });

    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            document.querySelectorAll('form[data-guard]').forEach(unlock);
        }
    });
})();
