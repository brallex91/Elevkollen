// Små globala hjälpare. Håll den här filen tunn — logiken hör hemma i C#.
window.appPrint = () => window.print();

// Guiden behöver veta var elementet den pratar om ligger, så att resten kan suddas.
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
