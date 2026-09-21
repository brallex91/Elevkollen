// Small global helpers. Keep this file thin — the logic belongs in C#.
window.appPrint = () => window.print();

// The tour needs to know where its target sits, so everything else can be blurred.
window.tourRect = (selector) => {
    const el = selector && document.querySelector(selector);
    if (!el) {
        return null;
    }

    const r = el.getBoundingClientRect();
    return r.width && r.height
        ? {
            top: r.top,
            left: r.left,
            width: r.width,
            height: r.height,
            viewportWidth: window.innerWidth,
            viewportHeight: window.innerHeight,
        }
        : null;
};
