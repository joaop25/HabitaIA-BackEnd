using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HabitaIA.Core.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "imoveis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Bairro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UF = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Quartos = table.Column<int>(type: "integer", nullable: false),
                    Banheiros = table.Column<int>(type: "integer", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Area = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Embedding = table.Column<double[]>(type: "double precision[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imoveis", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_imoveis_Bairro",
                table: "imoveis",
                column: "Bairro");

            migrationBuilder.CreateIndex(
                name: "IX_imoveis_TenantId",
                table: "imoveis",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_imoveis_TenantId_Preco_Quartos_Bairro",
                table: "imoveis",
                columns: new[] { "TenantId", "Preco", "Quartos", "Bairro" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imoveis");
        }
    }
}
