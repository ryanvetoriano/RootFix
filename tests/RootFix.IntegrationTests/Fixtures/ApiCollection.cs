using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RootFix.IntegrationTests.Fixtures;

/// <summary>
/// Fixture compartilhada: uma API e um banco por execução da coleção. Os testes criam os
/// próprios dados, então podem dividir o mesmo host sem interferir uns nos outros.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public RootFixApiFactory Factory { get; } = new();

    public Task InitializeAsync() => Factory.InicializarBancoAsync();

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "API de integração";
}

/// <summary>JSON igual ao da API: camelCase e enums como texto.</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T> LerAsync<T>(this HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>(Options))!;

    public static async Task<JsonElement> LerJsonAsync(this HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>(Options);
}
