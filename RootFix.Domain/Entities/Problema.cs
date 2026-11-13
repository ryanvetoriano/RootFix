using RootFix.Domain.Enums;
using RootFix.Domain.Exceptions;
using RootFix.Domain.Rules;

namespace RootFix.Domain.Entities;

/// <summary>
/// Problema publicado por um morador no feed de uma comunidade.
/// </summary>
/// <remarks>
/// A divisão entre as APIs segue o plano de ação: a API Java cuida de tudo até a adoção
/// (publicar, apoiar, comentar, adotar); a API .NET cuida de tudo depois dela (status,
/// prioridade e indicadores). Por isso as únicas escritas feitas aqui são
/// <see cref="AlterarStatus"/> e <see cref="DefinirPrioridade"/>.
/// </remarks>
public class Problema
{
    public long Id { get; private set; }

    public long ComunidadeId { get; private set; }

    public Comunidade? Comunidade { get; private set; }

    public long AutorId { get; private set; }

    public Usuario? Autor { get; private set; }

    public string Titulo { get; private set; } = null!;

    public string? Descricao { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public bool Adotado { get; private set; }

    public DateTime? AdotadoEm { get; private set; }

    /// <summary>
    /// Status gravado no banco. Fica nulo enquanto o problema não é adotado.
    /// </summary>
    public StatusProblema? Status { get; private set; }

    public Prioridade? Prioridade { get; private set; }

    public long? CategoriaId { get; private set; }

    public Categoria? Categoria { get; private set; }

    /// <summary>Gravidade sugerida pelo Spring AI na publicação: BAIXA, MEDIA, ALTA ou CRITICA.</summary>
    public string? GravidadeIa { get; private set; }

    /// <summary>Explicação curta do Spring AI para a gravidade sugerida.</summary>
    public string? JustificativaIa { get; private set; }

    public DateTime CriadoEm { get; private set; }

    private Problema() { }

    public Problema(
        long id,
        long comunidadeId,
        long autorId,
        string titulo,
        DateTime criadoEm,
        string? descricao = null,
        string? gravidadeIa = null,
        double? latitude = null,
        double? longitude = null,
        long? categoriaId = null,
        string? justificativaIa = null)
    {
        Id = id;
        ComunidadeId = comunidadeId;
        AutorId = autorId;
        Titulo = titulo;
        CriadoEm = criadoEm;
        Descricao = descricao;
        GravidadeIa = gravidadeIa;
        Latitude = latitude;
        Longitude = longitude;
        CategoriaId = categoriaId;
        JustificativaIa = justificativaIa;
    }

    /// <summary>
    /// Status efetivo de um problema adotado. O DDL (CK_RF_PROBLEMA_ADOCAO) já obriga o Java a
    /// gravar o status na adoção; o fallback para PENDENTE fica só como proteção.
    /// </summary>
    public StatusProblema? StatusAtual => Adotado ? Status ?? StatusProblema.PENDENTE : null;

    /// <summary>
    /// Marca o problema como adotado. Em produção quem faz isso é a API Java; o método existe
    /// para os testes e para a carga de dados.
    /// </summary>
    public void MarcarComoAdotado(DateTime adotadoEm)
    {
        Adotado = true;
        AdotadoEm = adotadoEm;
        Status ??= StatusProblema.PENDENTE;
    }

    /// <summary>
    /// Muda o status do problema e devolve o registro de histórico correspondente.
    /// </summary>
    /// <exception cref="DomainException">Problema não adotado ou transição não permitida.</exception>
    /// <exception cref="ConflitoException">O problema já está no status pedido.</exception>
    public HistoricoStatus AlterarStatus(
        StatusProblema novoStatus,
        long gestorId,
        DateTime agora,
        string? observacao = null)
    {
        GarantirAdotado();

        var atual = StatusAtual!.Value;

        if (atual == novoStatus)
            throw new ConflitoException($"O problema já está com o status {novoStatus}.");

        if (!TransicaoDeStatus.EhPermitida(atual, novoStatus))
        {
            var possiveis = TransicaoDeStatus.ProximosPossiveis(atual);
            var detalhe = possiveis.Count == 0
                ? $"{atual} é um status final."
                : $"A partir de {atual} são permitidos: {string.Join(", ", possiveis)}.";

            throw new DomainException(
                $"Não é permitido mudar o status de {atual} para {novoStatus}. {detalhe}");
        }

        // O histórico guarda o que estava gravado no banco (pode ser nulo), não o efetivo.
        var historico = new HistoricoStatus(Id, Status, novoStatus, agora, gestorId, observacao);

        Status = novoStatus;

        return historico;
    }

    /// <summary>Define (ou troca) a prioridade de um problema supervisionado.</summary>
    /// <exception cref="DomainException">Problema não adotado ou já encerrado.</exception>
    public void DefinirPrioridade(Prioridade prioridade)
    {
        GarantirAdotado();

        if (TransicaoDeStatus.EhFinal(StatusAtual!.Value))
        {
            throw new DomainException(
                $"Não é possível mudar a prioridade de um problema {StatusAtual}.");
        }

        Prioridade = prioridade;
    }

    private void GarantirAdotado()
    {
        if (!Adotado)
        {
            throw new DomainException(
                "O problema ainda não foi adotado. A adoção é feita pelo gestor no feed (API Java).");
        }
    }
}
