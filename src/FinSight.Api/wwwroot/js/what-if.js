(() => {
    const form = document.querySelector('[data-what-if-form]');
    if (!form) return;
    const field = name => form.querySelector('[name="' + name + '"]:not([type="hidden"])');
    const checked = name => field(name)?.checked === true;
    const active = (selector, enabled) => {
        const block = form.querySelector(selector);
        if (!block) return;
        block.hidden = !enabled;
        block.querySelectorAll('input,select,button').forEach(input => input.disabled = !enabled);
    };
    const invalidate = () => {
        const results = document.querySelectorAll('[data-what-if-results]');
        results.forEach(x => x.hidden = true);
        if (results.length) document.querySelector('[data-what-if-stale]').hidden = false;
    };
    const sync = () => {
        const cost = field('Concern').value !== 'Demand', demand = field('Concern').value !== 'Supplier';
        const mode = form.querySelector('[name="CostInputMode"]:checked')?.value;
        active('[data-cost-assumption]', cost);
        active('[data-demand-assumption]', demand);
        active('[data-cost-percent]', cost && mode === 'Percentage');
        active('[data-cost-direct]', cost && mode === 'NewUnitCost');
        active('[data-price-response]', checked('UsePriceResponse'));
        active('[data-supplier-response]', checked('UseSupplierResponse'));
        for (const [sel, name] of [['[data-price-note]', 'UsePriceResponse'], ['[data-supplier-note]', 'UseSupplierResponse']]) {
            const note = form.querySelector(sel); if (note) note.hidden = !checked(name);
        }
        active('[data-month-context]', field('Period').value === 'Month');
    };
    const digits = text => text.replace(/[٠-٩]/g, c => String(c.charCodeAt(0) - 0x660))
        .replace(/[۰-۹]/g, c => String(c.charCodeAt(0) - 0x6F0)).replace(/٫/g, '.');
    let lastSuggested = null;
    const suggest = () => {
        const q = digits(field('CurrentQuantity').value), d = field('Concern').value === 'Supplier' ? '0' : digits(field('SalesDeclinePercent').value);
        if (!/^\d+$/.test(q) || !/^\d+(\.\d{1,2})?$/.test(d)) return;
        const quantity = Number(q), hundredths = Math.round(Number(d) * 100);
        if (quantity > 1000000 || hundredths > 10000) return;
        const suggested = Math.floor(quantity * (10000 - hundredths) / 10000);
        form.querySelectorAll('[data-prefill-quantity]').forEach(input => {
            if (input.value === '' || input.value === String(lastSuggested)) input.value = String(suggested);
        });
        lastSuggested = suggested;
    };
    form.addEventListener('input', () => { suggest(); invalidate(); });
    form.addEventListener('change', () => { sync(); suggest(); invalidate(); });
    form.querySelectorAll('[data-preset]').forEach(button => button.addEventListener('click', () => {
        if (button.dataset.preset === 'cost') {
            form.querySelector('[name="CostInputMode"][value="Percentage"]').checked = true;
            field('CostIncreasePercent').value = '20';
        } else field('SalesDeclinePercent').value = '10';
        sync(); suggest(); invalidate();
    }));
    sync(); suggest();
    document.querySelectorAll('[data-what-if-save]').forEach(save => save.addEventListener('submit', () =>
        save.querySelectorAll('button[type="submit"]').forEach(b => b.disabled = true)));
})();
