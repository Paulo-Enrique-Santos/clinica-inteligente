# ADR 0002 — Identidade dos agentes e escopo de tenant

**Status:** aceito (implementação na Fase 4) · **Fase:** 0

## Contexto

A ADR 0001 estabelece que `tenant_id` vem **sempre do token**. Isso funciona bem para
humanos: cada usuário pertence a uma clínica. Mas o serviço de agentes (Python) é um só
processo, compartilhado, que atende todas as clínicas. Ele não "pertence" a um tenant.

Se o agente usasse um token de client credentials e informasse o tenant por parâmetro,
a regra da ADR 0001 seria quebrada logo no primeiro agente — e por um caminho que aceita
qualquer tenant que o chamador pedir.

## Decisão

O token de client credentials do `clinica-agents` **não carrega `tenant_id`** e, sozinho,
não dá acesso a nenhum dado de clínica. Ele só serve para o serviço se autenticar.

O acesso a dados acontece com um **token com escopo de execução**, emitido pela API .NET
no momento em que ela dispara um agente:

```
API .NET decide executar o agente de agenda para a clínica X
  -> emite token de execução { tenant_id: X, agent: "agenda", run_id: ..., exp: curto }
  -> chama o serviço Python passando esse token
  -> toda tool que o agente chamar de volta apresenta esse token
  -> a API valida: tenant vem do token, como sempre
```

Propriedades que isso garante:

- O tenant continua vindo do token — a regra da ADR 0001 vale para agente também.
- O token é curto e amarrado a um `run_id`, então dá para auditar exatamente qual execução
  de qual agente tocou qual dado.
- Um agente comprometido ou com alucinação não consegue alcançar outra clínica: não existe
  parâmetro de tenant para ele manipular.

## Permissões

O papel `AGENT` é **deliberadamente menor** que o de qualquer humano. O agente pode
consultar agenda, remarcar e enviar mensagem; não pode conceder desconto, excluir dado,
alterar preço nem mexer em configuração. Quando a ação necessária está fora do que o papel
permite, o agente abre uma `HitlTask` — que é o comportamento desejado, não uma falha.

## Consequências

- Na Fase 0 o client `clinica-agents` existe, mas ainda não há emissão de token de execução.
  Isso entra na Fase 4, junto do primeiro agente.
- Auditoria distingue "a secretária remarcou" de "o agente remarcou" sem heurística.
