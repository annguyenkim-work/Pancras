// Hành vi giao diện dùng chung qua thuộc tính data-*, để không cần JS inline (CSP không cho 'unsafe-inline').
//   data-open-dialog="id"    bấm để mở hộp thoại
//   data-close-dialog        bấm để đóng hộp thoại chứa nó
//   data-auto-submit         đổi giá trị thì gửi form
//   data-autoshow            hộp thoại tự mở khi tải trang; data-cancel-url: Esc thì chuyển tới địa chỉ đó
//   data-nav-toggle / data-nav-close  mở/đóng menu trên điện thoại
//   data-sidebar-toggle      thu gọn/mở sidebar trên desktop (nhớ trong localStorage)
(function () {
    document.addEventListener('click', function (event) {
        var opener = event.target.closest('[data-open-dialog]');
        if (opener) {
            var target = document.getElementById(opener.dataset.openDialog);
            if (target && !target.open) {
                target.showModal();
            }
            return;
        }

        var closer = event.target.closest('[data-close-dialog]');
        if (closer) {
            var dialog = closer.closest('dialog');
            if (dialog) {
                dialog.close();
            }
            return;
        }

        var sidebar = document.getElementById('app-sidebar');
        var backdrop = document.getElementById('nav-backdrop');
        if (event.target.closest('[data-nav-toggle]') && sidebar && backdrop) {
            sidebar.classList.toggle('-translate-x-full');
            backdrop.classList.toggle('hidden');
        } else if (event.target.closest('[data-nav-close]') && sidebar && backdrop) {
            sidebar.classList.add('-translate-x-full');
            backdrop.classList.add('hidden');
        }
    });

    // Thu gọn/mở sidebar trên desktop (data-sidebar-toggle); nhớ lựa chọn cục bộ, lỗi lưu trữ thì bỏ qua.
    var sidebarKey = 'pancras.sidebar.collapsed';

    function applySidebar(collapsed) {
        ['app-sidebar', 'app-content'].forEach(function (id) {
            var element = document.getElementById(id);
            if (!element) {
                return;
            }

            var expanded = element.dataset.expandedClass;
            var collapsedClass = element.dataset.collapsedClass;
            element.classList.toggle(expanded, !collapsed);
            element.classList.toggle(collapsedClass, collapsed);
        });
        document.querySelectorAll('[data-sidebar-toggle]').forEach(function (button) {
            button.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        });
    }

    function readSidebar() {
        try {
            return window.localStorage.getItem(sidebarKey) === '1';
        } catch (error) {
            return false;
        }
    }

    document.addEventListener('click', function (event) {
        if (!event.target.closest('[data-sidebar-toggle]')) {
            return;
        }

        var collapsed = !readSidebar();
        applySidebar(collapsed);
        try {
            window.localStorage.setItem(sidebarKey, collapsed ? '1' : '0');
        } catch (error) {
            // Không lưu được thì chỉ mất ghi nhớ, giao diện vẫn đổi.
        }
    });

    if (readSidebar()) {
        applySidebar(true);
    }

    document.addEventListener('change', function (event) {
        var field = event.target.closest('[data-auto-submit]');
        if (field && field.form) {
            field.form.submit();
        }
    });

    function showAuto() {
        document.querySelectorAll('dialog[data-autoshow]').forEach(function (dialog) {
            var cancelUrl = dialog.dataset.cancelUrl;
            if (cancelUrl) {
                dialog.addEventListener('cancel', function (event) {
                    event.preventDefault();
                    window.location.href = cancelUrl;
                });
            }

            if (!dialog.open) {
                dialog.showModal();
            }

            var invalid = dialog.querySelector('[aria-invalid="true"]');
            if (invalid) {
                invalid.focus();
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', showAuto);
    } else {
        showAuto();
    }
})();
