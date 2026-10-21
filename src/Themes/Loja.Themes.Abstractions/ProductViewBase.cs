using Loja.Cart.Application;
using Loja.Cart.Application.AddToCart;
using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.GetProductBySlug;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Loja.Themes;

/// <summary>
/// The logic of the product page (variant choice, stock, add to cart, which picture to show); each
/// theme writes only the markup with <c>@inherits ProductViewBase</c>. Rendered interactively
/// (Blazor Server): price and stock change with the shopper's choices without reloading.
/// </summary>
/// <remarks>
/// The markup should give the picture gallery <c>@ref="Gallery"</c> and the gallery attributes of
/// js/gallery.js, so picking a variant brings its picture into view.
/// </remarks>
public abstract class ProductViewBase : ComponentBase
{
    [Parameter, EditorRequired]
    public ProductPageResponse Page { get; set; } = default!;

    /// <summary>
    /// The visitor's cart, read from the cookie by the (static) page: this component runs over
    /// SignalR and has no access to cookies. Blazor encrypts the parameters of interactive components.
    /// </summary>
    [Parameter, EditorRequired]
    public CartOwner Owner { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    // Updates the static header's cart count (js/cart.js) and the gallery (js/gallery.js).
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>Set by the markup with @ref on the gallery element.</summary>
    protected ElementReference Gallery { get; set; }

    /// <summary>The shopper's choice per attribute, e.g. Tamanho → M.</summary>
    protected Dictionary<string, string> Selection { get; private set; } = [];

    /// <summary>The variant matching <see cref="Selection"/>; null when that combination does not exist.</summary>
    protected VariantDetails? Selected { get; private set; }

    /// <summary>Bound to the quantity selector.</summary>
    protected int Quantity { get; set; } = 1;

    protected bool Adding { get; private set; }

    protected string? AddError { get; private set; }

    /// <summary>Units in the cart after the last successful "add"; null before.</summary>
    protected int? CartUnits { get; private set; }

    protected ProductDetails Product => Page.Product;

    protected IReadOnlyList<VariantDetails> Variants => Page.Product.Variants;

    protected IReadOnlyList<ProductImageDetails> Images => Page.Product.Images;

    // While the selection matches no variant, keep showing the price of the first one.
    protected VariantDetails DisplayedVariant => Selected ?? Variants[0];

    protected bool CanAdd => Selected is { InStock: true };

    // When few units are left, do not offer more than there are.
    protected int MaxQuantity => Selected?.RemainingUnits ?? CartRules.MaxUnitsPerItem;

    protected string AltFor(ProductImageDetails image) => image.AppliesTo.Count == 0
        ? Product.Name
        : $"{Product.Name} - {string.Join(", ", image.AppliesTo.Values)}";

    protected bool IsSelected(string attribute, string value) => Selection.GetValueOrDefault(attribute) == value;

    // A value is available when, keeping the other choices, some variant with it is in stock.
    protected bool IsAvailable(string attribute, string value) =>
        Variants.Any(variant =>
            variant.InStock
            && variant.Attributes.TryGetValue(attribute, out var own) && own == value
            && Selection.Where(pair => pair.Key != attribute)
                .All(pair => variant.Attributes.TryGetValue(pair.Key, out var other) && other == pair.Value));

    protected override void OnParametersSet()
    {
        // Start with the first variant in stock, so the shopper sees something they can buy.
        var initial = Variants.FirstOrDefault(v => v.InStock) ?? Variants[0];
        Selection = new Dictionary<string, string>(initial.Attributes, StringComparer.Ordinal);
        Selected = initial;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The page is first rendered on the server, showing the first picture; once interactive,
        // bring the initial variant's picture into view.
        if (firstRender && Images.Count > 1)
        {
            await JS.InvokeVoidAsync("lojaGallery.show", Gallery, ImageIndexForSelection());
        }
    }

    protected async Task Select(string attribute, string value)
    {
        Selection[attribute] = value;
        Selected = Variants.FirstOrDefault(Matches);
        Quantity = 1;
        CartUnits = null;
        AddError = null;

        if (Images.Count > 1)
        {
            await JS.InvokeVoidAsync("lojaGallery.show", Gallery, ImageIndexForSelection());
        }
    }

    protected async Task AddToCartAsync()
    {
        if (Selected is null)
        {
            return;
        }

        Adding = true;
        AddError = null;

        // A fresh scope (and DbContext) per click: this component lives as long as the
        // SignalR connection, and a long-lived DbContext would keep tracking stale entities.
        await using var scope = ScopeFactory.CreateAsyncScope();
        var addToCart = scope.ServiceProvider.GetRequiredService<AddToCartHandler>();
        var result = await addToCart.HandleAsync(Owner, Selected.Id, Quantity, CancellationToken.None);

        if (result.IsSuccess)
        {
            CartUnits = result.Value;
            await JS.InvokeVoidAsync("lojaCart.setUnits", result.Value);
        }
        else
        {
            AddError = result.Error.Message;
            CartUnits = null;
        }

        Adding = false;
    }

    // The picture of the selected variant (e.g. the black one when "Preto" is picked).
    private int ImageIndexForSelection() =>
        Page.ImageFor(Selection) is { } image ? Math.Max(0, Images.ToList().IndexOf(image)) : 0;

    private bool Matches(VariantDetails variant) =>
        variant.Attributes.Count == Selection.Count
        && Selection.All(pair => variant.Attributes.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
