// Browser-only functionality used by Blazor through JS interop.
// Application logic lives in C#; this file only touches APIs .NET cannot reach directly.
(function () {
    "use strict";

    const storage = {
        get(key) {
            try { return window.localStorage.getItem(key); }
            catch (e) { console.warn("Tracker: localStorage unavailable", e); return null; }
        },
        set(key, value) {
            try { window.localStorage.setItem(key, value); return true; }
            catch (e) { console.warn("Tracker: could not save to localStorage", e); return false; }
        },
        remove(key) {
            try { window.localStorage.removeItem(key); } catch { /* ignore */ }
        }
    };

    const darkQuery = window.matchMedia("(prefers-color-scheme: dark)");

    const theme = {
        apply(mode) {
            const root = document.documentElement;
            if (mode === "light" || mode === "dark") root.dataset.theme = mode;
            else delete root.dataset.theme;
            theme.syncMeta();
        },
        effective() {
            const explicit = document.documentElement.dataset.theme;
            if (explicit === "light" || explicit === "dark") return explicit;
            return darkQuery.matches ? "dark" : "light";
        },
        syncMeta() {
            const meta = document.querySelector('meta[name="theme-color"]');
            if (meta) meta.setAttribute("content", theme.effective() === "dark" ? "#060b16" : "#eef3fa");
        }
    };

    darkQuery.addEventListener("change", theme.syncMeta);

    // Notifications API. Only works while the page is open; there is no service worker,
    // so nothing can fire when the browser is closed.
    const notify = {
        permission() {
            return "Notification" in window ? Notification.permission : "unsupported";
        },
        async request() {
            if (!("Notification" in window)) return "unsupported";
            try { return await Notification.requestPermission(); }
            catch { return Notification.permission; }
        },
        show(title, body) {
            if (!("Notification" in window) || Notification.permission !== "granted") return false;
            try {
                // Same tag: a new reminder replaces an old one instead of stacking (also dedupes multiple tabs).
                const n = new Notification(title, { body, tag: "tracker-water-reminder", renotify: true });
                n.onclick = () => { window.focus(); n.close(); };
                return true;
            } catch (e) {
                // e.g. Chrome on Android only allows notifications from a service worker.
                console.warn("Tracker: could not show notification", e);
                return false;
            }
        }
    };

    window.tracker = { storage, theme, notify };
})();
