using Loja.SharedKernel;

namespace Loja.Catalog.Domain.Categories;

public readonly record struct CategoryId(Guid Value)
{
    public static CategoryId New() => new(Guid.CreateVersion7());
}

public sealed class Category : Entity<CategoryId>
{
    private Category(CategoryId id, string name, Slug slug)
        : base(id)
    {
        Name = name;
        Slug = slug;
    }

    public string Name { get; private set; }

    public Slug Slug { get; private set; }

    public static Result<Category> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CatalogErrors.NameRequired;
        }

        var slug = Slug.From(name);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        return new Category(CategoryId.New(), name.Trim(), slug.Value);
    }
}
