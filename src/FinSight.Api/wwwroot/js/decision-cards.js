(() => {
    const initialize = () => {
        const cards = document.querySelector('[data-decision-cards]');
        const form = document.querySelector('[data-decision-form], [data-discount-form], [data-delivery-form]');
        if (!cards) return;
        const values = () => form ? JSON.stringify([...new FormData(form)]) : '';
        const initial = values();
        cards.addEventListener('click', event => {
            const link = event.target.closest('[data-decision-link]');
            if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            if (link.getAttribute('aria-current') === 'page') { event.preventDefault(); return; }
            if (form && (form.dataset.hasDraft === 'true' || values() !== initial)
                && !window.confirm(cards.dataset.discardWarning)) event.preventDefault();
        });
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true });
    else initialize();
})();
