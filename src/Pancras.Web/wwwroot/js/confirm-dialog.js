    (function () {
        const dialog = document.getElementById("confirm-dialog");
        const title = document.getElementById("confirm-title");
        const message = document.getElementById("confirm-message");
        const ok = document.getElementById("confirm-ok");
        let pending = null;

        function closeDialog() {
            pending = null;
            if (dialog.open) {
                dialog.close();
            }
        }

        document.addEventListener("submit", (event) => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-confirm")) {
                return;
            }

            if (form.dataset.confirmed === "1") {
                return;
            }

            const submitter = event.submitter;
            const read = (name) => (submitter && submitter.dataset[name]) || form.dataset[name] || "";
            if (!read("confirmMessage")) {
                // Không có nội dung xác nhận (ví dụ nút "Kích hoạt lại"): gửi thẳng.
                return;
            }

            event.preventDefault();
            pending = () => {
                form.dataset.confirmed = "1";
                form.requestSubmit(submitter instanceof HTMLElement ? submitter : undefined);
            };
            title.textContent = read("confirmTitle") || "Xác nhận";
            message.textContent = read("confirmMessage");
            ok.textContent = read("confirmLabel") || "Xác nhận";
            ok.className = "rounded-lg px-4 py-2 text-sm font-bold text-white " + (read("confirmTone") === "danger" ? "bg-rose-600" : "bg-sky-600");
            dialog.showModal();
        });

        ok.addEventListener("click", () => {
            const run = pending;
            closeDialog();
            if (run) {
                run();
            }
        });
        document.getElementById("confirm-cancel").addEventListener("click", closeDialog);
        document.getElementById("confirm-close").addEventListener("click", closeDialog);
        dialog.addEventListener("close", () => { pending = null; });
    })();
