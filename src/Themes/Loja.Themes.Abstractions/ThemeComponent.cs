using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Loja.Themes;

/// <summary>
/// Renders the component a theme chose for a part of the store, optionally interactive.
/// </summary>
/// <remarks>
/// &lt;DynamicComponent&gt; cannot be interactive: the parameters of an interactive component must
/// be serializable and a <see cref="Type"/> is not. This builds the render tree by hand instead,
/// so the theme decides which component and the page decides how it renders.
/// </remarks>
public sealed class ThemeComponent : ComponentBase
{
    [Parameter, EditorRequired]
    public Type Type { get; set; } = default!;

    [Parameter]
    public IReadOnlyDictionary<string, object?>? Parameters { get; set; }

    /// <summary>E.g. InteractiveServer for the product view; null renders it statically like the page.</summary>
    [Parameter]
    public IComponentRenderMode? RenderMode { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent(0, Type);
        foreach (var (name, value) in Parameters ?? new Dictionary<string, object?>())
        {
            builder.AddComponentParameter(1, name, value);
        }

        if (RenderMode is not null)
        {
            builder.AddComponentRenderMode(RenderMode);
        }

        builder.CloseComponent();
    }
}
