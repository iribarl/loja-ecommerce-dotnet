namespace Loja.Catalog.Infrastructure.Persistence.Seed;

/// <summary>
/// Fictitious clothing store used by the demo. Deterministic: the same data on every run.
/// </summary>
internal static class CatalogSeedData
{
    public static readonly string[] Categories = ["Camisetas", "Calças e Bermudas", "Casacos e Moletons", "Vestidos e Saias", "Acessórios"];

    private static readonly string[] Clothing = ["P", "M", "G", "GG"];
    private static readonly string[] Pants = ["38", "40", "42", "44"];
    private static readonly string[] Socks = ["34-38", "39-43"];
    private static readonly string[] Belts = ["90", "100", "110"];
    private static readonly string[] OneSize = [];

    public static readonly ProductSeed[] Products =
    [
        new("Camisetas", "Camiseta Básica Algodão", "CAM-BAS", 49.90m, Clothing, ["Branco", "Preto", "Azul Marinho"],
            "Camiseta de algodão penteado, gola careca e caimento reto. A peça coringa do dia a dia."),
        new("Camisetas", "Camiseta Estampada Tropical", "CAM-TRO", 69.90m, Clothing, ["Branco", "Verde"],
            "Estampa de folhagens tropicais em silk. Algodão leve, ideal para o verão."),
        new("Camisetas", "Camisa Polo Piquet", "POL-PIQ", 89.90m, Clothing, ["Azul", "Preto", "Vermelho"],
            "Polo em malha piquet com botões e punhos canelados. Casual e elegante."),
        new("Camisetas", "Camiseta Manga Longa", "CAM-MLO", 79.90m, Clothing, ["Cinza", "Preto"],
            "Manga longa em algodão com elastano. Confortável para os dias mais frescos."),
        new("Camisetas", "Regata Dry Fit", "REG-DRY", 39.90m, Clothing, ["Preto", "Azul"],
            "Regata esportiva em tecido de secagem rápida. Para treinar ou correr."),

        new("Calças e Bermudas", "Calça Jeans Slim", "CAL-JSL", 159.90m, Pants, ["Azul Escuro", "Azul Claro"],
            "Jeans com elastano, modelagem slim e cinco bolsos."),
        new("Calças e Bermudas", "Calça Chino", "CAL-CHI", 139.90m, Pants, ["Bege", "Azul Marinho"],
            "Sarja leve com bolsos faca. Do escritório ao fim de semana."),
        new("Calças e Bermudas", "Bermuda Jeans", "BER-JEA", 99.90m, Pants, ["Azul"],
            "Bermuda jeans com barra dobrada e lavagem média."),
        new("Calças e Bermudas", "Calça Jogger Moletom", "CAL-JOG", 119.90m, Clothing, ["Cinza", "Preto"],
            "Jogger em moletom flanelado, cós com elástico e cordão."),
        new("Calças e Bermudas", "Short de Linho", "SHO-LIN", 89.90m, Clothing, ["Bege", "Branco"],
            "Short em linho misto, fresco e com caimento soltinho."),

        new("Casacos e Moletons", "Moletom Canguru com Capuz", "MOL-CAN", 189.90m, Clothing, ["Cinza Mescla", "Preto", "Verde Musgo"],
            "Moletom flanelado com capuz e bolso canguru. Quentinho e macio."),
        new("Casacos e Moletons", "Jaqueta Corta-Vento", "JAQ-CVE", 229.90m, Clothing, ["Preto", "Amarelo"],
            "Jaqueta leve e impermeável, com capuz embutido na gola."),
        new("Casacos e Moletons", "Cardigan de Tricô", "CAR-TRI", 169.90m, Clothing, ["Bege", "Vinho"],
            "Cardigan em tricô com botões de madeira."),
        new("Casacos e Moletons", "Jaqueta Jeans", "JAQ-JEA", 249.90m, Clothing, ["Azul"],
            "Clássica jaqueta jeans com bolsos frontais e lavagem estonada."),

        new("Vestidos e Saias", "Vestido Midi Floral", "VES-MFL", 179.90m, Clothing, ["Azul", "Rosa"],
            "Vestido midi em viscose com estampa floral e alças reguláveis."),
        new("Vestidos e Saias", "Vestido Tubinho", "VES-TUB", 149.90m, Clothing, ["Preto", "Vermelho"],
            "Tubinho em malha encorpada com elastano. Versátil para qualquer ocasião."),
        new("Vestidos e Saias", "Saia Plissada", "SAI-PLI", 119.90m, Clothing, ["Preto", "Verde"],
            "Saia midi plissada com cós elástico."),
        new("Vestidos e Saias", "Macacão de Linho", "MAC-LIN", 219.90m, Clothing, ["Bege"],
            "Macacão pantalona em linho com faixa na cintura."),

        new("Acessórios", "Boné Aba Curva", "BON-ACU", 59.90m, OneSize, ["Preto", "Branco"],
            "Boné de sarja com fecho ajustável em metal."),
        new("Acessórios", "Kit 3 Meias Cano Alto", "MEI-KIT", 34.90m, Socks, ["Branco", "Preto"],
            "Kit com três pares de meias de algodão com punho reforçado."),
        new("Acessórios", "Cinto de Couro", "CIN-COU", 79.90m, Belts, ["Marrom", "Preto"],
            "Cinto em couro legítimo com fivela de metal escovado."),

        // Draft: exists in the database but must never appear in the store.
        new("Camisetas", "Camiseta Edição Limitada", "CAM-LIM", 99.90m, Clothing, ["Preto"],
            "Lançamento em breve.", Published: false),
    ];
}

/// <param name="Code">Prefix of the SKUs, e.g. "CAM-BAS" → "CAM-BAS-PRETO-M".</param>
/// <param name="Sizes">Empty when the product comes in a single size.</param>
internal sealed record ProductSeed(
    string Category,
    string Name,
    string Code,
    decimal Price,
    string[] Sizes,
    string[] Colors,
    string Description,
    bool Published = true);
