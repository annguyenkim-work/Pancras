    (function () {
        const dialog = document.getElementById("confirm-dialog");
        const title = document.getElementById("confirm-title");
        const message = document.getElementById("confirm-message");
        const reasonBox = document.getElementById("confirm-reason-box");
        const reason = document.getElementById("confirm-reason");
        const reasonError = document.getElementById("confirm-reason-error");
        const ok = document.getElementById("confirm-ok");
        let pending = null;

        function closeDialog() {
            pending = null;
            reason.value = "";
            reasonError.classList.add("hidden");
            if (dialog.open) {
                dialog.close();
            }
        }

        window.pancrasConfirm = function (options) {
            pending = options;
            title.textContent = options.title || "Xác nhận";
            message.textContent = options.message || "";
            ok.textContent = options.label || "Xác nhận";
            ok.className = "rounded-lg px-4 py-2 text-sm font-bold text-white " + (options.tone === "danger" ? "bg-rose-600" : "bg-sky-600");
            reasonBox.classList.toggle("hidden", options.reason !== "required");
            reason.value = "";
            reasonError.classList.add("hidden");
            dialog.showModal();
            if (options.reason === "required") {
                reason.focus();
            }
        };

        document.addEventListener("submit", (event) => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-confirm")) {
                return;
            }

            if (form.dataset.confirmed === "1") {
                if (form.dataset.sent === "1") {
                    event.preventDefault();
                } else {
                    form.dataset.sent = "1";
                }

                return;
            }

            event.preventDefault();
            const receiveForm = document.getElementById("receive-form");
            if (form === receiveForm) {
                const close = receiveForm.querySelector('input[name="closeOrder"]');
                const short = [...document.querySelectorAll("input[data-remaining]")].some((input) => Number(input.value) < Number(input.dataset.remaining));
                if (close instanceof HTMLInputElement && close.checked && short) {
                    receiveForm.dataset.reason = "required";
                } else {
                    delete receiveForm.dataset.reason;
                }
            }
            const submitter = event.submitter;
            const read = (name) => (submitter && submitter.dataset[name]) || form.dataset[name] || "";
            window.pancrasConfirm({
                title: read("confirmTitle"),
                message: read("confirmMessage"),
                label: read("confirmLabel"),
                tone: read("confirmTone"),
                reason: read("reason"),
                onOk: (text) => {
                    if (read("reason") === "required") {
                        const note = form.querySelector('input[name="note"]');
                        if (note) {
                            note.value = text;
                        }
                    }

                    form.dataset.confirmed = "1";
                    form.requestSubmit();
                }
            });
        }, true);

        ok.addEventListener("click", () => {
            if (!pending) {
                return;
            }

            const current = pending;
            const text = reason.value.trim();
            if (current.reason === "required" && !text) {
                reasonError.classList.remove("hidden");
                reason.focus();
                return;
            }

            closeDialog();
            current.onOk(text);
        });
        reason.addEventListener("input", () => reasonError.classList.add("hidden"));
        document.getElementById("confirm-cancel").addEventListener("click", closeDialog);
        document.getElementById("confirm-close").addEventListener("click", closeDialog);
        dialog.addEventListener("cancel", () => {
            pending = null;
            reason.value = "";
        });
    })();
