using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EstoquePorEmbalagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "content_per_unit",
                table: "stock_items",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                // 1 e nao 0: conteudo zero faria a repartição em embalagens dividir por
                // zero. Um item antigo, sem essa informação, se comporta como se cada
                // embalagem tivesse uma unidade — que era exatamente o modelo anterior.
                defaultValue: 1m);

            migrationBuilder.AddColumn<string>(
                name: "control_mode",
                table: "stock_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Informado");

            migrationBuilder.AddColumn<string>(
                name: "purchase_unit",
                table: "stock_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "un");

            // Itens que ja existiam continuam coerentes: compra e consumo na mesma
            // unidade, uma por embalagem. Sem isto, um item cadastrado antes desta fase
            // apareceria comprado em "un" mesmo sendo medido em ml.
            migrationBuilder.Sql("""
                UPDATE stock_items SET purchase_unit = unit WHERE purchase_unit = 'un';

                ALTER TABLE stock_items ADD CONSTRAINT ck_stock_items_conteudo_positivo
                    CHECK (content_per_unit > 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_per_unit",
                table: "stock_items");

            migrationBuilder.DropColumn(
                name: "control_mode",
                table: "stock_items");

            migrationBuilder.DropColumn(
                name: "purchase_unit",
                table: "stock_items");
        }
    }
}
