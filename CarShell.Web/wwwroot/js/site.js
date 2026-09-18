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
            inner.style.transform = `rotateX(${(-y * maxDeg).toFixed(2)}deg) rotateY(${(x * maxDeg).toFixed(2)}deg) translateZ(0)`;
        });

        el.addEventListener('mouseleave', () => {
            inner.style.transform = 'rotateX(0deg) rotateY(0deg) translateZ(0)';
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


// Header "Browse by Brand" menu: a plain toggle button + panel, no
// dependency. Closes on an outside click or Escape so it behaves like a
// normal dropdown even though it's built from scratch.
(function () {
    const toggle = document.getElementById('brand-menu-toggle');
    const panel = document.getElementById('brand-menu-panel');
    if (!toggle || !panel) return;

    function close() {
        panel.hidden = true;
        toggle.setAttribute('aria-expanded', 'false');
    }

    function open() {
        panel.hidden = false;
        toggle.setAttribute('aria-expanded', 'true');
    }

    toggle.addEventListener('click', (event) => {
        event.stopPropagation();
        if (panel.hidden) {
            open();
        } else {
            close();
        }
    });

    document.addEventListener('click', (event) => {
        if (!panel.hidden && !panel.contains(event.target)) {
            close();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') {
            close();
        }
    });
})();
