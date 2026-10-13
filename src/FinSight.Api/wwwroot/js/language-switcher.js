(() => {
    const forms = [...document.querySelectorAll('main form')];
    const snapshots = new Map(forms.map(form => [form, new FormData(form)]));
    const languageForm = document.querySelector('.language-switch');
    if (!languageForm) return;
    languageForm.addEventListener('submit', event => {
        const changed = forms.some(form => {
            const before = snapshots.get(form);
            const now = new FormData(form);
            const keys = new Set([...before.keys(), ...now.keys()]);
            return [...keys].some(key => String(before.get(key) ?? '') !== String(now.get(key) ?? ''));
        });
        if (changed && !window.confirm(languageForm.dataset.unsavedWarning)) event.preventDefault();
    });
})();

document.querySelectorAll('form[data-confirm]').forEach(form => {
    form.addEventListener('submit', event => {
        if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    });
});
