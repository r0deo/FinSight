(() => {
    const form = document.querySelector('[data-what-if-form]');
    if (!form) return;
    const field = name => form.elements.namedItem(name);
    const selectedMode = () => form.querySelector('[name="CostInputMode"]:checked')?.value;
    const blocks = [
        [form.querySelector('[data-cost-percent]'), () => selectedMode() === 'Percentage'],
        [form.querySelector('[data-cost-direct]'), () => selectedMode() === 'NewUnitCost'],
        [form.querySelector('[data-price-response]'), () => field('UsePriceResponse').checked],
        [form.querySelector('[data-supplier-response]'), () => field('UseSupplierResponse').checked]
    ];
    const sync = () => {
        for (const [block, active] of blocks) {
            block.hidden = !active();
            for (const input of block.querySelectorAll('input')) input.disabled = !active();
        }
        form.querySelector('[data-price-note]').hidden = !field('UsePriceResponse').checked;
        form.querySelector('[data-supplier-note]').hidden = !field('UseSupplierResponse').checked;
    };
    const digits = text => text.replace(/[٠-٩]/g, c => String(c.charCodeAt(0) - 0x660))
        .replace(/[۰-۹]/g, c => String(c.charCodeAt(0) - 0x6F0)).replace(/٫/g, '.');
    const parse = name => Number(digits(field(name).value));
    let lastSuggested = null;
    const suggest = () => {
        if (field('CurrentQuantity').value.trim() === '' || field('SalesDeclinePercent').value.trim() === '') return;
        const quantity = parse('CurrentQuantity');
        const decline = parse('SalesDeclinePercent');
        if (!Number.isInteger(quantity) || quantity < 0 || !Number.isFinite(decline)
            || decline < 0 || decline > 100) return;
        const suggested = Math.floor(quantity * (1 - decline / 100));
        for (const input of form.querySelectorAll('[data-prefill-quantity]')) {
            if (input.value === '' || input.value === String(lastSuggested)) input.value = String(suggested);
        }
        lastSuggested = suggested;
    };
    sync();
    form.addEventListener('input', event => {
        if (event.target.name === 'CurrentQuantity' || event.target.name === 'SalesDeclinePercent') suggest();
        const results = document.querySelector('[data-what-if-results]');
        if (results) results.hidden = true;
    });
    form.addEventListener('change', () => {
        sync();
        const results = document.querySelector('[data-what-if-results]');
        if (results) results.hidden = true;
    });
    suggest();
})();
