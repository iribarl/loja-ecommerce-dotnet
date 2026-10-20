// Progressive enhancement for the checkout: when the CEP has 8 digits, press the "Buscar CEP"
// button for the shopper. Without JavaScript the button still works by hand.
// Listening on the document keeps working after Blazor's enhanced navigation replaces the page.
document.addEventListener("input", event => {
    const input = event.target;
    if (!(input instanceof HTMLInputElement) || !input.dataset.cepLookup) {
        return;
    }

    const digits = input.value.replace(/\D/g, "");
    if (digits.length !== 8 || input.dataset.lastLookup === digits) {
        return;
    }

    input.dataset.lastLookup = digits;
    const button = document.querySelector(input.dataset.cepLookup);
    if (button && input.form) {
        input.form.requestSubmit(button);
    }
});
