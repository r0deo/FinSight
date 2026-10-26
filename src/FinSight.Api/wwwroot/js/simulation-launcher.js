(() => {
    const form = document.querySelector('[data-simulation-launcher]');
    if (!form) return;
    const key = 'rebaz.simulation-selection';
    const products = [...form.querySelectorAll('input[name="productId"]')];
    const decisions = [...form.querySelectorAll('input[name="decision"]')];
    const button = form.querySelector('[data-start-simulation]');
    const status = form.querySelector('[role="status"]');
    const selected = items => items.find(input => input.checked);
    // Store IDs only, never customer names or financial values. Restore a product
    // only if the server returned it on this page for the authenticated business.
    let saved = {};
    try { saved = JSON.parse(sessionStorage.getItem(key) || '{}') || {}; } catch { /* Storage may be disabled. */ }
    const query = new URLSearchParams(location.search);
    const product = products.find(input => input.value === (query.get('selectedProductId') || saved.productId));
    const decision = decisions.find(input => input.value === (query.get('decision') || saved.decision));
    if (!selected(products) && product) product.checked = true;
    if (!selected(decisions) && decision) decision.checked = true;
    const update = () => {
        const product = selected(products);
        const decision = selected(decisions);
        button.disabled = !product || !decision;
        status.textContent = !product && !decision ? form.dataset.missingBoth
            : !product ? form.dataset.missingProduct : !decision ? form.dataset.missingDecision : form.dataset.ready;
        if (decision) form.action = decision.dataset.route;
        try { sessionStorage.setItem(key, JSON.stringify({ productId: product?.value, decision: decision?.value })); }
        catch { /* The current form still works when storage is unavailable. */ }
    };
    form.addEventListener('change', update);
    form.addEventListener('submit', event => {
        update();
        if (button.disabled || !form.reportValidity()) { event.preventDefault(); return; }
        // Navigate only to one of the existing, server-rendered local routes.
        event.preventDefault();
        const destination = new URL(selected(decisions).dataset.route, location.origin);
        if (destination.origin !== location.origin) return;
        destination.search = new URLSearchParams({ productId: selected(products).value });
        location.assign(destination.pathname + destination.search);
    });
    window.addEventListener('pageshow', update);
    update();
})();
