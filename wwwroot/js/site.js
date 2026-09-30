// Formats an amount like the server does: ₦185,000 or ₦1,234.50.
function money(amount, symbol) {
    const whole = Math.abs(amount % 1) < 0.005;
    return (symbol || '₦') + amount.toLocaleString('en-US', { minimumFractionDigits: whole ? 0 : 2, maximumFractionDigits: whole ? 0 : 2 });
}

// Live preview of a chosen cover image.
document.querySelectorAll('input[type=file][data-preview]').forEach(input => {
    input.addEventListener('change', () => {
        const target = document.querySelector(input.dataset.preview);
        const file = input.files && input.files[0];
        if (!target || !file || !file.type.startsWith('image/')) return;
        const img = document.createElement('img');
        img.className = 'product-img is-cover';
        img.alt = 'Cover preview';
        img.src = URL.createObjectURL(file);
        target.replaceChildren(img);
    });
});

// Estimated royalty per sale on the ebook form.
const priceInput = document.querySelector('[data-royalty-input]');
const royaltyOutput = document.querySelector('[data-royalty-output]');
if (priceInput && royaltyOutput) {
    const rate = parseFloat(royaltyOutput.dataset.rate) || 0;
    const update = () => {
        const price = parseFloat(priceInput.value) || 0;
        royaltyOutput.textContent = money(price * rate, royaltyOutput.dataset.currency);
    };
    priceInput.addEventListener('input', update);
    update();
}


// Flash sale countdowns.
document.querySelectorAll('[data-countdown]').forEach(el => {
    const end = new Date(el.dataset.countdown).getTime();
    const parts = { d: el.querySelector('[data-part=d]'), h: el.querySelector('[data-part=h]'), m: el.querySelector('[data-part=m]'), s: el.querySelector('[data-part=s]') };
    const tick = () => {
        const left = Math.max(0, Math.floor((end - Date.now()) / 1000));
        const values = { d: Math.floor(left / 86400), h: Math.floor(left % 86400 / 3600), m: Math.floor(left % 3600 / 60), s: left % 60 };
        for (const key in parts) if (parts[key]) parts[key].textContent = String(values[key]).padStart(2, '0');
        if (left === 0) clearInterval(timer);
    };
    const timer = setInterval(tick, 1000);
    tick();
});

// Delivery estimate on product pages.
document.querySelectorAll('.delivery-box').forEach(box => {
    const data = JSON.parse(box.dataset.delivery || '{}');
    const freeOver = parseFloat(box.dataset.freeOver) || 0;
    const price = parseFloat(box.dataset.price) || 0;
    const select = box.querySelector('select');
    const result = box.querySelector('[data-delivery-result]');
    const update = () => {
        const info = data[select.value];
        if (!info) return;
        const free = freeOver > 0 && price >= freeOver;
        result.innerHTML = `Delivery: <strong>${free || info.fee === 0 ? 'FREE' : money(info.fee)}</strong> · Arrives in ${info.eta}` +
            (!free && freeOver > 0 ? `<br><span class="text-secondary">Free delivery on orders over ${money(freeOver)}</span>` : '');
    };
    select.addEventListener('change', update);
    update();
});

// Live delivery fee and total at checkout.
const checkout = document.getElementById('checkout-form');
if (checkout) {
    const fees = JSON.parse(checkout.dataset.fees || '{}');
    const symbol = checkout.dataset.currency;
    const subtotal = parseFloat(checkout.dataset.subtotal) || 0;
    const discount = parseFloat(checkout.dataset.discount) || 0;
    const physical = checkout.dataset.physical === 'true';
    const state = checkout.querySelector('[data-state-select]');
    const shippingEl = checkout.querySelector('[data-shipping]');
    const totalEl = checkout.querySelector('[data-total]');
    const update = () => {
        if (!physical || !state || !shippingEl) return;
        if (!state.value) { shippingEl.textContent = 'Choose state'; totalEl.textContent = money(subtotal - discount, symbol); return; }
        const fee = fees[state.value] ?? parseFloat(checkout.dataset.defaultFee) ?? 0;
        shippingEl.textContent = fee === 0 ? 'FREE' : money(fee, symbol);
        totalEl.textContent = money(subtotal + fee - discount, symbol);
    };
    state?.addEventListener('change', update);
    update();
}

// Chat: keep the newest message in view and refresh every few seconds.
const chatBody = document.getElementById('chat-body');
if (chatBody) {
    const toBottom = () => { chatBody.scrollTop = chatBody.scrollHeight; };
    toBottom();
    setInterval(async () => {
        try {
            const response = await fetch(chatBody.dataset.refresh, { headers: { 'X-Requested-With': 'fetch' } });
            if (!response.ok) return;
            const html = await response.text();
            const nearBottom = chatBody.scrollHeight - chatBody.scrollTop - chatBody.clientHeight < 80;
            if (html !== chatBody.innerHTML) { chatBody.innerHTML = html; if (nearBottom) toBottom(); }
        } catch { /* offline: try again next tick */ }
    }, 8000);
}
