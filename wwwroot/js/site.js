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
        royaltyOutput.textContent = (price * rate).toLocaleString('en-US', { style: 'currency', currency: 'USD' });
    };
    priceInput.addEventListener('input', update);
    update();
}
