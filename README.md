# CLINIQ — Clínica Inteligente

Sistema de gestão para clínica de estética com uma camada de agentes de IA operando os
canais de atendimento (WhatsApp): confirmação de agenda, acompanhamento pós-procedimento,
cobrança, remarketing, captação de novos pacientes e relatórios de gestão.

> **Status:** completo em funcionalidades; a camada de IA ainda não começou.
>
> 📄 **[CONTEXTO.md](CONTEXTO.md) — leia primeiro.** Reúne arquitetura, decisões de
> negócio, convenções, armadilhas conhecidas e o roadmap. É o documento que permite a
> outra pessoa (ou agente) continuar o trabalho sem reler o histórico.
>
> O planejamento original está em [PLANO.md](PLANO.md), já defasado em várias fases.

## Ideia central

O domínio é determinístico e vive no .NET; os agentes **não acessam o banco de dados**.
Eles enxergam o mundo apenas através de *tools*, que são endpoints da API — com as mesmas
validações, o mesmo isolamento multi-tenant e a mesma auditoria que a interface web usa.
A verdade fica no banco, nunca no contexto do modelo.

Todo agente nasce em modo **sugestão** (aprovação humana obrigatória) e só é promovido a
autônomo depois que os dados mostram que ele merece. As decisões humanas de aprovar, editar
ou rejeitar viram o dataset de avaliação dos agentes.

## Stack

| Camada | Tecnologia |
|---|---|
| Núcleo de domínio | .NET 9 — monolito modular |
| Serviço de agentes | Python + FastAPI |
| Frontend | Next.js (App Router) |
| Autenticação | Keycloak (OIDC, multi-tenant) |
| Banco | PostgreSQL + `pgvector` |
| Canais | WhatsApp Web + simulador web de números |
| LLM | Anthropic Claude — roteamento por custo (Haiku 4.5 / Sonnet 5 / Opus 5) |
| Observabilidade | OpenTelemetry + Langfuse |

## Estrutura planejada

```
/backend      .NET — domínio, API, multi-tenant, jobs, HITL
/agents       Python — agentes, tool calling, RAG, guardrails, evals
/frontend     Next.js — painel, simulador, ficha de anamnese
/channels     Node — adaptador WhatsApp Web
/infra        docker-compose, Keycloak, Postgres, observabilidade
/docs         decisões de arquitetura
```

## Conceitos de Engenharia de IA cobertos

LLM · Tool Calling · Agentes · RAG · Guardrails · HITL · Evals · Observabilidade · MCP —
cada um implementado numa fase específica do roadmap, com critério de pronto definido.
Ver seção 10 do [PLANO.md](PLANO.md).

## Aviso

O sistema lida com dados sensíveis de saúde (LGPD). Nenhum dump de banco, export de
paciente ou credencial entra neste repositório. Os agentes não fornecem diagnóstico —
coletam informação e escalam para a profissional responsável.
