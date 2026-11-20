    const stockMap = JSON.parse(document.getElementById('stock-map').textContent || '[]');
    function onHandAt(itemId, locationId) {
        const row = stockMap.find(point => point.item === itemId && point.location === locationId);
        return row ? row.onHand : 0;
    }
    function availableAt(itemId, locationId) {
        const row = stockMap.find(point => point.item === itemId && point.location === locationId);
        return row ? row.available : 0;
    }
    function openTransfer(id) {
        const dialog = document.getElementById('transfer-' + id);
        if (dialog && !dialog.open) {
            dialog.showModal();
        }
    }
    const transferItemIds = new Map(Array.from(document.querySelectorAll('#transfer-items option')).map(option => [option.value, option.dataset.id]));
    function refreshTransferLine(line) {
        const text = line.querySelector('.transfer-item-search');
        const hidden = line.querySelector('.transfer-item');
        const quantity = line.querySelector('.transfer-qty');
        const hint = line.querySelector('.transfer-stock');
        const typed = text.value.trim();
        const id = transferItemIds.get(typed) || '';
        hidden.value = id;
        text.setCustomValidity(typed && !id ? 'Chọn mặt hàng trong danh sách gợi ý.' : '');
        hint.className = 'transfer-stock text-xs md:col-span-2 text-slate-500';
        if (!id) {
            hint.textContent = '';
            return;
        }
        const source = document.getElementById('transfer-source').value;
        if (!source) {
            hint.textContent = 'Chọn kho xuất để xem tồn khả dụng.';
            return;
        }
        const available = availableAt(id, source);
        const wanted = parseInt(quantity.value, 10) || 0;
        if (wanted > available) {
            hint.className = 'transfer-stock text-xs md:col-span-2 font-semibold text-rose-700';
            hint.textContent = 'Kho xuất chỉ còn khả dụng ' + available + ', ít hơn số lượng đề nghị (' + wanted + '). Phiếu có thể không duyệt được.';
        } else {
            hint.textContent = 'Tồn khả dụng ở kho xuất: ' + available;
        }
    }
    function refreshTransferLines() {
        document.querySelectorAll('#transfer-lines .transfer-line').forEach(refreshTransferLine);
    }
    function reindexTransferLines() {
        document.querySelectorAll('#transfer-lines .transfer-line').forEach((line, index) => {
            line.querySelector('.transfer-item').name = 'Transfer.Lines[' + index + '].CatalogItemId';
            line.querySelector('.transfer-qty').name = 'Transfer.Lines[' + index + '].Quantity';
        });
    }
    document.getElementById('transfer-source').addEventListener('change', refreshTransferLines);
    document.getElementById('transfer-lines').addEventListener('input', event => {
        const line = event.target.closest('.transfer-line');
        if (line) {
            refreshTransferLine(line);
        }
    });
    document.getElementById('add-transfer-line').addEventListener('click', () => {
        const lines = document.getElementById('transfer-lines');
        const copy = lines.querySelector('.transfer-line').cloneNode(true);
        copy.querySelector('.transfer-item-search').value = '';
        copy.querySelector('.transfer-item').value = '';
        copy.querySelector('.transfer-qty').value = '';
        lines.appendChild(copy);
        reindexTransferLines();
        refreshTransferLines();
    });
    refreshTransferLines();

    document.addEventListener('click', event => {
        const button = event.target.closest('[data-fill-received]');
        if (!button) {
            return;
        }
        button.closest('form').querySelectorAll('input[data-shipped]').forEach(input => {
            input.value = input.dataset.shipped;
        });
    });

    // Hộp xác nhận dùng chung: form có data-confirm sẽ hiện hộp này trước khi gửi.
    // Nút gửi có thể mang data-confirm-title, data-confirm-message, data-confirm-label, data-confirm-tone="danger" và
    // data-reason="required|optional" (lý do được chép vào ô ẩn name="reason" của form).
    (function () {
        const dialog = document.getElementById('confirm-dialog');
        const title = document.getElementById('confirm-title');
        const message = document.getElementById('confirm-message');
        const reasonBox = document.getElementById('confirm-reason-box');
        const reason = document.getElementById('confirm-reason');
        const reasonError = document.getElementById('confirm-reason-error');
        const ok = document.getElementById('confirm-ok');
        let pending = null;
        function closeConfirm() {
            dialog.close();
            pending = null;
        }
        document.addEventListener('submit', event => {
            const form = event.target;
            if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-confirm') || form.dataset.confirmed === '1') {
                return;
            }
            event.preventDefault();
            const submitter = event.submitter;
            const read = name => (submitter && submitter.dataset[name]) || form.dataset[name] || '';
            const mode = read('reason');
            pending = { form, submitter, mode };
            title.textContent = read('confirmTitle') || 'Xác nhận';
            message.textContent = read('confirmMessage');
            ok.textContent = read('confirmLabel') || 'Xác nhận';
            ok.className = 'rounded-xl px-4 py-2 text-sm font-bold text-white ' + (read('confirmTone') === 'danger' ? 'bg-rose-600 hover:bg-rose-700' : 'bg-sky-600 hover:bg-sky-700');
            reasonBox.classList.toggle('hidden', !mode);
            reason.value = '';
            reasonError.classList.add('hidden');
            dialog.showModal();
        }, true);
        ok.addEventListener('click', () => {
            if (!pending) {
                return;
            }
            const { form, submitter, mode } = pending;
            if (mode) {
                const text = reason.value.trim();
                if (mode === 'required' && !text) {
                    reasonError.classList.remove('hidden');
                    reason.focus();
                    return;
                }
                const target = form.querySelector('input[name="reason"]');
                if (target) {
                    target.value = text;
                }
            }
            if (submitter && submitter.name) {
                const extra = document.createElement('input');
                extra.type = 'hidden';
                extra.name = submitter.name;
                extra.value = submitter.value;
                form.appendChild(extra);
            }
            form.dataset.confirmed = '1';
            dialog.close();
            form.submit();
        });
        document.getElementById('confirm-cancel').addEventListener('click', closeConfirm);
        document.getElementById('confirm-close').addEventListener('click', closeConfirm);
    })();
    function refreshBook() {
        const item = document.getElementById('stocktake-item').value;
        const location = document.getElementById('stocktake-location').value;
        const book = document.getElementById('stocktake-book');
        book.textContent = item && location ? 'Tồn sổ sách: ' + onHandAt(item, location) : 'Tồn sổ sách: —';
    }
    document.getElementById('stocktake-item').addEventListener('change', refreshBook);
    document.getElementById('stocktake-location').addEventListener('change', refreshBook);
    const boot = (document.getElementById('inventory-boot') || { dataset: {} }).dataset;
    if (boot.showTransferDialog === 'true') {
        document.getElementById('transfer-dialog').showModal();
    }
    if (boot.openTransfer) {
        openTransfer(boot.openTransfer);
    }
    if (boot.showStocktakeDialog === 'true') {
        document.getElementById('stocktake-dialog').showModal();
        refreshBook();
    }
    if (boot.showAdjustmentDialog === 'true') {
        document.getElementById('adjustment-dialog').showModal();
    }
    if (boot.showDisposalDialog === 'true') {
        document.getElementById('disposal-dialog').showModal();
    }
    document.addEventListener('click', event => {
        const opener = event.target.closest('[data-open-transfer]');
        if (opener) {
            openTransfer(opener.dataset.openTransfer);
        }
    });
