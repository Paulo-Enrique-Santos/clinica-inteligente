# Backend (.NET 10)

```
src/Clinica.Domain           entidades e contratos, sem dependencia de infraestrutura
src/Clinica.Infrastructure   EF Core, Postgres, migrations, RLS
src/Clinica.Api              endpoints, autenticacao, resolucao de tenant
tests/Clinica.Tests          integracao com Postgres real (Testcontainers)
```

## Rodar

Com a infra de pé (ver [../infra/README.md](../infra/README.md)):

```bash
dotnet run --project backend/src/Clinica.Api
```

Swagger UI em **http://localhost:5231/swagger** (abre sozinho ao rodar pelo Visual Studio).
Documento OpenAPI cru em `/openapi/v1.json`.

`GET /health` é anônimo; o resto exige token do Keycloak. Para testar no Swagger:

1. Gere um token — ver [Gerar um token](../infra/README.md#gerar-um-token).
2. Clique em **Authorize**, no topo direito.
3. Cole **apenas o token**, sem escrever `Bearer` na frente (a UI já acrescenta).

Trocando o usuário do token você troca de clínica, e a mesma chamada devolve outro
conjunto de pacientes — é a forma mais rápida de ver o multi-tenant em ação.

## Migrations

```bash
dotnet dotnet-ef migrations add NomeDaMigration --project backend/src/Clinica.Infrastructure --startup-project backend/src/Clinica.Api --output-dir Persistence/Migrations
```

Aplicar:

```bash
dotnet dotnet-ef database update --project backend/src/Clinica.Infrastructure --startup-project backend/src/Clinica.Api
```

As migrations conectam como `clinica_owner` (dono das tabelas). A API conecta como
`clinica_app`, que **não** é dono — é isso que faz o RLS incidir sobre a aplicação.

> **Toda migration que cria tabela derivada de `TenantEntity` precisa chamar
> `TenantRls.Enable(migrationBuilder, "nome_da_tabela")`.** Esquecer é o modo mais provável
> de furar o isolamento entre clínicas, e por isso existe `RlsSchemaGuardTests`, que varre
> o banco e falha se alguma tabela com `tenant_id` ficar sem policy.

## Testes

```bash
dotnet test backend/Clinica.sln
```

Sobem um Postgres real via Testcontainers (Docker precisa estar rodando). O fixture recria
a separação de papéis da produção de propósito: se os testes usassem o superusuário do
container, o RLS seria ignorado e a suíte passaria mesmo com a policy errada.

## Onde mora a tenancy

| Mecanismo | Arquivo |
|---|---|
| Base das entidades | `Clinica.Domain/Tenancy/TenantEntity.cs` |
| Tenant vindo do token | `Clinica.Api/Tenancy/HttpTenantContext.cs` |
| Filtro global + carimbo na escrita | `Clinica.Infrastructure/Persistence/ClinicaDbContext.cs` |
| Variável de sessão para o RLS | `Clinica.Infrastructure/Persistence/TenantConnectionInterceptor.cs` |
| Policies de RLS | `Clinica.Infrastructure/Persistence/Migrations/TenantRls.cs` |

Decisões e o porquê de cada camada: [ADR 0001](../docs/adr/0001-multi-tenancy.md).
