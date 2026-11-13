// Lọc danh sách theo thời gian thực: gõ vào ô tìm kiếm hoặc đổi ô chọn thì danh sách tự cập nhật, không cần nút Lọc.
// Dùng: <form method="get" data-live-filter> ... </form> và bọc vùng kết quả bằng <div id="live-results">.
(function () {
    var forms = document.querySelectorAll('form[data-live-filter]');
    forms.forEach(function (form) {
        var timer = null;
        var controller = null;

        function targetUrl() {
            var params = new URLSearchParams();
            new FormData(form).forEach(function (value, key) {
                if (typeof key === 'string' && key.indexOf('__') !== 0 && typeof value === 'string' && value.trim() !== '') {
                    params.append(key, value.trim());
                }
            });
            var base = (form.getAttribute('action') || window.location.pathname).split('?')[0];
            var query = params.toString();
            return query ? base + '?' + query : base;
        }

        function run() {
            var url = targetUrl();
            if (controller) {
                controller.abort();
            }

            controller = new AbortController();
            var current = document.getElementById('live-results');
            if (current) {
                current.setAttribute('aria-busy', 'true');
                current.classList.add('opacity-60');
            }

            fetch(url, { headers: { 'X-Live-Filter': '1' }, credentials: 'same-origin', signal: controller.signal })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('status ' + response.status);
                    }

                    return response.text();
                })
                .then(function (html) {
                    var doc = new DOMParser().parseFromString(html, 'text/html');
                    var next = doc.getElementById('live-results');
                    var target = document.getElementById('live-results');
                    if (next && target) {
                        target.replaceWith(next);
                        window.history.replaceState(null, '', url);
                    } else {
                        window.location.href = url;
                    }
                })
                .catch(function (error) {
                    if (error && error.name === 'AbortError') {
                        return;
                    }

                    var target = document.getElementById('live-results');
                    if (target) {
                        target.classList.remove('opacity-60');
                        target.removeAttribute('aria-busy');
                    }
                });
        }

        form.addEventListener('input', function (event) {
            var element = event.target;
            if (element && element.matches && element.matches('input:not([type=hidden]):not([type=checkbox]):not([type=radio])')) {
                window.clearTimeout(timer);
                timer = window.setTimeout(run, 300);
            }
        });

        form.addEventListener('change', function (event) {
            var element = event.target;
            if (element && element.matches && element.matches('select, input[type=checkbox], input[type=radio]')) {
                window.clearTimeout(timer);
                run();
            }
        });

        form.addEventListener('submit', function (event) {
            if (event.submitter && event.submitter.getAttribute('formaction')) {
                return;
            }

            event.preventDefault();
            window.clearTimeout(timer);
            run();
        });
    });
})();
