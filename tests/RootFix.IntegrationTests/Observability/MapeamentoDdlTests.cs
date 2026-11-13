using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using RootFix.Infrastructure.Persistence;
using RootFix.IntegrationTests.Fixtures;

namespace RootFix.IntegrationTests.Observability;

/// <summary>
/// Os testes de integração rodam em SQLite, que aceita qualquer nome de coluna. Este teste
/// fecha essa lacuna: confere o modelo do EF Core contra o DDL oficial do grupo
/// (<c>scripts/script-bd.sql</c>). Se o Raul renomear uma coluna, o teste aponta qual.
/// </summary>
[Collection(ApiCollection.Name)]
public partial class MapeamentoDdlTests
{
    private readonly RootFixApiFactory _factory;

    public MapeamentoDdlTests(ApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public void ModeloEfCore_TodasAsTabelasEColunasExistemNoDdlOficial()
    {
        // Arrange
        var ddl = LerDdl();
        using var scope = _factory.CriarEscopo();
        var modelo = scope.ServiceProvider.GetRequiredService<RootFixContext>().Model;

        var faltando = new List<string>();

        // Act
        foreach (var entidade in modelo.GetEntityTypes())
        {
            var tabela = entidade.GetTableName()!;
            if (!ddl.TryGetValue(tabela, out var colunas))
            {
                faltando.Add($"tabela {tabela} ({entidade.ClrType.Name})");
                continue;
            }

            var identificador = StoreObjectIdentifier.Table(tabela, entidade.GetSchema());
            foreach (var propriedade in entidade.GetProperties())
            {
                var coluna = propriedade.GetColumnName(identificador)!;
                if (!colunas.Contains(coluna))
                    faltando.Add($"{tabela}.{coluna} ({entidade.ClrType.Name}.{propriedade.Name})");
            }
        }

        // Assert
        Assert.True(faltando.Count == 0,
            "Mapeamento do EF Core sem correspondência no scripts/script-bd.sql:\n" + string.Join("\n", faltando));
    }

    [Fact]
    public void ColunasObrigatoriasDasTabelasEscritasPeloDotnet_EstaoMapeadas()
    {
        // Arrange
        // Nas tabelas em que a API .NET faz INSERT, toda coluna NOT NULL sem DEFAULT precisa
        // estar no modelo; senão o INSERT falha no Oracle (ORA-01400).
        var obrigatorias = LerColunasObrigatoriasSemDefault();
        using var scope = _factory.CriarEscopo();
        var modelo = scope.ServiceProvider.GetRequiredService<RootFixContext>().Model;

        var faltando = new List<string>();

        // Act
        foreach (var tabela in new[] { NomesDoBanco.HistoricoStatus, NomesDoBanco.Idempotencia })
        {
            var entidade = modelo.GetEntityTypes().Single(e => e.GetTableName() == tabela);
            var identificador = StoreObjectIdentifier.Table(tabela, entidade.GetSchema());
            var mapeadas = entidade.GetProperties().Select(p => p.GetColumnName(identificador)).ToHashSet();

            faltando.AddRange(obrigatorias[tabela].Where(c => c != "ID" && !mapeadas.Contains(c))
                .Select(c => $"{tabela}.{c}"));
        }

        // Assert
        Assert.True(faltando.Count == 0, "Colunas NOT NULL sem DEFAULT não mapeadas:\n" + string.Join("\n", faltando));
    }

    private static string CaminhoDoDdl()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "RootFix.sln")))
            pasta = pasta.Parent;

        Assert.NotNull(pasta);
        return Path.Combine(pasta.FullName, "scripts", "script-bd.sql");
    }

    private static IEnumerable<(string Tabela, string Corpo)> Tabelas()
    {
        var sql = File.ReadAllText(CaminhoDoDdl());

        foreach (Match bloco in CreateTable().Matches(sql))
            yield return (bloco.Groups["tabela"].Value.ToUpperInvariant(), bloco.Groups["corpo"].Value);
    }

    private static IEnumerable<string> LinhasDeColuna(string corpo)
        => corpo.Split('\n')
            .Select(l => Regex.Replace(l, "--.*$", "").Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase)
                        && !l.StartsWith("REFERENCES", StringComparison.OrdinalIgnoreCase)
                        && !l.StartsWith("OR ", StringComparison.OrdinalIgnoreCase)
                        && !l.StartsWith("(", StringComparison.Ordinal));

    private static Dictionary<string, HashSet<string>> LerDdl()
        => Tabelas().ToDictionary(
            t => t.Tabela,
            t => LinhasDeColuna(t.Corpo)
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant())
                .ToHashSet());

    private static Dictionary<string, List<string>> LerColunasObrigatoriasSemDefault()
        => Tabelas().ToDictionary(
            t => t.Tabela,
            t => LinhasDeColuna(t.Corpo)
                .Where(l => l.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase)
                            && !l.Contains("DEFAULT", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant())
                .ToList());

    [GeneratedRegex(@"CREATE\s+TABLE\s+(?<tabela>\w+)\s*\((?<corpo>.*?)\n\);", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex CreateTable();
}
