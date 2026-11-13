namespace RootFix.Infrastructure.Persistence;

/// <summary>
/// Nomes das tabelas do Oracle, em um único lugar. Seguem o DDL oficial do grupo
/// (<c>scripts/script-bd.sql</c>): prefixo <c>RF_</c> nas tabelas e <c>ID_</c> nas chaves
/// estrangeiras (<c>ID_AUTOR</c>, <c>ID_COMUNIDADE</c>...).
/// </summary>
/// <remarks>
/// O EF Core <b>não cria nem altera</b> essas tabelas: elas são do DDL compartilhado com a
/// API Java (o mesmo <c>script-bd.sql</c> usado pelo DevOps). Por isso não há migrations.
/// </remarks>
public static class NomesDoBanco
{
    public const string Usuario = "RF_USUARIO";
    public const string Comunidade = "RF_COMUNIDADE";
    public const string Participacao = "RF_PARTICIPACAO";
    public const string Categoria = "RF_CATEGORIA";
    public const string Problema = "RF_PROBLEMA";
    public const string Apoio = "RF_APOIO";
    public const string HistoricoStatus = "RF_HISTORICO_STATUS";

    /// <summary>Usada só pela API .NET, para as chaves de idempotência.</summary>
    public const string Idempotencia = "RF_IDEMPOTENCIA";
}
