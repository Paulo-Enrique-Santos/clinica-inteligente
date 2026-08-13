#!/bin/bash
# Executado UMA VEZ, na criacao inicial do volume do Postgres.
# Se precisar reexecutar: docker compose down -v && docker compose up -d
set -euo pipefail

echo ">> Criando bancos e papeis da Clinica Inteligente"

# ---------------------------------------------------------------------------
# Papeis
#
# Separacao deliberada, e ela existe por causa do RLS (Fase 0, tarefa 5):
#   - APP_DB_OWNER : dono das tabelas. Roda as migrations do EF Core.
#   - APP_DB_USER  : usuario da aplicacao em runtime. NAO e dono de nada.
#
# Isso importa porque, no Postgres, o dono da tabela IGNORA as policies de RLS
# por padrao. Se a API se conectasse como dono, o Row Level Security viraria
# decoracao. Rodando a aplicacao com um papel sem privilegio, a policy vale de
# verdade e vira a segunda barreira real contra vazamento entre clinicas.
# ---------------------------------------------------------------------------
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
    CREATE ROLE ${APP_DB_OWNER} LOGIN PASSWORD '${APP_DB_OWNER_PASSWORD}';
    CREATE ROLE ${APP_DB_USER}  LOGIN PASSWORD '${APP_DB_USER_PASSWORD}';
    CREATE ROLE ${KEYCLOAK_DB_USER} LOGIN PASSWORD '${KEYCLOAK_DB_PASSWORD}';

    CREATE DATABASE ${APP_DB_NAME} OWNER ${APP_DB_OWNER};
    CREATE DATABASE ${KEYCLOAK_DB_NAME} OWNER ${KEYCLOAK_DB_USER};
EOSQL

# ---------------------------------------------------------------------------
# Banco da aplicacao: extensoes e permissoes
# ---------------------------------------------------------------------------
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$APP_DB_NAME" <<-EOSQL
    -- pgvector ja entra agora para nao trocar imagem/volume na Fase 7 (RAG).
    CREATE EXTENSION IF NOT EXISTS vector;
    CREATE EXTENSION IF NOT EXISTS pg_trgm;      -- busca textual (RAG hibrido)
    CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
    -- btree_gist permite combinar igualdade (tenant, profissional) com sobreposicao
    -- de intervalo na mesma constraint EXCLUDE da agenda.
    CREATE EXTENSION IF NOT EXISTS btree_gist;

    -- Ninguem alem dos nossos papeis cria coisa no schema public.
    REVOKE ALL ON SCHEMA public FROM PUBLIC;
    GRANT ALL ON SCHEMA public TO ${APP_DB_OWNER};
    GRANT USAGE ON SCHEMA public TO ${APP_DB_USER};

    -- O usuario de runtime ganha DML nas tabelas que o owner criar dali em diante.
    ALTER DEFAULT PRIVILEGES FOR ROLE ${APP_DB_OWNER} IN SCHEMA public
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ${APP_DB_USER};
    ALTER DEFAULT PRIVILEGES FOR ROLE ${APP_DB_OWNER} IN SCHEMA public
        GRANT USAGE, SELECT ON SEQUENCES TO ${APP_DB_USER};
EOSQL

echo ">> Bancos '${APP_DB_NAME}' e '${KEYCLOAK_DB_NAME}' prontos."
