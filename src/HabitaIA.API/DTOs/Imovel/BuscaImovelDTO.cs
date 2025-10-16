namespace HabitaIA.API.DTOs.Imovel
{
    public record BuscaImovelDTO(
    string consulta,
    decimal? precoMaximo,
    int? quartosMinimos,
    string? bairro,
    int limite = 20
);

    public record ImovelRespostaDTO(
        Guid id, string titulo, string bairro, string cidade, string uf,
        int quartos, int banheiros, decimal preco, double area, double similaridade, double score
    );

    public record CriarImovelDTO(
        Guid tenantId,
        string titulo,
        string descricao,
        string bairro,
        string cidade,
        string uf,
        int quartos,
        int banheiros,
        decimal preco,
        double area
    );
}
