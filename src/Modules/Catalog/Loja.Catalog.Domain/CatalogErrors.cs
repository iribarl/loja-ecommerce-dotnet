using Loja.SharedKernel;

namespace Loja.Catalog.Domain;

// Messages may reach the store or the admin panel, so they are in Brazilian Portuguese.
public static class CatalogErrors
{
    public static readonly Error InvalidSlug =
        Error.Validation("Catalog.InvalidSlug", "O identificador deve conter letras ou números.");

    public static readonly Error NameRequired =
        Error.Validation("Catalog.NameRequired", "O nome é obrigatório.");

    public static readonly Error InvalidSku =
        Error.Validation("Catalog.InvalidSku", "O SKU deve ter até 64 caracteres: letras maiúsculas, números e hífens.");

    public static readonly Error InvalidAttribute =
        Error.Validation("Catalog.InvalidAttribute", "Os atributos da variante não podem ter nome ou valor vazio.");

    public static readonly Error InvalidPrice =
        Error.Validation("Catalog.InvalidPrice", "O preço deve ser maior que zero.");

    public static readonly Error NegativeStock =
        Error.Validation("Catalog.NegativeStock", "O estoque não pode ser negativo.");

    public static readonly Error InvalidImageUrl =
        Error.Validation("Catalog.InvalidImageUrl", "A URL da imagem é inválida.");

    public static readonly Error DuplicateSku =
        Error.Conflict("Catalog.DuplicateSku", "Já existe uma variante com este SKU.");

    public static readonly Error DuplicateAttributes =
        Error.Conflict("Catalog.DuplicateAttributes", "Já existe uma variante com estes atributos.");

    public static readonly Error VariantNotFound =
        Error.NotFound("Catalog.VariantNotFound", "Variante não encontrada.");

    public static readonly Error ProductNotFound =
        Error.NotFound("Catalog.ProductNotFound", "Produto não encontrado.");

    public static readonly Error CategoryNotFound =
        Error.Validation("Catalog.CategoryNotFound", "Escolha uma categoria válida.");

    public static readonly Error SkuInUse =
        Error.Conflict("Catalog.SkuInUse", "Este SKU já é usado por outro produto.");

    public static readonly Error ZeroAdjustment =
        Error.Validation("Catalog.ZeroAdjustment", "Informe quantas unidades entram (+) ou saem (-).");

    public static readonly Error AdjustmentTooLarge =
        Error.Validation("Catalog.AdjustmentTooLarge", "Ajuste grande demais. Confira a quantidade.");

    public static readonly Error AdjustmentReasonRequired =
        Error.Validation("Catalog.AdjustmentReasonRequired", "Informe o motivo do ajuste (até 200 caracteres).");

    public static Error StockWouldBeNegative(int stock) =>
        Error.Conflict("Catalog.StockWouldBeNegative", $"O estoque ficaria negativo: há {stock} unidade(s) disponível(is).");

    public static readonly Error PublishWithoutVariants =
        Error.Validation("Catalog.PublishWithoutVariants", "Um produto precisa de ao menos uma variante para ser publicado.");
}
