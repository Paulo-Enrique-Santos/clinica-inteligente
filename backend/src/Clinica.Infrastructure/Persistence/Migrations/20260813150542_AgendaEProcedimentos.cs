using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinica.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgendaEProcedimentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procedures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    supplies_cost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procedures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "professionals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    keycloak_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    specialty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professionals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    procedure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    professional_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointments_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_procedures_procedure_id",
                        column: x => x.procedure_id,
                        principalTable: "procedures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_professionals_professional_id",
                        column: x => x.professional_id,
                        principalTable: "professionals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_id",
                table: "appointments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_procedure_id",
                table: "appointments",
                column: "procedure_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_professional_id",
                table: "appointments",
                column: "professional_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_id",
                table: "appointments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_id_patient_id",
                table: "appointments",
                columns: new[] { "tenant_id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_id_professional_id_starts_at",
                table: "appointments",
                columns: new[] { "tenant_id", "professional_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_id_starts_at",
                table: "appointments",
                columns: new[] { "tenant_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_procedures_tenant_id",
                table: "procedures",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_procedures_tenant_id_name",
                table: "procedures",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professionals_tenant_id",
                table: "professionals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_professionals_tenant_id_active",
                table: "professionals",
                columns: new[] { "tenant_id", "active" });

            TenantRls.Enable(migrationBuilder, "procedures");
            TenantRls.Enable(migrationBuilder, "professionals");
            TenantRls.Enable(migrationBuilder, "appointments");

            // -----------------------------------------------------------------
            // 1. Chave estrangeira dentro do MESMO tenant
            //
            // As FKs que o EF gerou olham so o id. O problema: no Postgres, a
            // verificacao de chave estrangeira ignora RLS. Ou seja, um bug na
            // aplicacao poderia criar um atendimento apontando para uma paciente de
            // OUTRA clinica, e o banco aceitaria.
            //
            // A FK composta fecha esse buraco: so aceita a referencia se o tenant
            // bater dos dois lados.
            // -----------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE patients      ADD CONSTRAINT uq_patients_tenant_id      UNIQUE (tenant_id, id);
                ALTER TABLE procedures    ADD CONSTRAINT uq_procedures_tenant_id    UNIQUE (tenant_id, id);
                ALTER TABLE professionals ADD CONSTRAINT uq_professionals_tenant_id UNIQUE (tenant_id, id);

                ALTER TABLE appointments ADD CONSTRAINT fk_appointments_patient_mesmo_tenant
                    FOREIGN KEY (tenant_id, patient_id) REFERENCES patients (tenant_id, id);

                ALTER TABLE appointments ADD CONSTRAINT fk_appointments_procedure_mesmo_tenant
                    FOREIGN KEY (tenant_id, procedure_id) REFERENCES procedures (tenant_id, id);

                ALTER TABLE appointments ADD CONSTRAINT fk_appointments_professional_mesmo_tenant
                    FOREIGN KEY (tenant_id, professional_id) REFERENCES professionals (tenant_id, id);
                """);

            // -----------------------------------------------------------------
            // 2. Atendimento tem que terminar depois de comecar
            //
            // Obvio, e por isso mesmo facil de furar num bug de fuso ou de calculo
            // de duracao. Alem disso, o tipo de intervalo usado na regra abaixo
            // rejeita faixa invertida com um erro feio; melhor barrar aqui.
            // -----------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE appointments ADD CONSTRAINT ck_appointments_intervalo_valido
                    CHECK (ends_at > starts_at);
                """);

            // -----------------------------------------------------------------
            // 3. Duas pacientes no mesmo horario com a mesma profissional: NAO
            //
            // A checagem tambem existe na aplicacao, para devolver mensagem
            // decente. Mas checar na aplicacao tem uma janela de corrida: duas
            // requisicoes simultaneas consultam "esta livre?", ambas recebem sim,
            // e ambas gravam. Numa recepcao com duas pessoas atendendo telefone ao
            // mesmo tempo, isso acontece de verdade.
            //
            // A constraint EXCLUDE resolve no banco, sob a mesma transacao — nao ha
            // janela. Cancelado nao ocupa horario, dai o WHERE.
            // -----------------------------------------------------------------
            // Depende da extensao btree_gist, provisionada junto do banco (ver
            // infra/postgres/init) e nao aqui: criar extensao exige privilegio que o
            // usuario de migration nao tem — e nao deveria ter.
            migrationBuilder.Sql("""
                ALTER TABLE appointments ADD CONSTRAINT ck_appointments_sem_sobreposicao
                    EXCLUDE USING gist (
                        tenant_id       WITH =,
                        professional_id WITH =,
                        tstzrange(starts_at, ends_at, '[)') WITH &&
                    ) WHERE (status <> 'Cancelado');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            TenantRls.Disable(migrationBuilder, "appointments");
            TenantRls.Disable(migrationBuilder, "professionals");
            TenantRls.Disable(migrationBuilder, "procedures");

            // Ordem importa: a constraint unica de patients so pode cair depois que
            // appointments (e as FKs que dependem dela) deixarem de existir. As de
            // procedures e professionals somem junto com as proprias tabelas.
            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "procedures");

            migrationBuilder.DropTable(
                name: "professionals");

            migrationBuilder.Sql("""
                ALTER TABLE patients DROP CONSTRAINT IF EXISTS uq_patients_tenant_id;
                """);
        }
    }
}
