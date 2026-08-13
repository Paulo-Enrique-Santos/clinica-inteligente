using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceiroEEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    minimum_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_movements_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_movements_stock_items_stock_item_id",
                        column: x => x.stock_item_id,
                        principalTable: "stock_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payments_appointment_id",
                table: "payments",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id",
                table: "payments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_appointment_id",
                table: "payments",
                columns: new[] { "tenant_id", "appointment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_tenant_id_status_due_date",
                table: "payments",
                columns: new[] { "tenant_id", "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_tenant_id",
                table: "stock_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_items_tenant_id_name",
                table: "stock_items",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_appointment_id",
                table: "stock_movements",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_stock_item_id",
                table: "stock_movements",
                column: "stock_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_tenant_id",
                table: "stock_movements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_tenant_id_stock_item_id",
                table: "stock_movements",
                columns: new[] { "tenant_id", "stock_item_id" });

            TenantRls.Enable(migrationBuilder, "payments");
            TenantRls.Enable(migrationBuilder, "stock_items");
            TenantRls.Enable(migrationBuilder, "stock_movements");

            // FKs dentro do mesmo tenant. A verificacao de chave estrangeira do Postgres
            // ignora RLS, entao sem isto um pagamento poderia referenciar atendimento de
            // outra clinica (ver ADR 0003).
            migrationBuilder.Sql("""
                ALTER TABLE appointments ADD CONSTRAINT uq_appointments_tenant_id UNIQUE (tenant_id, id);
                ALTER TABLE stock_items  ADD CONSTRAINT uq_stock_items_tenant_id  UNIQUE (tenant_id, id);

                ALTER TABLE payments ADD CONSTRAINT fk_payments_appointment_mesmo_tenant
                    FOREIGN KEY (tenant_id, appointment_id) REFERENCES appointments (tenant_id, id);

                ALTER TABLE stock_movements ADD CONSTRAINT fk_movements_item_mesmo_tenant
                    FOREIGN KEY (tenant_id, stock_item_id) REFERENCES stock_items (tenant_id, id);
                """);

            migrationBuilder.Sql("""
                -- Valor negativo em cobranca e sempre erro de digitacao ou de calculo.
                ALTER TABLE payments ADD CONSTRAINT ck_payments_valor_positivo
                    CHECK (amount > 0);

                -- Pago sem data de pagamento deixa o financeiro sem saber quando entrou;
                -- e data de pagamento sem estar pago e contradicao pura.
                ALTER TABLE payments ADD CONSTRAINT ck_payments_pago_tem_data
                    CHECK ((status = 'Pago') = (paid_at IS NOT NULL));

                -- Entrada e saida trazem quantidade positiva (a direcao vem do tipo).
                -- Ajuste vem com sinal, mas ajuste de zero nao e ajuste de nada.
                ALTER TABLE stock_movements ADD CONSTRAINT ck_movements_quantidade
                    CHECK (
                        (type IN ('Entrada','Saida') AND quantity > 0)
                        OR (type = 'Ajuste' AND quantity <> 0)
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            TenantRls.Disable(migrationBuilder, "payments");
            TenantRls.Disable(migrationBuilder, "stock_movements");
            TenantRls.Disable(migrationBuilder, "stock_items");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_items");

            // Só depois das tabelas caírem, porque as FKs dependiam desta constraint.
            migrationBuilder.Sql("""
                ALTER TABLE appointments DROP CONSTRAINT IF EXISTS uq_appointments_tenant_id;
                """);
        }
    }
}
