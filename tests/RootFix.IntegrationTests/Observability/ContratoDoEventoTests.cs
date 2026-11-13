using System.Text.Json;
using RootFix.Application.Interfaces.Mensageria;
using RootFix.Domain.Enums;
using RootFix.Infrastructure.Mensageria;

namespace RootFix.IntegrationTests.Observability;

/// <summary>
/// O JSON de <c>problema.status-alterado</c> é um contrato com o listener da API Java:
/// qualquer campo a mais, a menos ou com outro nome quebra a integração.
/// </summary>
public class ContratoDoEventoTests
{
    [Fact]
    public void Evento_SerializaExatamenteNoFormatoCombinadoComOJava()
    {
        // Arrange
        var evento = new ProblemaStatusAlteradoEvento(
            12, StatusProblema.PENDENTE, StatusProblema.EM_ANDAMENTO, "A equipe vai amanhã cedo");

        // Act
        var json = JsonSerializer.Serialize(evento, RabbitMqPublicadorDeEventos.Json);

        // Assert
        Assert.Equal(
            """{"problemaId":12,"statusAnterior":"PENDENTE","statusNovo":"EM_ANDAMENTO","observacao":"A equipe vai amanhã cedo"}""",
            json);
    }

    [Fact]
    public void Evento_SemRecadoDoGestor_EnviaObservacaoNula()
    {
        // Arrange
        var evento = new ProblemaStatusAlteradoEvento(
            7, StatusProblema.EM_ANDAMENTO, StatusProblema.RESOLVIDO, null);

        // Act
        var json = JsonSerializer.Serialize(evento, RabbitMqPublicadorDeEventos.Json);

        // Assert
        Assert.Equal(
            """{"problemaId":7,"statusAnterior":"EM_ANDAMENTO","statusNovo":"RESOLVIDO","observacao":null}""",
            json);
    }
}
