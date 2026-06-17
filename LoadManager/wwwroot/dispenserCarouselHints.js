window.uaacDispensersCarousel = (() => {
    const tolerance = 4;

    function cleanupOf(element) {
        return element?._uaacDispensersCleanup;
    }

    function setCleanup(element, cleanup) {
        element._uaacDispensersCleanup = cleanup;
    }

    function sync(element, dotNetRef) {
        if (!element || !dotNetRef) {
            return;
        }

        const maxScrollLeft = Math.max(0, element.scrollWidth - element.clientWidth);
        const canScrollLeft = element.scrollLeft > tolerance;
        const canScrollRight = element.scrollLeft < maxScrollLeft - tolerance;

        dotNetRef.invokeMethodAsync("UpdateDispenserScrollHints", canScrollLeft, canScrollRight);
    }

    function dispose(element) {
        const cleanup = cleanupOf(element);
        if (cleanup) {
            cleanup();
            setCleanup(element, null);
        }
    }

    function init(element, dotNetRef) {
        if (!element || !dotNetRef) {
            return;
        }

        dispose(element);

        let frameRequested = false;

        const requestSync = () => {
            if (frameRequested) {
                return;
            }

            frameRequested = true;
            window.requestAnimationFrame(() => {
                frameRequested = false;
                sync(element, dotNetRef);
            });
        };

        const onScroll = () => requestSync();
        const onResize = () => requestSync();

        element.addEventListener("scroll", onScroll, { passive: true });
        window.addEventListener("resize", onResize, { passive: true });

        const resizeObserver = typeof ResizeObserver === "function"
            ? new ResizeObserver(() => requestSync())
            : null;

        resizeObserver?.observe(element);

        setCleanup(element, () => {
            element.removeEventListener("scroll", onScroll);
            window.removeEventListener("resize", onResize);
            resizeObserver?.disconnect();
        });

        requestSync();
    }

    function scrollByPage(element, direction) {
        if (!element) {
            return;
        }

        const delta = Math.max(120, Math.floor(element.clientWidth * 0.75)) * direction;
        element.scrollBy({
            left: delta,
            behavior: "smooth"
        });
    }

    return {
        dispose,
        init,
        scrollByPage
    };
})();
