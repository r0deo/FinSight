(() => {
    const initialize = () => {
        const cards = document.querySelectorAll('[data-decision-cards], [data-simulation-navigation], [data-product-selection]');
        const form = document.querySelector('[data-decision-form], [data-discount-form], [data-delivery-form], [data-what-if-form]');
        if (!cards.length) return;
        const values = () => form ? JSON.stringify([...new FormData(form)]) : '';
        const initial = values();
        cards.forEach(nav => nav.addEventListener('click', event => {
            const link = event.target.closest('[data-decision-link]');
            if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            const target = new URL(link.href, window.location.href);
            const samePage = target.pathname === window.location.pathname && target.search === window.location.search;
            if (link.getAttribute('aria-current') === 'page'
                && (!nav.matches('[data-simulation-navigation]') || samePage)) {
                event.preventDefault(); return;
            }
            if (form && (form.dataset.hasDraft === 'true' || values() !== initial)
                && !window.confirm(nav.dataset.discardWarning)) event.preventDefault();
        }));
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true });
    else initialize();
})();
