namespace RootFix.Domain.Entities;

/// <summary>
/// Vínculo entre morador e comunidade. Gravado pela API Java quando o morador entra; usado
/// aqui para saber quem pode acompanhar os problemas de uma comunidade.
/// </summary>
public class Participacao
{
    public long Id { get; private set; }

    public long UsuarioId { get; private set; }

    public long ComunidadeId { get; private set; }

    public DateTime EntrouEm { get; private set; }

    private Participacao() { }

    public Participacao(long id, long usuarioId, long comunidadeId, DateTime entrouEm)
    {
        Id = id;
        UsuarioId = usuarioId;
        ComunidadeId = comunidadeId;
        EntrouEm = entrouEm;
    }
}
