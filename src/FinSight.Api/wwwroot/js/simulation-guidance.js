(() => {
    document.querySelectorAll('[data-input-examples]').forEach(source => {
        const form = source.closest('form');
        const period = form.querySelector('[name=ComparisonPeriod], [name=Period]');
        const groups = {
            unitPrice: ['ProposedPrice', 'DeliveryUnitPrice', 'AlternativePrice'],
            unitCost: ['NewUnitCost', 'AlternativeSupplierCost'],
            orderCost: ['PackagingPerOrder', 'OtherChargesPerOrder'],
            periodCost: ['AdditionalDecisionCost', 'CampaignCost', 'FixedPeriodCost'],
            quantity: ['CurrentSalesQuantity', 'ExpectedSalesQuantity', 'CurrentQuantity', 'AlternativePriceQuantity', 'AlternativeSupplierQuantity'],
            orders: ['CurrentDirectOrders', 'ExpectedDeliveryOrders', 'TransferredOrders'],
            units: ['UnitsPerOrder'],
            percent: ['DeclinePercentage', 'DiscountPercentage', 'CommissionPercentage', 'CostIncreasePercent', 'SalesDeclinePercent', 'AllocationPercentage'],
            period: ['ComparisonPeriod', 'Period'], month: ['CostMonth']
        };
        const examples = [];
        Object.entries(groups).forEach(([kind, names]) => names.forEach(name => {
            const input = form.querySelector(`[name="${name}"]:not([type=hidden])`);
            if (!input) return;
            const help = document.createElement('small');
            help.className = 'input-example';
            help.id = `${input.id}-example`;
            input.closest('.field').append(help);
            input.setAttribute('aria-describedby', [input.getAttribute('aria-describedby'), help.id].filter(Boolean).join(' '));
            examples.push([help, source.dataset[kind]]);
        }));
        const update = () => examples.forEach(([help, template]) => {
            help.textContent = template.replace('{period}', period?.selectedOptions[0]?.textContent || '');
        });
        period?.addEventListener('change', update);
        update();
        // Required fields inside collapsed optional sections must remain reachable.
        form.addEventListener('invalid', event => {
            for (let parent = event.target.parentElement; parent && parent !== form; parent = parent.parentElement)
                if (parent.matches('details')) parent.open = true;
        }, true);
    });
})();
