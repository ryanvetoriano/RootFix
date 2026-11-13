namespace RootFix.Domain.Entities;

/// <summary>
/// Apoio ("também sofri com isso") de um morador a um problema. Gravado pela API Java;
/// aqui só é contado, para ordenar o painel e calcular os indicadores.
/// </summary>
public class Apoio
{
    public long Id { get; private set; }

    public long ProblemaId { get; private set; }

    public long UsuarioId { get; private set; }

    public DateTime CriadoEm { get; private set; }

    private Apoio() { }

    public Apoio(long id, long problemaId, long usuarioId, DateTime criadoEm)
    {
        Id = id;
        ProblemaId = problemaId;
        UsuarioId = usuarioId;
        CriadoEm = criadoEm;
    }
}
