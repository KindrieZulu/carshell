// Low-cost 3D: a small mousemove-driven perspective tilt on any element
// with class "tilt" wrapping a ".tilt-inner" child. Pure CSS transforms,
// no dependency, respects prefers-reduced-motion.
(function () {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduceMotion) return;

    function attach(el) {
        const inner = el.querySelector('.tilt-inner');
        if (!inner) return;

        el.addEventListener('mousemove', (event) => {
            const rect = el.getBoundingClientRect();
            const x = (event.clientX - rect.left) / rect.width - 0.5;
            const y = (event.clientY - rect.top) / rect.height - 0.5;
            const maxDeg = 6;
            inner.style.transform = `translateY(-8px) rotateX(${(-y * maxDeg).toFixed(2)}deg) rotateY(${(x * maxDeg).toFixed(2)}deg) translateZ(0)`;
        });

        el.addEventListener('mouseleave', () => {
            inner.style.transform = 'translateY(0) rotateX(0deg) rotateY(0deg) translateZ(0)';
        });
    }

    function init() {
        document.querySelectorAll('.tilt').forEach(attach);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Exposed so pages that render cards after an async fetch (e.g. the
    // admin table) can re-scan once new .tilt elements exist in the DOM.
    window.CarShellTilt = { init };
})();
