using HabitaIA.Business.Imovel.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HabitaIA.Core.Mapping.Imovel
{
    public class ImovelModelMap : IEntityTypeConfiguration<ImovelModel>
    {
        public void Configure(EntityTypeBuilder<ImovelModel> b)
        {
            b.ToTable("imoveis");
            b.HasKey(i => i.Id);

            b.Property(i => i.Titulo).IsRequired().HasMaxLength(200);
            b.Property(i => i.Descricao).IsRequired();
            b.Property(i => i.Bairro).IsRequired().HasMaxLength(120);
            b.Property(i => i.Cidade).IsRequired().HasMaxLength(120);
            b.Property(i => i.UF).IsRequired().HasMaxLength(2);

            b.Property(i => i.Preco).HasColumnType("numeric(14,2)");
            b.Property(i => i.Area).HasColumnType("double precision");
            b.Property(i => i.CreatedAt).HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            // (2) Embedding como float4[] (real[]) no Postgres
            b.Property(i => i.Embedding).HasColumnType("real[]");

            b.HasIndex(i => new { i.TenantId, i.Preco, i.Quartos, i.Bairro });
            b.HasIndex(i => i.TenantId);
            b.HasIndex(i => i.Bairro);
            // opcional: case-insensitive para buscas lexicais
            // criar índice separado em migração SQL: create index idx_imoveis_bairro_ci on imoveis (lower("Bairro"));
        }
    }
}
