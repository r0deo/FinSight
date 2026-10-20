(() => {
    const initialize = () => document.querySelectorAll('[data-cost-link]').forEach(section => {
        const link = section.querySelector('#LinkCosts');
        const allocate = section.querySelector('#AllocateCosts');
        const linked = section.querySelector('[data-linked-fields]');
        const allocated = section.querySelector('[data-allocation-fields]');
        const update = () => {
            linked.hidden = !link.checked;
            allocated.hidden = !link.checked || !allocate.checked;
            linked.querySelectorAll('input').forEach(input => input.disabled = !link.checked);
            allocated.querySelectorAll('input').forEach(input => input.disabled = !link.checked || !allocate.checked);
            section.querySelector('#CostMonth').required = link.checked;
            section.querySelector('#AllocationPercentage').required = link.checked && allocate.checked;
        };
        link.addEventListener('change', update); allocate.addEventListener('change', update); update();
    });
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true }); else initialize();
})();
