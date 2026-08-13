using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FichaDeAnamnese : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anamnesis_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anamnesis_links", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "anamnesis_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answers_json = table.Column<string>(type: "jsonb", nullable: false),
                    image_consent = table.Column<bool>(type: "boolean", nullable: false),
                    data_consent = table.Column<bool>(type: "boolean", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anamnesis_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_anamnesis_responses_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_anamnesis_links_token",
                table: "anamnesis_links",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_anamnesis_responses_patient_id",
                table: "anamnesis_responses",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_anamnesis_responses_tenant_id",
                table: "anamnesis_responses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_anamnesis_responses_tenant_id_patient_id",
                table: "anamnesis_responses",
                columns: new[] { "tenant_id", "patient_id" });

            // A ficha preenchida e dado de saude e segue a regra de todo mundo.
            TenantRls.Enable(migrationBuilder, "anamnesis_responses");

            migrationBuilder.Sql("""
                ALTER TABLE anamnesis_responses ADD CONSTRAINT fk_anamnese_paciente_mesmo_tenant
                    FOREIGN KEY (tenant_id, patient_id) REFERENCES patients (tenant_id, id)
                    ON DELETE CASCADE;
                """);

            // anamnesis_links fica DE FORA do RLS de proposito: e a tabela pela qual a
            // clinica e descoberta, quando a paciente abre o link sem estar logada. Sob
            // RLS, a busca pelo token nao acharia nada — nao haveria tenant para filtrar
            // antes de o token ser lido.
            //
            // O que protege a tabela e o proprio token: 32 bytes aleatorios, com validade
            // e uso unico. Em compensacao, ela nao guarda nada sensivel: so o vinculo
            // entre token, clinica e paciente.
            migrationBuilder.Sql("""
                ALTER TABLE anamnesis_links ADD CONSTRAINT fk_links_paciente_mesmo_tenant
                    FOREIGN KEY (tenant_id, patient_id) REFERENCES patients (tenant_id, id)
                    ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            TenantRls.Disable(migrationBuilder, "anamnesis_responses");

            migrationBuilder.DropTable(
                name: "anamnesis_links");

            migrationBuilder.DropTable(
                name: "anamnesis_responses");
        }
    }
}
