# ADR 0001 — Estratégia de multi-tenancy

**Status:** aceito · **Fase:** 0

## Contexto

O sistema atende várias clínicas na mesma instalação. Os dados são de saúde (LGPD):
vazamento entre clínicas não é bug, é incidente reportável. Ao mesmo tempo, a premissa
de custo do projeto é rodar em free tier — um banco por clínica é inviável.

## Decisão

**Shared-everything**: um banco, um schema, coluna `tenant_id` em toda tabela de negócio.

Quatro mecanismos, em camadas, porque nenhum deles sozinho é suficiente:

1. **`TenantEntity`** — toda entidade de negócio herda `tenant_id`. Não existe tabela de
   negócio sem tenant.
2. **`ITenantContext`** — o `tenant_id` é lido **exclusivamente do claim do token**.
   Nunca de body, query string ou header. Um cliente não consegue pedir dado de outro
   tenant porque não existe caminho de entrada para essa informação.
3. **Global query filter (EF Core)** — aplicado automaticamente em toda entidade
   `TenantEntity`. Torna impossível escrever uma consulta sem filtro por descuido, que é
   o modo real como esse bug acontece.
4. **Row Level Security (Postgres)** — policy no banco baseada em variável de sessão,
   definida por request. Se o filtro do ORM falhar (query crua, migration mal feita, bug
   do EF), o banco recusa.

### Dois papéis de banco

Consequência não óbvia do RLS: **o dono da tabela ignora as policies por padrão**. Se a
API se conectasse como dono, o RLS seria decorativo. Por isso:

- `clinica_owner` — dono das tabelas, roda migrations. Não é usado em runtime.
- `clinica_app` — usuário da API em runtime, sem privilégio de dono. É sobre ele que a
  policy realmente incide.

## Consequências

- Toda migration nova precisa habilitar RLS na tabela criada. Isso é fácil de esquecer →
  existe teste que falha se uma tabela `TenantEntity` estiver sem policy.
- Consultas administrativas cross-tenant (relatórios globais, suporte) precisam de um
  caminho explícito e auditado, não do caminho normal da aplicação.
- O teste de isolamento entre tenants roda na CI e trava o merge. Ele é o guardião desta ADR.

## Alternativas descartadas

- **Schema por tenant**: melhor isolamento, mas migrations viram operação de N schemas e
  o custo de conexões cresce. Reavaliar se passar de ~50 clínicas.
- **Banco por tenant**: isolamento máximo, custo incompatível com a premissa de free tier.
