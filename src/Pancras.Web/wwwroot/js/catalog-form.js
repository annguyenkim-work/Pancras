    document.querySelectorAll("[data-vnd]").forEach(function (input) {
        input.addEventListener("focus", function () {
            input.value = input.value.replace(/VNĐ/gi, "").replace(/đ/gi, "").replace(/\./g, "").replace(/\s/g, "").trim();
        });
        input.addEventListener("blur", function () {
            var text = input.value.trim();
            if (text === "") {
                return;
            }
            var normalized = text.replace(/\./g, "").replace(",", ".");
            var number = Number(normalized);
            if (!Number.isFinite(number)) {
                return;
            }
            var whole = Math.max(0, Math.round(number));
            input.value = new Intl.NumberFormat("vi-VN").format(whole) + " VNĐ";
        });
    });
