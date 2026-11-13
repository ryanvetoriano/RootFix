namespace RootFix.Domain.Entities;

/// <summary>
/// Categoria do problema (buraco, iluminação, lixo...). Carregada pelo DDL e sugerida pelo
/// Spring AI na publicação; aqui é somente leitura, para o painel exibir o nome.
/// </summary>
public class Categoria
{
    public long Id { get; private set; }

    public string Nome { get; private set; } = null!;

    private Categoria() { }

    public Categoria(long id, string nome)
    {
        Id = id;
        Nome = nome;
    }
}
