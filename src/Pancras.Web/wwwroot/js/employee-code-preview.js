        (function () {
            const name = document.getElementById("employee-name");
            const code = document.getElementById("employee-code");
            if (!name || !code) return;
            const preview = (value) => {
                const words = value.trim().split(/\s+/).filter(Boolean).map((word) => word.normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/đ/gi, "d").replace(/[^A-Za-z0-9]/g, ""));
                if (words.length === 0) return "";
                const parts = words.map((word) => word[0].toUpperCase() + word.slice(1).toLowerCase());
                if (parts.length === 1) return parts[0];
                return parts[parts.length - 1] + parts.slice(0, -1).map((part) => part[0].toUpperCase()).join("");
            };
            name.addEventListener("input", () => { code.value = preview(name.value); });
        })();
