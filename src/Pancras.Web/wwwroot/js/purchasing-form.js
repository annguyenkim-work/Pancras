        (function () {
            document.getElementById("purchase-dialog")?.showModal();
            // Đóng bằng Esc không đổi địa chỉ: bỏ form, edit, focus để tải lại trang không mở lại hộp thoại,
            // nhưng giữ bộ lọc, trang, cỡ trang và đơn đang chọn (docs/03-ui-ux/components.md mục 10).
            document.getElementById("purchase-dialog")?.addEventListener("close", () => {
                const query = new URLSearchParams(location.search);
                if (location.pathname !== "/purchasing" || !(query.has("form") || query.has("edit"))) {
                    return;
                }

                const editing = query.get("edit");
                ["form", "edit", "focus"].forEach((name) => query.delete(name));
                if (editing && !query.has("order")) {
                    query.set("order", editing);
                }

                const rest = query.toString();
                history.replaceState(null, "", "/purchasing" + (rest ? "?" + rest : ""));
            });
            const lineBody = document.getElementById("line-body");
            const chargeBody = document.getElementById("charge-body");
            const money = new Intl.NumberFormat("vi-VN");
            const dong = (value) => money.format(Math.round(value)) + " VNĐ";
            const roundDong = (value) => Math.sign(value) * Math.round(Math.abs(value));
            const plainNumber = (input) => {
                if (input.hasAttribute("data-vnd")) {
                    return window.PancrasVnd.parse(input);
                }

                const value = Number(String(input.value).trim().replace(",", "."));
                return Number.isFinite(value) ? value : 0;
            };
            const text = (id, value) => { document.getElementById(id).textContent = value; };
            const lineTemplate = lineBody.querySelector(".line-row")?.cloneNode(true) ?? null;
            const chargeTemplate = document.getElementById("charge-template");

            // Mã nhóm và cách chia trùng với ChargeGroup / ChargeSplitMethod ở máy chủ.
            const GROUP_ADJUST = 1, GROUP_LOGISTICS = 2, GROUP_DEDUCTIBLE = 3, GROUP_NONDEDUCTIBLE = 4;
            const BY_FOB = 1, BY_QUANTITY = 2, BY_WEIGHT = 3, BY_VOLUME = 4, BY_MANUAL = 5;

            // Ô tiền định dạng bằng vnd-input.js dùng chung; ở đây chỉ định dạng lại các dòng vừa thêm hoặc vừa tính.
            function formatVndInputs() {
                window.PancrasVnd.formatAll();
            }

            function reindexLines() {
                [...lineBody.querySelectorAll(".line-row")].forEach((row, index) => {
                    row.querySelector("select").name = `Form.Lines[${index}].CatalogItemId`;
                    row.querySelector(".line-qty").name = `Form.Lines[${index}].Quantity`;
                    row.querySelector(".line-unit").name = `Form.UnitPrices[${index}]`;
                    row.querySelector(".line-duty").name = `Form.DutyPercents[${index}]`;
                    row.querySelector(".line-other").name = `Form.Lines[${index}].SupplementalDuty`;
                    row.querySelector(".line-id").name = `Form.Lines[${index}].LineId`;
                });
            }

            function reindexCharges() {
                [...chargeBody.querySelectorAll(".charge-row")].forEach((row, index) => {
                    row.querySelectorAll("[name]").forEach((element) => {
                        element.name = element.name.replace(/^Form\.Charges\[[^\]]*\]/, `Form.Charges[${index}]`);
                    });
                });
            }

            // Chia theo đúng cách của máy chủ (ChargeAllocation.TrySplit): cắt bỏ phần lẻ, phần dư đồng dồn về dòng cuối.
            function split(amount, weights) {
                if (weights.length === 0 || weights.some((weight) => !(weight > 0))) {
                    return null;
                }

                const total = weights.reduce((sum, weight) => sum + weight, 0);
                const whole = roundDong(amount);
                const result = [];
                let used = 0;
                weights.forEach((weight, index) => {
                    const share = index === weights.length - 1 ? whole - used : Math.trunc(whole * weight / total);
                    used += share;
                    result.push(share);
                });
                return result;
            }

            function measuresOf(select) {
                const option = select.selectedOptions[0];
                const read = (name) => {
                    const raw = option?.dataset[name];
                    return raw === undefined || raw === "" ? null : Number(raw);
                };
                return { code: option?.dataset.code || option?.textContent?.split("—")[0]?.trim() || "?", weight: read("weight"), volume: read("volume") };
            }

            function paint() {
                formatVndInputs();
                const rate = roundDong(plainNumber(document.getElementById("rate")));
                const vatRate = Math.round(plainNumber(document.getElementById("vat-percent")) * 100) / 10000;
                const deductible = document.getElementById("vat-deductible").checked;

                const lineRows = [...lineBody.querySelectorAll(".line-row")];
                lineRows.forEach((row) => {
                    const missingPrice = plainNumber(row.querySelector(".line-qty")) > 0 && plainNumber(row.querySelector(".line-unit")) <= 0;
                    row.querySelector(".line-unit").classList.toggle("border-rose-400", missingPrice);
                    row.querySelector(".line-unit").title = missingPrice ? "Đơn giá FOB phải lớn hơn 0" : "";
                });
                const drafts = lineRows.map((row) => {
                    const qty = plainNumber(row.querySelector(".line-qty"));
                    const usd = Math.round(plainNumber(row.querySelector(".line-unit")) * qty * 100) / 100;
                    const dutyRate = Math.round(plainNumber(row.querySelector(".line-duty")) * 100) / 10000;
                    const other = Math.max(0, roundDong(plainNumber(row.querySelector(".line-other"))));
                    const measure = measuresOf(row.querySelector("select"));
                    return { row, qty, usd, fob: roundDong(usd * rate), dutyRate, other, lineId: row.querySelector(".line-id").value, ...measure, adj: 0, log: 0, nonded: 0, ded: 0 };
                }).filter((line) => line.qty > 0 && line.usd > 0);

                // Chi phí chung chỉ nhập được sau khi có ít nhất một mặt hàng đủ số lượng và đơn giá.
                const hasLines = drafts.length > 0;
                const chargeArea = document.getElementById("charge-area");
                chargeArea.inert = !hasLines;
                chargeArea.classList.toggle("opacity-50", !hasLines);
                document.getElementById("charge-lock-note").hidden = hasLines;

                // Dòng mẫu còn ở chế độ tự động: chia theo thể tích khi mọi mặt hàng đã có thể tích, nếu không thì theo trị giá FOB.
                const allHaveVolume = hasLines && drafts.every((line) => line.volume > 0);
                chargeBody.querySelectorAll('.charge-row[data-auto="1"]').forEach((row) => {
                    row.querySelector(".charge-method").value = String(allHaveVolume ? BY_VOLUME : BY_FOB);
                    row.querySelector(".charge-auto").textContent = allHaveVolume
                        ? "Tự động: theo thể tích (mọi mặt hàng đã có thể tích). Chọn tay để cố định."
                        : "Tự động: theo trị giá FOB vì có mặt hàng chưa có thể tích. Chọn tay để đổi.";
                });

                // Chia từng khoản chi phí cho các mặt hàng.
                let chargeSum = 0;
                [...chargeBody.querySelectorAll(".charge-row")].forEach((row) => {
                    const warn = row.querySelector(".charge-warn");
                    warn.textContent = "";
                    const amount = roundDong(plainNumber(row.querySelector(".charge-amount")));
                    if (amount <= 0 || drafts.length === 0) {
                        return;
                    }

                    chargeSum += amount;
                    const group = Number(row.querySelector(".charge-group").value);
                    const method = Number(row.querySelector(".charge-method").value);
                    let shares = null;
                    if (method === BY_MANUAL) {
                        let saved = {};
                        try { saved = JSON.parse(row.dataset.shares || "{}"); } catch { saved = {}; }
                        shares = drafts.map((line) => Number(saved[line.lineId] ?? 0));
                    } else {
                        let weights;
                        let missing = [];
                        if (method === BY_FOB) {
                            weights = drafts.map((line) => line.usd);
                        } else if (method === BY_QUANTITY) {
                            weights = drafts.map((line) => line.qty);
                        } else {
                            const key = method === BY_WEIGHT ? "weight" : "volume";
                            missing = drafts.filter((line) => !(line[key] > 0)).map((line) => line.code);
                            weights = drafts.map((line) => (line[key] ?? 0) * line.qty);
                        }

                        if (missing.length > 0) {
                            warn.textContent = `Thiếu ${method === BY_WEIGHT ? "khối lượng" : "thể tích"} ở Danh mục: ${[...new Set(missing)].join(", ")}. Chọn cách chia khác hoặc bổ sung số đo.`;
                        } else {
                            shares = split(amount, weights);
                        }
                    }

                    if (shares === null) {
                        return;
                    }

                    shares.forEach((share, index) => {
                        const line = drafts[index];
                        if (group === GROUP_ADJUST) { line.adj += share; }
                        else if (group === GROUP_LOGISTICS) { line.log += share; }
                        else if (group === GROUP_NONDEDUCTIBLE) { line.nonded += share; }
                        else if (group === GROUP_DEDUCTIBLE) { line.ded += share; }
                    });
                });

                // Tính từng bước cho từng mặt hàng.
                const total = { qty: 0, fob: 0, adj: 0, customs: 0, duty: 0, other: 0, vatBase: 0, vat: 0, extra: 0, landed: 0, log: 0, nonded: 0, ded: 0 };
                let html = "";
                lineRows.forEach((row) => {
                    row.querySelector(".out-unit").textContent = "0 VNĐ";
                });
                drafts.forEach((line) => {
                    const customs = line.fob + line.adj;
                    const duty = customs * line.dutyRate;
                    const vatBase = customs + duty + line.other;
                    const vat = vatBase * vatRate;
                    const extra = line.log + line.nonded;
                    const landed = customs + duty + line.other + extra + (deductible ? 0 : vat);
                    Object.assign(line, { customs, duty, vatBase, vat, extra, landed });
                    total.qty += line.qty; total.fob += line.fob; total.adj += line.adj; total.customs += customs; total.duty += duty;
                    total.other += line.other; total.vatBase += vatBase; total.vat += vat; total.extra += extra; total.landed += landed;
                    total.log += line.log; total.nonded += line.nonded; total.ded += line.ded;
                    line.row.querySelector(".out-unit").textContent = dong(line.qty === 0 ? 0 : landed / line.qty);
                    const cell = (value, extraClass = "") => `<td class="py-1.5 pr-2 ${extraClass}">${value}</td>`;
                    html += `<tr class="border-b border-slate-100">${cell(line.code, "text-left font-mono")}${cell(line.qty)}${cell(dong(line.fob))}${cell(dong(line.adj))}${cell(dong(customs))}${cell(dong(duty))}${cell(dong(line.other))}${cell(dong(vatBase))}${cell(dong(vat))}${cell(dong(extra))}${cell(dong(landed), "font-bold")}${cell(dong(landed / line.qty), "font-bold text-emerald-700")}</tr>`;
                });
                document.getElementById("check-body").innerHTML = html === ""
                    ? `<tr><td colspan="12" class="py-3 text-center text-slate-500">Chọn mặt hàng, nhập số lượng, đơn giá FOB (lớn hơn 0) và tỷ giá để xem bảng tính.</td></tr>`
                    : html;
                const foot = (value, extraClass = "") => `<td class="py-2 pr-2 ${extraClass}">${value}</td>`;
                document.getElementById("check-foot").innerHTML = drafts.length === 0 ? "" :
                    `<tr class="border-t-2 border-slate-300">${foot("Tổng", "text-left")}${foot(total.qty)}${foot(dong(total.fob))}${foot(dong(total.adj))}${foot(dong(total.customs))}${foot(dong(total.duty))}${foot(dong(total.other))}${foot(dong(total.vatBase))}${foot(dong(total.vat))}${foot(dong(total.extra))}${foot(dong(total.landed), "text-emerald-700")}${foot("")}</tr>`;

                // Tổng hợp.
                text("sum-charges", dong(chargeSum));
                text("vat-amount", dong(total.vat));
                text("t-fob", dong(total.fob));
                text("t-adj", dong(total.adj));
                text("t-duty", dong(total.duty));
                text("t-other", dong(total.other));
                text("t-log", dong(total.log));
                text("t-nonded", dong(total.nonded));
                text("t-vatcost-label", deductible ? "VAT tính vào giá vốn (đang tắt vì được khấu trừ)" : "VAT tính vào giá vốn");
                text("t-vatcost", dong(deductible ? 0 : total.vat));
                text("t-landed", dong(total.landed));
                text("t-vatded", dong(deductible ? total.vat : 0));
                text("t-ded", dong(total.ded));
                const cash = total.landed + (deductible ? total.vat : 0) + total.ded;
                text("sum-order", dong(cash));
                text("f-landed", dong(total.landed));
                text("f-cash", dong(cash));
                text("line-count", String(lineRows.length));
            }

            document.getElementById("add-line").addEventListener("click", () => {
                if (!lineTemplate) {
                    return;
                }

                const row = lineTemplate.cloneNode(true);
                row.querySelectorAll("input").forEach((input) => { input.value = "0"; });
                row.querySelector(".line-id").value = "";
                row.querySelector("select").selectedIndex = 0;
                lineBody.appendChild(row);
                reindexLines();
                paint();
            });
            lineBody.addEventListener("click", (event) => {
                const button = event.target.closest(".remove-line");
                if (!button) {
                    return;
                }

                button.closest(".line-row")?.remove();
                reindexLines();
                paint();
            });
            document.getElementById("add-charge").addEventListener("click", () => {
                const index = chargeBody.querySelectorAll(".charge-row").length;
                const holder = document.createElement("tbody");
                holder.innerHTML = chargeTemplate.innerHTML.replaceAll("__i__", String(index));
                const row = holder.querySelector("tr");
                chargeBody.appendChild(row);
                row.querySelector(".charge-label")?.focus();
                reindexCharges();
                paint();
            });
            chargeBody.addEventListener("click", (event) => {
                const button = event.target.closest(".remove-charge");
                if (!button) {
                    return;
                }

                button.closest(".charge-row")?.remove();
                reindexCharges();
                paint();
            });
            const form = document.getElementById("purchase-draft");
            form.addEventListener("input", paint);
            form.addEventListener("change", paint);
            // Người dùng tự chọn cách chia cho một dòng mẫu thì dừng tự động cho dòng đó.
            chargeBody.addEventListener("change", (event) => {
                if (event.isTrusted && event.target.classList.contains("charge-method")) {
                    const row = event.target.closest(".charge-row");
                    row.dataset.auto = "0";
                    row.querySelector(".charge-auto").textContent = "";
                }
            });

            // Cảnh báo khi rời form mà còn dữ liệu chưa lưu (đóng bằng ×, Hủy, hoặc tải lại trang).
            let dirty = false;
            form.addEventListener("input", () => { dirty = true; });
            // Số tiền gửi lên dạng số thô do vnd-input.js lo (sự kiện formdata).
            form.addEventListener("submit", () => {
                dirty = false;
            });
            // Esc cũng là một cách rời form: còn dữ liệu chưa lưu thì hỏi như nút ×. Hộp thoại đã đóng (bỏ dữ liệu) thì
            // không còn gì để giữ, nên xóa cờ, nếu không cảnh báo rời trang sẽ chặn mọi lần bấm link sau đó.
            const purchaseDialog = document.getElementById("purchase-dialog");
            purchaseDialog?.addEventListener("cancel", (event) => {
                if (!dirty) {
                    return;
                }

                event.preventDefault();
                window.pancrasConfirm({
                    title: "Bỏ thay đổi",
                    message: "Bỏ các thay đổi chưa lưu?",
                    label: "Bỏ thay đổi",
                    tone: "danger",
                    onOk: () => {
                        dirty = false;
                        purchaseDialog.close();
                    }
                });
            });
            purchaseDialog?.addEventListener("close", () => {
                dirty = false;
            });
            window.addEventListener("beforeunload", (event) => {
                if (dirty) {
                    event.preventDefault();
                    event.returnValue = "";
                }
            });
            document.querySelectorAll("dialog a[data-leave]").forEach((link) => {
                link.addEventListener("click", (event) => {
                    if (!dirty) {
                        return;
                    }

                    event.preventDefault();
                    window.pancrasConfirm({
                        title: "Bỏ thay đổi",
                        message: "Bỏ các thay đổi chưa lưu?",
                        label: "Bỏ thay đổi",
                        tone: "danger",
                        onOk: () => {
                            dirty = false;
                            window.location.href = link.href;
                        }
                    });
                });
            });

            reindexLines();
            reindexCharges();
            paint();
            // Vào từ nút "Sửa chi phí" ở chi tiết đơn: cuộn thẳng tới phần chi phí chung.
            if (new URLSearchParams(location.search).get("focus") === "charges") {
                document.getElementById("charge-section")?.scrollIntoView({ block: "start" });
            }
        })();
