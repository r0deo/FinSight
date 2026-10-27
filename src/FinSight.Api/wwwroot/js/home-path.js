(() => {
    const root = document.querySelector('[data-home-path]');
    if (!root) return;
    const svg = root.querySelector('svg');
    const line = svg.querySelector('path');
    const point = svg.querySelector('circle');
    const motion = matchMedia('(prefers-reduced-motion: reduce)');
    let frame = 0;
    let entered = false;
    let previousSize = '';

    const draw = () => {
        cancelAnimationFrame(frame);
        const bounds = root.getBoundingClientRect();
        const position = element => {
            const rect = element.getBoundingClientRect();
            return { x: rect.left + rect.width / 2 - bounds.left, y: rect.top + rect.height / 2 - bounds.top };
        };
        const [first, second] = [...root.querySelectorAll('[data-path-stop]')].map(position);
        const action = root.querySelector('[data-path-action]');
        const end = position(action);
        end.y -= action.getBoundingClientRect().height / 2 + 5;
        const rtl = getComputedStyle(root).direction === 'rtl';
        const stacked = Math.abs(first.y - second.y) > 10;
        const edge = stacked ? (rtl ? bounds.width - 5 : 5) : (rtl ? 5 : bounds.width - 5);
        // The route stays in the gutters, never over text or interactive controls.
        const start = stacked
            ? `M ${first.x} ${first.y} C ${edge} ${first.y}, ${edge} ${first.y}, ${edge} ${first.y + 20} L ${edge} ${second.y - 20} Q ${edge} ${second.y}, ${second.x} ${second.y}`
            : `M ${first.x} ${first.y} L ${second.x} ${second.y}`;
        line.setAttribute('d', `${start} C ${edge} ${second.y}, ${edge} ${second.y}, ${edge} ${second.y + 20} L ${edge} ${end.y - 24} Q ${edge} ${end.y - 12}, ${edge + (end.x - edge) * .15} ${end.y - 12} L ${end.x + (edge - end.x) * .06} ${end.y - 12} Q ${end.x} ${end.y - 12}, ${end.x} ${end.y}`);
        svg.setAttribute('viewBox', `0 0 ${bounds.width} ${bounds.height}`);
        const length = line.getTotalLength();
        line.style.strokeDasharray = `${length}`;
        const progress = fraction => {
            line.style.strokeDashoffset = `${length * (1 - fraction)}`;
            const current = line.getPointAtLength(length * fraction);
            point.setAttribute('cx', current.x);
            point.setAttribute('cy', current.y);
        };
        svg.classList.add('is-ready');
        if (entered || motion.matches) { progress(1); entered = true; return; }
        entered = true;
        progress(0);
        const started = performance.now();
        const animate = now => {
            const elapsed = Math.min(1, (now - started) / 1800);
            progress(1 - Math.pow(1 - elapsed, 2));
            if (elapsed < 1) frame = requestAnimationFrame(animate);
        };
        frame = requestAnimationFrame(animate);
    };
    document.fonts.ready.then(() => {
        new ResizeObserver(() => {
            const size = `${root.offsetWidth}:${root.offsetHeight}`;
            if (size === previousSize) return;
            previousSize = size;
            draw();
        }).observe(root);
        motion.addEventListener('change', draw);
    });
})();
