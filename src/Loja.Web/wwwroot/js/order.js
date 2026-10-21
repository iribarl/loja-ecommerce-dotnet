// Order page helpers:
// - "Copiar" copies the Pix "copia e cola" to the clipboard.
// - While the order waits for payment, ask its status every 3 seconds and reload when it changes
//   (e.g. the payment was confirmed). Listening on the document survives Blazor's enhanced navigation.
document.addEventListener("click", async event => {
    const button = event.target instanceof Element ? event.target.closest("[data-copy-target]") : null;
    if (!button) {
        return;
    }

    const source = document.querySelector(button.dataset.copyTarget);
    if (!source) {
        return;
    }

    try {
        await navigator.clipboard.writeText(source.value ?? source.textContent);
        button.textContent = "Copiado!";
    } catch {
        source.select?.();
        button.textContent = "Selecione e copie";
    }
});

let pollTimer = null;

function startPolling() {
    clearInterval(pollTimer);
    const watched = document.querySelector("[data-order-status-url]");
    if (!watched) {
        return;
    }

    const url = watched.dataset.orderStatusUrl;
    const current = watched.dataset.orderStatus;
    pollTimer = setInterval(async () => {
        try {
            const response = await fetch(url, { headers: { Accept: "application/json" } });
            if (!response.ok) {
                return;
            }

            const { status } = await response.json();
            if (status !== current) {
                clearInterval(pollTimer);
                location.reload();
            }
        } catch {
            // Offline for a moment: try again on the next tick.
        }
    }, 3000);
}

// blazor.web.js loads after this script, so Blazor exists only once the page has loaded.
// Its enhanced navigation swaps the page without reloading scripts: start again after each one.
document.addEventListener("DOMContentLoaded", () => {
    startPolling();
    window.Blazor?.addEventListener("enhancedload", startPolling);
});
