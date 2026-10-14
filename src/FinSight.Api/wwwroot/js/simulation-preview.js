(() => {
    const form = document.querySelector('[data-decision-form]');
    if (!form) return;
    const unknown = form.querySelector('[name="UnknownExpectedSales"][type="checkbox"]');
    const expected = form.querySelector('[name="ExpectedSalesQuantity"]');
    const expectedField = form.querySelector('[data-expected-field]');
    const results = document.querySelector('[data-decision-results]');
    const save = document.querySelector('[data-decision-save]');
    const syncExpected = () => {
        const monthBlock = form.querySelector('[data-month-context]');
        if (monthBlock) {
            const monthly = form.querySelector('[name="ComparisonPeriod"]').value === 'Month';
            monthBlock.hidden = !monthly;
            monthBlock.querySelector('input').disabled = !monthly;
        }
        expected.disabled = unknown.checked;
        expected.required = !unknown.checked;
        expectedField.hidden = unknown.checked;
        if (unknown.checked) expected.value = '';
    };
    syncExpected();
    form.addEventListener('input', event => {
        syncExpected();
        if (results) results.hidden = true;
        if (save) save.querySelector('button[type="submit"]').disabled = true;
    });
    form.addEventListener('change', event => {
        syncExpected();
        if (results) results.hidden = true;
        if (save) save.querySelector('button[type="submit"]').disabled = true;
    });
    if (save) save.addEventListener('submit', () => save.querySelector('button[type="submit"]').disabled = true);
})();
