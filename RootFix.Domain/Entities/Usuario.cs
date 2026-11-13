using RootFix.Domain.Enums;

namespace RootFix.Domain.Entities;

/// <summary>
/// Usuário do RootFix. Cadastrado e mantido pela API Java; a API .NET apenas lê, para saber
/// quem é o gestor de cada comunidade e exibir nomes no painel.
/// </summary>
public class Usuario
{
    public long Id { get; private set; }

    public string Nome { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public Perfil Perfil { get; private set; }

    /// <summary>Organização do gestor (ex.: "Associação de Moradores da Vila X").</summary>
    public string? Organizacao { get; private set; }

    private Usuario() { }

    /// <summary>Usado pelos testes e pela carga de dados; em produção quem cria é o Java.</summary>
    public Usuario(long id, string nome, string email, Perfil perfil, string? organizacao = null)
    {
        Id = id;
        Nome = nome;
        Email = email;
        Perfil = perfil;
        Organizacao = organizacao;
    }
}
