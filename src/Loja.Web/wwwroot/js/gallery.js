// Product galleries: a horizontal track of pictures with scroll-snap, so swiping works without
// this script. This adds the previous/next buttons, thumbnails, dots and arrow keys.
//
// Markup: [data-gallery] holding a [data-gallery-track] (the pictures), and optionally
// [data-gallery-prev], [data-gallery-next], thumbnails [data-gallery-go="i"] and dots [data-gallery-dot="i"].
// Listeners sit on the document (event delegation), so they keep working after enhanced
// navigation and when Blazor re-renders the product page.
window.lojaGallery = (() => {
    const trackOf = gallery => gallery.querySelector("[data-gallery-track]");
    const reduceMotion = () => matchMedia("(prefers-reduced-motion: reduce)").matches;

    const currentIndex = gallery => {
        const track = trackOf(gallery);
        return track.clientWidth ? Math.round(track.scrollLeft / track.clientWidth) : 0;
    };

    function mark(gallery, index) {
        gallery.querySelectorAll("[data-gallery-go]").forEach(thumb =>
            thumb.setAttribute("aria-current", Number(thumb.dataset.galleryGo) === index ? "true" : "false"));
        gallery.querySelectorAll("[data-gallery-dot]").forEach(dot =>
            dot.toggleAttribute("data-active", Number(dot.dataset.galleryDot) === index));
    }

    function show(gallery, index, smooth) {
        const track = trackOf(gallery);
        const count = track?.children.length ?? 0;
        if (count === 0) {
            return;
        }

        const target = (index + count) % count; // past the last comes the first, and back
        track.scrollTo({ left: target * track.clientWidth, behavior: smooth && !reduceMotion() ? "smooth" : "instant" });
        mark(gallery, target);
    }

    document.addEventListener("click", event => {
        const control = event.target.closest("[data-gallery-prev], [data-gallery-next], [data-gallery-go]");
        const gallery = control?.closest("[data-gallery]");
        if (!gallery) {
            return;
        }

        event.preventDefault();
        if (control.hasAttribute("data-gallery-go")) {
            show(gallery, Number(control.dataset.galleryGo), true);
        } else {
            show(gallery, currentIndex(gallery) + (control.hasAttribute("data-gallery-next") ? 1 : -1), true);
        }
    });

    document.addEventListener("keydown", event => {
        const track = event.target.closest?.("[data-gallery-track]");
        if (!track || (event.key !== "ArrowLeft" && event.key !== "ArrowRight")) {
            return;
        }

        event.preventDefault();
        const gallery = track.closest("[data-gallery]");
        show(gallery, currentIndex(gallery) + (event.key === "ArrowRight" ? 1 : -1), true);
    });

    // Swiping moves the track by itself: keep thumbnails and dots in step. Scroll events do not
    // bubble, so the listener uses the capture phase.
    document.addEventListener("scroll", event => {
        const track = event.target;
        if (track instanceof Element && track.matches("[data-gallery-track]")) {
            const gallery = track.closest("[data-gallery]");
            mark(gallery, currentIndex(gallery));
        }
    }, { capture: true, passive: true });

    // Called by the product page when the shopper picks a variant with its own picture.
    return { show: (gallery, index) => show(gallery, index, true) };
})();
