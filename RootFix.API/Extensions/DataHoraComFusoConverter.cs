using System.Text.Json;
using System.Text.Json.Serialization;

namespace RootFix.API.Extensions;

/// <summary>
/// Escreve as datas da API com o fuso explícito: <c>2026-11-13T15:00:00-03:00</c>.
/// </summary>
/// <remarks>
/// <para>As datas lidas do Oracle chegam sem fuso (<c>TIMESTAMP</c>); pelo combinado do grupo,
/// elas estão em horário de Brasília. Sem o offset no JSON, cada cliente adivinharia o fuso —
/// é o que faz a tela do Java mostrar a hora errada quando o servidor roda em UTC.</para>
/// <para>Datas em UTC (<see cref="DateTimeKind.Utc"/>) são convertidas para o fuso antes de sair.
/// Na leitura, qualquer ISO 8601 é aceito e convertido para a hora de Brasília.</para>
/// </remarks>
public sealed class DataHoraComFusoConverter : JsonConverter<DateTime>
{
    private readonly TimeZoneInfo _fuso;

    public DataHoraComFusoConverter(TimeZoneInfo fuso)
    {
        _fuso = fuso;
    }

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var valor = reader.GetDateTimeOffset();
        return TimeZoneInfo.ConvertTime(valor, _fuso).DateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var local = value.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(value, _fuso)
            : DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        writer.WriteStringValue(new DateTimeOffset(local, _fuso.GetUtcOffset(local)));
    }
}
