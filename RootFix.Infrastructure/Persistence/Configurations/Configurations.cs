using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RootFix.Domain.Entities;

namespace RootFix.Infrastructure.Persistence.Configurations;

// Mapeamento sobre o DDL oficial (scripts/script-bd.sql). Tamanhos iguais aos do DDL (em CHAR).
// Enums são gravados como texto (PENDENTE, EM_ANDAMENTO...), o mesmo que o JPA grava com
// @Enumerated(EnumType.STRING); os nomes dos membros já são os valores aceitos pelos CHECKs.
// Colunas que a API .NET não usa (SENHA_HASH, ENDERECO, CRIADA_EM da comunidade...) ficam de fora.

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable(NomesDoBanco.Usuario);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(x => x.Nome).HasColumnName("NOME").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Email).HasColumnName("EMAIL").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Perfil).HasColumnName("PERFIL").HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Organizacao).HasColumnName("ORGANIZACAO").HasMaxLength(150);
    }
}

public sealed class ComunidadeConfiguration : IEntityTypeConfiguration<Comunidade>
{
    public void Configure(EntityTypeBuilder<Comunidade> builder)
    {
        builder.ToTable(NomesDoBanco.Comunidade);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(x => x.Nome).HasColumnName("NOME").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("DESCRICAO").HasMaxLength(500);
        builder.Property(x => x.GestorId).HasColumnName("ID_GESTOR");

        builder.HasOne(x => x.Gestor)
            .WithMany()
            .HasForeignKey(x => x.GestorId);
    }
}

public sealed class ParticipacaoConfiguration : IEntityTypeConfiguration<Participacao>
{
    public void Configure(EntityTypeBuilder<Participacao> builder)
    {
        builder.ToTable(NomesDoBanco.Participacao);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(x => x.UsuarioId).HasColumnName("ID_USUARIO");
        builder.Property(x => x.ComunidadeId).HasColumnName("ID_COMUNIDADE");
        builder.Property(x => x.EntrouEm).HasColumnName("ENTROU_EM");

        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId);
        builder.HasOne<Comunidade>().WithMany().HasForeignKey(x => x.ComunidadeId);
    }
}

public sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable(NomesDoBanco.Categoria);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(x => x.Nome).HasColumnName("NOME").HasMaxLength(60).IsRequired();
    }
}

public sealed class ProblemaConfiguration : IEntityTypeConfiguration<Problema>
{
    public void Configure(EntityTypeBuilder<Problema> builder)
    {
        builder.ToTable(NomesDoBanco.Problema);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(x => x.ComunidadeId).HasColumnName("ID_COMUNIDADE");
        builder.Property(x => x.AutorId).HasColumnName("ID_AUTOR");
        builder.Property(x => x.CategoriaId).HasColumnName("ID_CATEGORIA");
        builder.Property(x => x.Titulo).HasColumnName("TITULO").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("DESCRICAO").HasMaxLength(1000);
        builder.Property(x => x.Latitude).HasColumnName("LATITUDE");
        builder.Property(x => x.Longitude).HasColumnName("LONGITUDE");
        builder.Property(x => x.Adotado).HasColumnName("ADOTADO");
        builder.Property(x => x.AdotadoEm).HasColumnName("ADOTADO_EM");
        builder.Property(x => x.GravidadeIa).HasColumnName("GRAVIDADE_IA").HasMaxLength(10);
        builder.Property(x => x.JustificativaIa).HasColumnName("JUSTIFICATIVA_IA").HasMaxLength(500);
        builder.Property(x => x.CriadoEm).HasColumnName("CRIADO_EM");

        builder.Property(x => x.Prioridade)
            .HasColumnName("PRIORIDADE")
            .HasConversion<string>()
            .HasMaxLength(20);

        // Token de concorrência: o UPDATE leva "WHERE STATUS = <valor lido>". Se dois gestores
        // (ou duas abas) mudarem o status ao mesmo tempo, o segundo recebe 409 em vez de
        // sobrescrever o primeiro e deixar o histórico inconsistente.
        builder.Property(x => x.Status)
            .HasColumnName("STATUS")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsConcurrencyToken();

        // Propriedade calculada no domínio, sem coluna.
        builder.Ignore(x => x.StatusAtual);

        builder.HasOne(x => x.Comunidade).WithMany().HasForeignKey(x => x.ComunidadeId);
        builder.HasOne(x => x.Autor).WithMany().HasForeignKey(x => x.AutorId);
        builder.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId);
    }
}

public sealed class ApoioConfiguration : IEntityTypeConfiguration<Apoio>
{
    public void Configure(EntityTypeBuilder<Apoio> builder)
    {
        builder.ToTable(NomesDoBanco.Apoio);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(x => x.ProblemaId).HasColumnName("ID_PROBLEMA");
        builder.Property(x => x.UsuarioId).HasColumnName("ID_USUARIO");
        builder.Property(x => x.CriadoEm).HasColumnName("CRIADO_EM");

        builder.HasOne<Problema>().WithMany().HasForeignKey(x => x.ProblemaId);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId);
    }
}

public sealed class HistoricoStatusConfiguration : IEntityTypeConfiguration<HistoricoStatus>
{
    public void Configure(EntityTypeBuilder<HistoricoStatus> builder)
    {
        builder.ToTable(NomesDoBanco.HistoricoStatus);
        builder.HasKey(x => x.Id);

        // IDENTITY no DDL. O EF lê o valor de volta com RETURNING INTO, então o ID do
        // histórico já sai na resposta da API.
        builder.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        builder.Property(x => x.ProblemaId).HasColumnName("ID_PROBLEMA");
        builder.Property(x => x.UsuarioId).HasColumnName("ID_USUARIO");
        builder.Property(x => x.DataHora).HasColumnName("DATA_HORA");

        builder.Property(x => x.StatusAnterior)
            .HasColumnName("STATUS_ANTERIOR")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.StatusNovo)
            .HasColumnName("STATUS_NOVO")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasColumnName("OBSERVACAO")
            .HasMaxLength(HistoricoStatus.TamanhoMaximoObservacao);

        builder.HasOne<Problema>().WithMany().HasForeignKey(x => x.ProblemaId);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId);
    }
}
