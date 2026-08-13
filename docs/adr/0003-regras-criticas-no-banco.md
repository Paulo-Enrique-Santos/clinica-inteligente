# ADR 0003 — Regras críticas vivem no banco, não só na aplicação

**Status:** aceito · **Fase:** 1

## Contexto

A agenda tem uma regra simples de enunciar: a mesma profissional não pode ter dois
atendimentos sobrepostos. A implementação óbvia é consultar antes de gravar:

```csharp
var livre = !await db.Appointments.AnyAsync(a => /* sobrepõe */);
if (livre) { db.Add(novo); await db.SaveChangesAsync(); }
```

Isso funciona em teste manual e falha na recepção. Entre a consulta e a gravação existe uma
janela: duas atendentes ao telefone ao mesmo tempo, ou a secretária e o agente de IA (Fase 4)
agindo em paralelo, ambos recebem "está livre" e ambos gravam. O resultado é duas pacientes
marcadas no mesmo horário — e a clínica só descobre quando as duas chegam.

## Decisão

Regras que não podem ser violadas ficam no **Postgres**, como constraint. A aplicação
continua validando, mas para dar mensagem decente — não para garantir a regra.

Nesta fase, três constraints:

**1. Sobreposição de horário** — `EXCLUDE USING gist`, que compara igualdade de tenant e de
profissional com sobreposição de intervalo. Atendimento cancelado não ocupa horário, daí o
`WHERE (status <> 'Cancelado')`. O intervalo é `[)`: fechado no início, aberto no fim, então
um atendimento que termina às 17h não conflita com outro que começa às 17h.

**2. Intervalo válido** — `CHECK (ends_at > starts_at)`. Óbvio, e por isso mesmo fácil de
furar num bug de fuso ou de cálculo de duração.

**3. Referência dentro do mesmo tenant** — chave estrangeira composta
`(tenant_id, patient_id) → patients (tenant_id, id)`.

A terceira merece explicação, porque não é óbvia: **no Postgres, a verificação de chave
estrangeira ignora RLS**. Uma FK comum, só pelo `id`, aceitaria um atendimento apontando
para paciente de outra clínica. A FK composta fecha o buraco no banco, na mesma linha da
ADR 0001.

## Consequências

- A API traduz `SQLSTATE 23P01` (violação de EXCLUDE) em **409** com mensagem legível. Sem
  essa tradução, a recepção veria erro 500.
- Toda tabela referenciada por FK composta precisa de `UNIQUE (tenant_id, id)`.
- A constraint de sobreposição depende da extensão `btree_gist`.

## Onde a extensão é criada — e por quê não na migration

Primeira tentativa foi `CREATE EXTENSION` dentro da migration. Funcionou em desenvolvimento
e quebrou na suíte de testes com `permission denied`: em dev o banco pertence ao
`clinica_owner`, no container de teste pertence ao `postgres`.

O erro apontou algo mais interessante que a diferença de ambiente: **criar extensão é
provisionamento de banco, não migração de aplicação**. Exige privilégio que o usuário de
migration não tem — e não deveria ter. As extensões agora vivem em
`infra/postgres/init/01-databases.sh`, junto de `vector` e `pg_trgm`, e o fixture de teste
reproduz o mesmo provisionamento.

## Alternativa descartada

**Bloqueio pessimista** (`SELECT ... FOR UPDATE` na agenda da profissional). Resolveria a
corrida, mas serializa agendamentos e coloca em código de aplicação uma regra que o banco
expressa em quatro linhas.
