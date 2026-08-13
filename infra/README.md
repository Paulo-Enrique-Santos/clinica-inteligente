# Infra de desenvolvimento local

Postgres 17 (com `pgvector`) + Keycloak 26. Tudo em container, nada instalado na máquina.

## Subir

```bash
cp infra/.env.example infra/.env
docker compose -f infra/docker-compose.yml --env-file infra/.env up -d
```

- Keycloak: http://localhost:8080 — admin `admin` / `admin_dev_only`
- Postgres: `localhost:5432` — bancos `clinica` e `keycloak`

## Usuários seed

Senha de todos: `dev123`

| Usuário | Papel | Clínica |
|---|---|---|
| `ana.owner` | OWNER | Bella Face (tenant `1111…`) |
| `carla.doutora` | DOCTOR | Bella Face |
| `bia.secretaria` | SECRETARY | Bella Face |
| `sofia.financeiro` | FINANCE | Bella Face |
| `rita.owner` | OWNER | Nova Estética (tenant `2222…`) |

Duas clínicas desde o primeiro dia, de propósito: é o que permite o teste de isolamento
entre tenants existir de verdade em vez de ser teórico.

## Reimportar o realm depois de editar `clinica-realm.json`

O `--import-realm` **ignora realm que já existe**. Editar o JSON e reiniciar não basta:

```bash
docker compose -f infra/docker-compose.yml --env-file infra/.env restart keycloak
```

...só funciona depois de apagar o realm (Admin Console → Realm settings → Delete, ou via
Admin REST API). Para zerar tudo, incluindo o Postgres:

```bash
docker compose -f infra/docker-compose.yml --env-file infra/.env down -v
```

O script `postgres/init/01-databases.sh` também só roda na **primeira** criação do volume.

## Duas armadilhas do Keycloak que já custaram tempo aqui

**1. `clientScopes` no JSON de import substitui, não acrescenta.**
Declarar um `clientScopes` com um único scope customizado apaga os nativos (`profile`,
`email`, `roles`, `basic`, `web-origins`). O sintoma é sutil: o token vem válido, mas sem
`preferred_username` e sem `realm_access.roles`. Por isso o mapper de `tenant_id` está
pendurado direto nos `protocolMappers` do client, e nenhum client declara
`defaultClientScopes` — assim herdam os padrões do realm.

**2. Atributo customizado não aparece no token sem liberar o user profile.**
Desde o Keycloak 24, atributos não declarados são descartados (declarative user profile).
O `tenant_id` só chega no token porque o realm declara o atributo e usa
`unmanagedAttributePolicy: ENABLED` no bloco `components`. Note que a permissão de `edit`
do `tenant_id` é **só admin** — usuário não muda o próprio tenant.

## Verificar que o token está correto

```bash
docker exec clinica-keycloak /opt/keycloak/bin/kcadm.sh config credentials --server http://localhost:8080 --realm master --user admin --password admin_dev_only
```

Ou pegue um token direto e inspecione o payload — o que deve estar lá:
`preferred_username`, `tenant_id`, `realm_access.roles` e `aud: clinica-api`.
