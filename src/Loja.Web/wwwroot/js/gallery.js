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
        gallery.querySelectorAll("[data-gallery-position]").forEach(position =>
            position.textContent = String(index + 1));
    }

    // Full-screen view: a gallery with [data-lightbox="dialog-id"] opens that <dialog> on the
    // clicked picture; closing it leaves the page gallery on the last picture seen.
    function openLightbox(gallery, index) {
        const dialog = document.getElementById(gallery.dataset.lightbox);
        const inner = dialog?.querySelector("[data-gallery]");
        if (!inner) {
            return;
        }

        dialog.returnGallery = gallery;
        dialog.showModal();
        // After showModal the dialog has its size, so the track can scroll to the picture.
        requestAnimationFrame(() => show(inner, index, false));
    }

    function closeLightbox(dialog) {
        const inner = dialog.querySelector("[data-gallery]");
        if (dialog.returnGallery && inner) {
            show(dialog.returnGallery, currentIndex(inner), false);
        }

        dialog.close();
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
        const picture = event.target.closest("[data-lightbox] [data-lightbox-open]");
        if (picture) {
            const gallery = picture.closest("[data-lightbox]");
            openLightbox(gallery, [...trackOf(gallery).children].indexOf(picture));
            return;
        }

        // The × button, or a click on the dark area around the picture.
        const dialog = event.target.closest("dialog.lightbox");
        if (dialog && (event.target.closest("[data-lightbox-close]") || event.target === dialog
            || event.target.matches("[data-gallery-track]"))) {
            closeLightbox(dialog);
            return;
        }

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
        // In the full-screen view the arrows work wherever the focus is (e.g. on the × button).
        const dialog = event.target.closest?.("dialog.lightbox[open]");
        if (dialog && (event.key === "ArrowLeft" || event.key === "ArrowRight")) {
            event.preventDefault();
            const inner = dialog.querySelector("[data-gallery]");
            show(inner, currentIndex(inner) + (event.key === "ArrowRight" ? 1 : -1), true);
            return;
        }

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
