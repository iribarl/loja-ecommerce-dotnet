using Loja.Catalog.Application.Categories;
using Loja.Catalog.Application.Categories.ListCategories;
using Loja.Catalog.Application.Products;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace Loja.Themes;

/// <summary>Base of a theme's home page content: the categories are loaded; the theme writes the markup.</summary>
public abstract class HomeContentBase : ComponentBase
{
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    [Inject]
    private ListCategoriesHandler ListCategories { get; set; } = default!;

    protected IReadOnlyList<CategorySummary> Categories { get; private set; } = [];

    protected override async Task OnInitializedAsync() =>
        Categories = await ListCategories.HandleAsync(HttpContext?.RequestAborted ?? default);
}

/// <summary>
/// Base of a theme's product card in listings. The markup must keep the gallery attributes
/// (data-gallery, data-gallery-track…) if it offers several pictures: js/gallery.js relies on them.
/// </summary>
public abstract class ProductCardBase : ComponentBase
{
    [Parameter, EditorRequired]
    public ProductSummary Product { get; set; } = default!;

    protected string ProductUrl => $"/produtos/{Product.Slug}";
}
