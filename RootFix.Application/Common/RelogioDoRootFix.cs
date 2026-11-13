namespace RootFix.Application.Common;

/// <summary>
/// Relógio no fuso combinado pelo grupo: <b>horário de Brasília</b>.
/// </summary>
/// <remarks>
/// <para>As colunas de data do Oracle são <c>TIMESTAMP</c> sem fuso, gravadas pelas duas APIs. Para
/// que <c>ADOTADO_EM</c> (Java) e <c>DATA_HORA</c> (.NET) sejam comparáveis — o tempo médio de
/// resolução subtrai um do outro — e para que o <c>SELECT</c> mostre a hora que o morador viu,
/// todo mundo grava a hora de Brasília: o Java com <c>TZ=America/Sao_Paulo</c> no ambiente
/// (o <c>LocalDateTime.now()</c> segue o fuso da JVM) e o .NET com este relógio.</para>
/// <para>Uso: <c>GetLocalNow().DateTime</c> devolve a hora de Brasília, independentemente do fuso
/// da máquina (o container do Render roda em UTC).</para>
/// </remarks>
public sealed class RelogioDoRootFix : TimeProvider
{
    public const string FusoPadrao = "America/Sao_Paulo";

    private readonly TimeZoneInfo _fuso;

    public RelogioDoRootFix(TimeZoneInfo fuso)
    {
        _fuso = fuso;
    }

    public override TimeZoneInfo LocalTimeZone => _fuso;

    /// <summary>
    /// Fuso pelo ID IANA. Se o sistema não tiver a base de fusos (container enxuto), usa UTC−03:00
    /// fixo — equivalente para Brasília, que não tem horário de verão desde 2019.
    /// </summary>
    public static TimeZoneInfo ObterFuso(string? id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? FusoPadrao : id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "UTC-03 (Brasília)", TimeSpan.FromHours(-3), "Horário de Brasília", "Horário de Brasília");
        }
    }
}
