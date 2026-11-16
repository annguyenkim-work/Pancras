// Hành vi giao diện dùng chung qua thuộc tính data-*, để không cần JS inline (CSP không cho 'unsafe-inline').
//   data-open-dialog="id"    bấm để mở hộp thoại
//   data-close-dialog        bấm để đóng hộp thoại chứa nó
//   data-auto-submit         đổi giá trị thì gửi form
//   data-autoshow            hộp thoại tự mở khi tải trang; data-cancel-url: Esc thì chuyển tới địa chỉ đó
//   data-nav-toggle / data-nav-close  mở/đóng menu trên điện thoại
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
