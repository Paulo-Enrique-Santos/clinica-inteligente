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

## Gerar um token

Para testar a API (no Swagger, no Postman ou no curl) você precisa de um access token do
Keycloak. Em desenvolvimento o client `clinica-web` tem o *Direct Access Grant* habilitado,
então dá para pegar um token com usuário e senha, sem passar pelo navegador.

**PowerShell** — imprime o token já pronto para copiar:

```powershell
$b = @{ grant_type='password'; client_id='clinica-web'; client_secret='dev-secret-clinica-web'; username='bia.secretaria'; password='dev123'; scope='openid' }; (Invoke-RestMethod -Method Post -Uri "http://localhost:8080/realms/clinica/protocol/openid-connect/token" -Body $b -ContentType "application/x-www-form-urlencoded").access_token
```

**curl / bash**:

```bash
curl -s -X POST "http://localhost:8080/realms/clinica/protocol/openid-connect/token" -d "grant_type=password" -d "client_id=clinica-web" -d "client_secret=dev-secret-clinica-web" -d "username=bia.secretaria" -d "password=dev123" -d "scope=openid"
```

Troque `username` por qualquer usuário da tabela acima para testar outra clínica ou outro
papel. O token vale 15 minutos.

### Ver o que tem dentro do token

Um JWT são três blocos separados por ponto, em Base64URL. O do meio é o payload:

```powershell
$p = $token.Split('.')[1].Replace('-','+').Replace('_','/'); while ($p.Length % 4) { $p += '=' }; [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p))
```

O que precisa estar lá: `preferred_username`, `tenant_id`, `realm_access.roles` e
`aud: clinica-api`. Se `tenant_id` sumir, a API devolve erro dizendo isso — o problema
estará no protocol mapper do client (ver armadilhas acima).

Alternativa visual: cole o token em https://jwt.io. **Só faça isso com token de
desenvolvimento** — token de produção em site de terceiro é vazamento de credencial.

## Acessar o banco

```bash
docker exec -it -e PGPASSWORD=postgres_dev_only clinica-postgres psql -U postgres -d clinica
```

Dentro do psql: `\dt` lista as tabelas, `\d patients` descreve uma, `\q` sai.

### A pegadinha do RLS

Conectando como **`postgres`** (superusuário) você enxerga tudo — superusuário ignora RLS.
É o que você quer para depurar.

Mas conectando como `clinica_owner` ou `clinica_app`, um `SELECT * FROM patients` devolve
**zero linhas**, mesmo com dados na tabela. Não é bug: é o RLS negando por padrão, porque a
variável de sessão `app.tenant_id` não foi definida. Em uma requisição real quem define é o
`TenantConnectionInterceptor`. No psql você define na mão:

```sql
SELECT set_config('app.tenant_id', '11111111-1111-1111-1111-111111111111', false);
SELECT full_name, phone_e164 FROM patients;
```

Trocando para o tenant `2222…` a mesma query devolve outro conjunto de linhas. É a forma
mais direta de ver o multi-tenant funcionando.

### Cliente gráfico

DBeaver, pgAdmin ou o próprio Visual Studio conectam em `localhost:5432`, banco `clinica`,
usuário `postgres`, senha `postgres_dev_only`. Vale lembrar da pegadinha acima se você
optar por conectar com os outros usuários.
