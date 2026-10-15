(() => {
    const form = document.querySelector('[data-product-form]');
    if (!form) return;
    const helper = form.querySelector('[data-cost-helper]');
    const result = form.querySelector('[data-product-cost-result]');
    // Enter in an ingredient field calculates; it must not activate the first Remove button.
    form.addEventListener('keydown', event => {
        if (event.key !== 'Enter' || event.isComposing || !event.target.matches('input[type=text]')) return;
        event.preventDefault();
        form.requestSubmit(event.target.closest('[data-cost-helper]')
            ? form.querySelector('button[value=calculate]') : form.querySelector('[data-product-save]'));
    });
    helper.addEventListener('input', event => {
        if (event.target.matches('input, select') && result) result.hidden = true;
    });
    helper.addEventListener('change', event => {
        if (event.target.matches('input, select') && result) result.hidden = true;
    });
    form.querySelector('#UnitCost').addEventListener('input', () => {
        form.querySelector('#UseCostHelper').value = 'false';
        form.querySelector('#ClearCostBreakdown').value = 'true';
        form.querySelector('[data-cost-selected]').hidden = true;
        form.querySelector('[data-cost-manual]').hidden = false;
    });
})();
