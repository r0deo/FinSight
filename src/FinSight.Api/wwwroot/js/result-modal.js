(() => {
    const focus = element => {
        if (!element) return;
        for (let parent = element.parentElement; parent; parent = parent.parentElement)
            if (parent.matches('details')) parent.open = true;
        element.focus();
    };
    // Run after page-specific scripts have revealed/enabled their conditional fields.
    const initialize = () => {
        const invalid = [...document.querySelectorAll('main input.input-validation-error:not([type=hidden]), main select.input-validation-error, main textarea.input-validation-error, main [aria-invalid=true]:not([type=hidden])')]
            .find(field => !field.disabled && !field.closest('[hidden]'));
        const summary = document.querySelector('main .validation-summary-errors');
        document.querySelectorAll('[data-result-modal]').forEach(section => {
            if (typeof HTMLDialogElement === 'undefined' || !HTMLDialogElement.prototype.showModal) return;
            const dialog = document.createElement('dialog');
            for (const attribute of section.attributes) dialog.setAttribute(attribute.name, attribute.value);
            dialog.append(...section.childNodes);
            section.replaceWith(dialog);
            const shell = dialog.closest('[data-result-shell]');
            const reopen = shell.querySelector('[data-result-open]');
            const close = dialog.querySelector('[data-result-close]');
            const source = document.querySelector(dialog.dataset.sourceForm);
            const trigger = document.querySelector(dialog.dataset.returnFocus);
            let returnTo = trigger;
            const open = invoker => {
                if (reopen.hidden || dialog.open) return;
                returnTo = invoker || trigger;
                dialog.showModal();
                document.documentElement.classList.add('result-modal-open');
                dialog.querySelector('.result-modal-body').scrollTop = 0;
                close.focus();
            };
            reopen.hidden = false;
            close.hidden = false;
            reopen.addEventListener('click', () => open(reopen));
            close.addEventListener('click', () => dialog.close());
            // Native dialog supplies Escape and inert background content. Explicit wrapping
            // keeps Tab/Shift+Tab inside the result rather than moving to browser chrome.
            dialog.addEventListener('keydown', event => {
                if (event.key !== 'Tab') return;
                const items = [...dialog.querySelectorAll('button, a[href], input:not([type=hidden]), select, textarea, [tabindex]')]
                    .filter(item => !item.disabled && item.tabIndex >= 0 && item.getClientRects().length);
                const first = items[0], last = items[items.length - 1];
                if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault(); last?.focus();
                } else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault(); first?.focus();
                }
            });
            dialog.addEventListener('close', () => {
                document.documentElement.classList.remove('result-modal-open');
                focus(returnTo?.isConnected && !returnTo.hidden ? returnTo : trigger);
            });
            const invalidate = event => {
                if (!event.target.matches('input,select,textarea, [data-preset]')) return;
                // A changed draft must never reopen an old result or offer its save/apply action.
                reopen.hidden = true;
                if (dialog.open) dialog.close();
            };
            const costing = source?.querySelector('[data-cost-helper]');
            const inputs = costing || source;
            inputs?.addEventListener('input', invalidate);
            inputs?.addEventListener('change', invalidate);
            inputs?.addEventListener('click', event => { if (event.target.closest('[data-preset]')) invalidate({ target: event.target.closest('[data-preset]') }); });
            if (dialog.dataset.autoOpen === 'true' && !invalid && !summary) open(trigger);
        });
        if (invalid) focus(invalid);
        else if (summary) { summary.tabIndex = -1; summary.focus(); }
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', initialize, { once: true });
    else initialize();
})();
