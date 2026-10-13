(() => {
    const forms = [...document.querySelectorAll('main form')];
    const snapshots = new Map(forms.map(form => [form, new FormData(form)]));
    const languageForm = document.querySelector('.language-switch');
    if (!languageForm) return;
    const languageSelect = languageForm.querySelector('select[name="culture"]');
    const initialCulture = languageSelect.value;
    languageSelect.addEventListener('change', () => {
        if (languageSelect.value !== initialCulture) languageForm.requestSubmit();
    });
    languageForm.addEventListener('submit', event => {
        const changed = forms.some(form => {
            const before = snapshots.get(form);
            const now = new FormData(form);
            const keys = new Set([...before.keys(), ...now.keys()]);
            return [...keys].some(key => String(before.get(key) ?? '') !== String(now.get(key) ?? ''));
        });
        if (changed && !window.confirm(languageForm.dataset.unsavedWarning)) {
            event.preventDefault();
            languageSelect.value = initialCulture;
        }
    });
})();

document.querySelectorAll('form[data-confirm]').forEach(form => {
    form.addEventListener('submit', event => {
        if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    });
});
