// Nút có data-print mở hộp thoại in của trình duyệt (không dùng onclick nội tuyến vì CSP không cho 'unsafe-inline').
(function () {
    document.addEventListener('click', function (event) {
        if (event.target.closest('[data-print]')) {
            window.print();
        }
    });
})();
