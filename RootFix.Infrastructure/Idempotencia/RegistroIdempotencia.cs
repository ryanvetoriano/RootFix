using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RootFix.Infrastructure.Persistence;

namespace RootFix.Infrastructure.Idempotencia;

/// <summary>
/// Linha da tabela RF_IDEMPOTENCIA: uma <c>Idempotency-Key</c> já vista e, quando a operação
/// terminou, a resposta que deve ser repetida.
/// </summary>
/// <remarks>
/// <para>Fica no Oracle (e não em memória) porque o Render gratuito desliga a instância depois
/// de 15 minutos parada: uma chave em memória se perderia justamente quando o app costuma
/// reenviar — na primeira chamada lenta depois que o serviço acorda.</para>
/// <para><b>Situação da chave</b> sem coluna própria: <c>STATUS_HTTP = 102</c> (o código HTTP
/// "Processing") marca uma reserva em andamento; ao terminar, recebe o status da resposta
/// (200). A validade é contada a partir de <c>CRIADO_EM</c>.</para>
/// </remarks>
public class RegistroIdempotencia
{
    /// <summary>HTTP 102 Processing: reserva feita, operação ainda executando.</summary>
    public const int EmProcessamento = 102;

    public long Id { get; set; }

    public string Chave { get; set; } = null!;

    /// <summary>Dono da chave (usuário autenticado). Chaves de usuários diferentes não colidem.</summary>
    public long UsuarioId { get; set; }

    /// <summary>Método e caminho (ex.: <c>PATCH /painel/problemas/12/status</c>).</summary>
    public string Rota { get; set; } = null!;

    /// <summary>SHA-256 de método + rota + corpo. Detecta a mesma chave usada para outra operação.</summary>
    public string ImpressaoDigital { get; set; } = null!;

    public int StatusHttp { get; set; } = EmProcessamento;

    public string? Resposta { get; set; }

    public DateTime CriadoEm { get; set; }
}

public sealed class RegistroIdempotenciaConfiguration : IEntityTypeConfiguration<RegistroIdempotencia>
{
    public const int TamanhoMaximoChave = 100;
    public const int TamanhoMaximoRota = 200;

    public void Configure(EntityTypeBuilder<RegistroIdempotencia> builder)
    {
        builder.ToTable(NomesDoBanco.Idempotencia);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(x => x.Chave).HasColumnName("CHAVE").HasMaxLength(TamanhoMaximoChave).IsRequired();
        builder.Property(x => x.UsuarioId).HasColumnName("ID_USUARIO");
        builder.Property(x => x.Rota).HasColumnName("ROTA").HasMaxLength(TamanhoMaximoRota).IsRequired();
        builder.Property(x => x.ImpressaoDigital).HasColumnName("IMPRESSAO_DIGITAL").HasMaxLength(64).IsRequired();
        builder.Property(x => x.StatusHttp).HasColumnName("STATUS_HTTP");
        builder.Property(x => x.Resposta).HasColumnName("RESPOSTA");
        builder.Property(x => x.CriadoEm).HasColumnName("CRIADO_EM");

        // A unicidade é o que torna a reserva atômica: duas requisições simultâneas com a
        // mesma chave não conseguem inserir as duas.
        builder.HasIndex(x => new { x.Chave, x.UsuarioId })
            .IsUnique()
            .HasDatabaseName("UK_RF_IDEMPOTENCIA");

        builder.HasOne<Domain.Entities.Usuario>().WithMany().HasForeignKey(x => x.UsuarioId);
    }
}
