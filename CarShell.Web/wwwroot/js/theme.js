// Shared light/dark toggle for both the public site and the admin area.
// Each area keeps its own stored preference (and its own default) since
// they are independent surfaces with different brand accents, but both
// read/write the same way through this module. Applying the theme
// happens via a `data-theme` attribute on <html>; every color in
// site.css is a custom property that flips off that attribute, so
// setting it is the entire theme switch.
const CarShellTheme = (() => {
    function apply(theme) {
        document.documentElement.setAttribute('data-theme', theme);
    }

    function current() {
        return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    // Called as early as possible in <head>, before the stylesheet paints,
    // so the page never flashes the wrong theme.
    function init(storageKey, defaultTheme) {
        let theme = defaultTheme;
        try {
            theme = localStorage.getItem(storageKey) || defaultTheme;
        } catch {
            // Private browsing, blocked storage, etc. — just use the default.
        }
        apply(theme);
        return theme;
    }

    function toggle(storageKey) {
        const next = current() === 'dark' ? 'light' : 'dark';
        apply(next);
        try {
            localStorage.setItem(storageKey, next);
        } catch {
            // The choice just will not persist across reloads.
        }
        return next;
    }

    return { init, toggle, current };
})();
