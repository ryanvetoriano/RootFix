namespace RootFix.Domain.Entities;

/// <summary>
/// Comunidade criada por um gestor. Mantida pela API Java; aqui é somente leitura.
/// </summary>
public class Comunidade
{
    public long Id { get; private set; }

    public string Nome { get; private set; } = null!;

    public string? Descricao { get; private set; }

    public long GestorId { get; private set; }

    public Usuario? Gestor { get; private set; }

    private Comunidade() { }

    public Comunidade(long id, string nome, long gestorId, string? descricao = null)
    {
        Id = id;
        Nome = nome;
        GestorId = gestorId;
        Descricao = descricao;
    }

    /// <summary>Só o gestor que criou a comunidade administra o painel dela.</summary>
    public bool EhGerenciadaPor(long usuarioId) => GestorId == usuarioId;
}
