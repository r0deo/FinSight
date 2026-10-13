(() => {
    const form = document.querySelector('[data-decision-form]');
    if (!form) return;
    const unknown = form.querySelector('[name="UnknownExpectedSales"][type="checkbox"]');
    const expected = form.querySelector('[name="ExpectedSalesQuantity"]');
    const results = document.querySelector('[data-decision-results]');
    const save = document.querySelector('[data-decision-save]');
    const syncExpected = () => {
        expected.disabled = unknown.checked;
        expected.required = !unknown.checked;
    };
    syncExpected();
    form.addEventListener('input', event => {
        if (event.target === unknown) syncExpected();
        if (results) results.hidden = true;
        if (save) save.querySelector('button[type="submit"]').disabled = true;
    });
    form.addEventListener('change', event => {
        if (event.target === unknown) syncExpected();
        if (results) results.hidden = true;
        if (save) save.querySelector('button[type="submit"]').disabled = true;
    });
})();
