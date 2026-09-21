// One light/dark preference shared by the whole site -- public pages and
// the admin area both read/write the same storage key, so toggling the
// theme anywhere applies everywhere. Applying the theme happens via a
// `data-theme` attribute on <html>; every color in site.css is a custom
// property that flips off that attribute, so setting it is the entire
// theme switch.
const CarShellTheme = (() => {
    const STORAGE_KEY = 'carshell-theme';

    function apply(theme) {
        document.documentElement.setAttribute('data-theme', theme);
    }

    function current() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    // Called as early as possible in <head>, before the stylesheet paints,
    // so the page never flashes the wrong theme.
    function init(defaultTheme) {
        let theme = defaultTheme;
        try {
            theme = localStorage.getItem(STORAGE_KEY) || defaultTheme;
        } catch {
            // Private browsing, blocked storage, etc. — just use the default.
        }
        apply(theme);
        return theme;
    }

    function toggle() {
        const next = current() === 'dark' ? 'light' : 'dark';
        apply(next);
        try {
            localStorage.setItem(STORAGE_KEY, next);
        } catch {
            // The choice just will not persist across reloads.
        }
        return next;
    }

    return { init, toggle, current };
})();
