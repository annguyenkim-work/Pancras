// Toast: nút × đóng; toast thành công tự tắt theo data-autoclose (ms), cảnh báo/lỗi phải bấm × mới tắt.
(function () {
    function dismiss(toast) {
        if (toast && toast.parentNode) {
            toast.parentNode.removeChild(toast);
        }
    }

    document.querySelectorAll('#toast-region .toast').forEach(function (toast) {
        var ms = parseInt(toast.getAttribute('data-autoclose') || '0', 10);
        if (ms > 0) {
            window.setTimeout(function () { dismiss(toast); }, ms);
        }
    });

    document.addEventListener('click', function (event) {
        var button = event.target instanceof Element ? event.target.closest('.toast-close') : null;
        if (button) {
            dismiss(button.closest('.toast'));
        }
    });
})();
