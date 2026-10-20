// Updates the cart count in the header without reloading the page.
// The header is rendered on the server (static), so interactive components that change the cart
// call this through JS interop instead of making the whole header interactive.
window.lojaCart = {
    setUnits(units) {
        const badge = document.querySelector("[data-cart-count]");
        if (!badge) {
            return;
        }

        badge.textContent = units;
        badge.hidden = units === 0;
        badge.setAttribute("aria-label", `${units} ${units === 1 ? "item" : "itens"} no carrinho`);
    }
};
