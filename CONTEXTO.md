# CLINIQ — contexto do projeto

Documento de transferência de contexto. Quem chegar aqui — pessoa ou agente — deve
conseguir continuar o trabalho sem reler o histórico de conversas.

Repositório: https://github.com/Paulo-Enrique-Santos/clinica-inteligente

---

## 1. O que é, e por que existe

Sistema de gestão para clínica de estética, com uma camada de agentes de IA operando os
canais de atendimento (WhatsApp). Existe uma clínica real por trás, com implantação
prevista assim que o sistema amadurecer.

O projeto tem **dois objetivos simultâneos**, e isso explica várias decisões:

1. Entregar um produto que uma clínica de verdade use.
2. Servir de aprendizado prático dos conceitos cobrados em vagas de Engenheiro de IA:
   **LLM, tool calling, agentes, RAG, guardrails, HITL, evals, observabilidade e MCP**.

A camada de IA **ainda não começou**. Tudo que existe hoje é o domínio determinístico que
os agentes vão operar — e isso é deliberado (ver §3).

---

## 2. Estado atual

**Completo em funcionalidades**, com 68 testes de backend passando e CI verde.

| Área | O que já funciona |
|---|---|
| Agenda | visão dia e semana em grade, expediente por profissional, exceções por data, encaixe validado |
| Pacientes | cadastro, busca, ficha em abas paginadas, anamnese por link público |
| Protocolos | prescrição da doutora, orçamento da recepção, cancelamento |
| Financeiro | cobranças, dashboard, baixa automática por meio de pagamento, estorno |
| Estoque | compra em embalagem, consumo em conteúdo, dois modos de controle |
| Equipe | onboarding de contas pela dona, papéis, matriz de permissões |

**O que não existe ainda:** qualquer coisa de IA, canal de WhatsApp, simulador de números,
filtros e ordenação nas tabelas, busca global, e multi-unidade (ver §9).

---

## 3. As três teses do projeto

Se você só puder guardar três coisas deste documento, guarde estas.

### Agente é a última camada, não a primeira

Um agente só é útil se existir domínio determinístico embaixo. O LLM **não decide** nada
crítico: ele interpreta linguagem natural, escolhe qual ferramenta chamar e escreve texto
no tom certo. A verdade fica no banco, nunca no contexto do modelo.

É por isso que sete fases foram construídas sem uma linha de IA. O que parece atraso é o
que vai permitir ao agente ter ferramentas reais para chamar.

### O agente não acessa o banco

Ele enxerga o mundo **apenas por tools que são endpoints da API .NET** — com as mesmas
validações, o mesmo isolamento por clínica e a mesma auditoria que a tela usa. Isso resolve
segurança e testabilidade de uma vez, e entrega o MCP quase de graça: mesmo contrato de
ferramentas, outro transporte.

### Todo agente nasce em modo sugestão

Cada agente começa com aprovação humana obrigatória (HITL) e só é promovido a autônomo
depois que os dados mostram que ele merece. As decisões de aprovar, editar ou rejeitar
viram o **dataset de eval**. HITL e Evals são o mesmo sistema visto de dois ângulos.

---

## 4. Stack

| Camada | Tecnologia | Observação |
|---|---|---|
| Backend | .NET 10, monolito modular | `backend/Clinica.sln` — formato `.sln` clássico, não `.slnx` |
| Frontend | Next.js 16 (App Router), Tailwind 4 | `frontend/` |
| Auth | Keycloak 26, realm `clinica` | OIDC; tema próprio em `infra/keycloak/themes/clinica` |
| Banco | PostgreSQL 17 + pgvector | um banco só: relacional, JSONB e vetorial |
| Agentes (futuro) | Python + FastAPI | decisão tomada: mercado de IA é Python |
| LLM (futuro) | Anthropic Claude | roteamento S/M/L = Haiku 4.5 / Sonnet 5 / Opus 5 |

**Ambiente local:**

```bash
docker compose -f infra/docker-compose.yml --env-file infra/.env up -d
```

Depois: API pelo Visual Studio (F5) e `npm run dev` no `frontend/`.

- Front: http://localhost:3000 · API: http://localhost:5231 · Keycloak: http://localhost:8080
- Usuários seed (senha `dev123`): `ana.owner`, `carla.doutora`, `bia.secretaria`,
  `sofia.financeiro` (Bella Face) e `rita.owner` (Nova Estética)
- Keycloak admin: `admin` / `admin_dev_only`

> **Armadilha recorrente:** a API rodando (VS ou `dotnet run`) trava os DLLs e faz
> `dotnet build`, `dotnet test` e `dotnet ef` falharem com erro de cópia de arquivo. Pare a
> execução antes. Alternativa: `-p:BaseOutputPath=<temp>` para compilar em outra saída.

---

## 5. Multi-tenancy — a decisão mais importante do backend

Dados de saúde, várias clínicas na mesma instalação. Vazamento entre clínicas não é bug, é
incidente de LGPD. **Três barreiras independentes**, nenhuma dependendo de o desenvolvedor
lembrar de algo:

1. **`tenant_id` vem do claim do token.** Não há fallback para header, query ou body — essa
   ausência é a feature. Se o cliente não pode informar o tenant, não pode pedir o errado.
2. **Filtro global do EF Core**, aplicado por reflexão a toda entidade `TenantEntity`. É um
   laço, não linha por linha: entidade nova nasce filtrada.
3. **RLS no Postgres**, com `FORCE`. Sem `FORCE`, o dono da tabela ignora a policy — e como
   migrations rodam como dono, o RLS pareceria ativo protegendo nada.

Complementos que valem conhecer:

- **Dois papéis de banco:** `clinica_owner` roda migrations, `clinica_app` roda a aplicação.
  A API precisa NÃO ser dona das tabelas para o RLS incidir sobre ela.
- **FKs compostas por `(tenant_id, id)`**: a verificação de chave estrangeira do Postgres
  **ignora RLS**, então uma FK só pelo `id` aceitaria atendimento apontando para paciente de
  outra clínica.
- **`RlsSchemaGuardTests`** varre o schema e falha se qualquer tabela com `tenant_id` ficar
  sem policy. Toda migration que cria tabela `TenantEntity` precisa chamar
  `TenantRls.Enable(migrationBuilder, "tabela")`.
- **Exceção declarada:** `anamnesis_links` fica fora do RLS, porque é a tabela pela qual a
  clínica é *descoberta* quando a paciente abre o link sem login. Está na lista de exceções
  do guard, com justificativa escrita.

ADRs em `docs/adr/`: 0001 multi-tenancy, 0002 identidade dos agentes, 0003 regras críticas
no banco.

---

## 6. Regras de negócio que não são óbvias

Estas foram decididas com o dono do produto e têm teste cobrindo. Mudar qualquer uma exige
conversa, não refatoração.

**Agenda**
- Conflito de horário é **constraint `EXCLUDE` do Postgres**, não consulta prévia. Consultar
  antes tem janela de corrida: duas atendentes ao telefone recebem "está livre" e ambas
  gravam. A API traduz `SQLSTATE 23P01` em 409.
- Intervalo `[)`: atendimento que termina às 17h não conflita com outro que começa às 17h.
- Profissional **sem expediente cadastrado não restringe nada**. Negar tornaria o sistema
  inutilizável no dia da implantação. Com quadro definido, dia fora dele é fechado.
- Atendimento que terminaria após a meia-noite é recusado — limitação conhecida do modelo
  de horas do dia, com teste explícito.
- Doutora vê apenas a própria agenda; quem acumula `DOCTOR` com `OWNER`/`SECRETARY` vê tudo.
  A restrição é do servidor, não da tela.

**Financeiro**
- "Vencido" **não é status gravado**, é conta feita na consulta. Gravar exigiria rotina
  diária, e no dia em que ela falhasse o financeiro veria atraso como se estivesse em dia.
- Dinheiro e cartão entram **efetivados**; só PIX parcelado fica pendente. Sinal em PIX
  nasce pago, parcelas pendentes.
- **Estorno é estado próprio**, não volta para pendente: "nunca entrou" e "entrou e voltou"
  são conversas diferentes. A data do pagamento é preservada para conciliar com a maquininha.
- Parcelamento: R$ 100 em 3× = 33,33 + 33,33 + **33,34**. A diferença vai na última.
- Cancelar protocolo derruba cobranças **pendentes**; o que já foi pago não é tocado.

**Estoque**
- Compra em embalagem (frasco), consumo em conteúdo (ml). Saldo vive em conteúdo.
- **Dois modos**: `Informado` (profissional diz quanto usou — preenchimento, toxina, agulha)
  e `PorAbertura` (baixa a embalagem inteira ao abrir e para de contar — creme, luva).
  Pedir quantidade de algo que ninguém mede produz número inventado, que é pior do que não
  medir porque parece dado.
- Saldo **pode ficar negativo**. Travar impediria a doutora de fechar atendimento com a
  paciente na frente por causa de escrituração.
- A repartição entre embalagens fechadas e sobra aberta é **aproximação declarada**: com
  várias profissionais, 23ml podem estar em dois frascos.
- Não há rastreio por lote/frasco. Decisão consciente: exigiria a doutora apontar o frasco a
  cada aplicação — controle de farmácia, não de clínica.

**Protocolo**
- Item recusado **não é apagado**: vira `Recusado` e fica registrado. É o dado que responde
  "quanto do que se prescreve vira venda?".
- `Recusado` (nunca começou) e `Cancelado` (começou e parou) são estados distintos: um mede
  conversão de proposta, o outro abandono de tratamento.
- Preço e duração são **congelados** no agendamento e na prescrição.

**Anamnese**
- Único caminho em que o tenant **não vem do token** — a paciente não tem login. Quem
  carrega a clínica é o link: 32 bytes aleatórios, validade de 7 dias, uso único.
- A página pública recebe **apenas o primeiro nome**.
- Consentimento de dados é obrigatório; de imagem, opcional.

---

## 7. Convenções

**Backend**
- Minimal API com *endpoint extension*: cada assunto tem `MapXEndpoints`. Sem Controllers.
- Sem Repository e sem MediatR: o `DbContext` já é Unit of Work + Repository. Quando os
  agentes precisarem chamar as mesmas operações que a tela, aí a lógica sai do handler.
- `TreatWarningsAsErrors` ligado. Já pegou vulnerabilidade em dependência transitiva.
- Nomes de domínio e comentários em português; o código, em inglês onde é convenção da
  plataforma.

**Frontend**
- Server Components por padrão. Componentes de cliente só quando há estado de interação.
- **Toda chamada à API sai do servidor do Next**: o access token nunca chega ao navegador,
  e CORS deixa de existir como problema.
- Mutações por Server Actions.
- Sistema de design em `frontend/app/globals.css` (`@theme` do Tailwind 4). **Nenhuma cor
  literal nas telas** — só tokens. Neutros são *quentes*; usar `zinc`/`slate` destoa.
- Sem dark mode, decisão deliberada.
- Mobile funciona, mas o alvo principal é iPad e computador.
- Documentação do design: `docs/design-system.md`.

**Testes**
- Integração com Postgres real via Testcontainers, reproduzindo a separação
  `clinica_owner`/`clinica_app` — com superusuário o RLS seria ignorado e a suíte passaria
  mesmo com policy errada.
- Autenticação nos testes é substituída por headers (`X-Test-Tenant`, `X-Test-User`,
  `X-Test-Roles`). Cobre tenancy, não validação de token.
- Lógica pura (cálculo de parcelas) é testada sem banco.

---

## 8. Lições que custaram tempo

Registradas porque a chance de repetir é alta.

- **Teste que passa pelo motivo errado.** A suíte passou 6/6 falando com o banco de
  *desenvolvimento* em vez do container, porque o override de connection string não vence o
  `appsettings.json` no hosting minimal. Só a CI pegou. Hoje o DbContext é trocado em
  `ConfigureTestServices`.
- **Verificar pelo caminho conveniente.** Aconteceu quatro vezes: testar sem PKCE e concluir
  que o tema do Keycloak não aplicou; testar página autenticada sem sessão; procurar a
  fronteira de erro no HTML quando ela é renderizada no cliente. Sempre verificar pelo
  caminho real do usuário.
- **Mensagem de erro é hipótese, não diagnóstico.** "Papel não existe no realm" na verdade
  era falta de permissão para *ler* o papel.
- **`clientScopes` no import do Keycloak substitui, não acrescenta.** Apagou os scopes
  nativos; o sintoma foi token sem `preferred_username` nem roles.
- **Atributo customizado do Keycloak precisa de `unmanagedAttributePolicy`** desde a versão
  24, senão é descartado silenciosamente.
- **Criar extensão do Postgres não é trabalho de migration** — é provisionamento de banco.
- **O ESLint do Next 16 pega impureza real** (`setState` síncrono em efeito, `Date.now()` no
  render). Corrigir, não silenciar: as duas vezes o conserto revelou um bug de verdade.

---

## 9. Próximo passo pedido: multi-unidade

**Requisito:** uma dona pode ter várias unidades da mesma clínica. Cada unidade tem dias de
funcionamento próprios. É preciso saber quanto cada unidade vendeu e quantas pacientes
atendeu.

### Decisão de modelagem

**Tenant continua sendo a dona/negócio.** A unidade é uma dimensão *dentro* do tenant, não
um tenant novo.

Se cada unidade fosse um tenant, a dona precisaria de cinco logins e nunca veria o
consolidado — que é exatamente o que ela quer. Além disso, a paciente viraria cinco
cadastros distintos.

### O que muda

Nova entidade `Unit` (TenantEntity): nome, endereço, dias/horário de funcionamento, ativa.

Ganham `unit_id` — o que é **físico ou operacional**:

| Entidade | Por quê |
|---|---|
| `Appointment` | onde a paciente foi atendida |
| `StockItem` / `StockMovement` | estoque é físico: o frasco está numa unidade |
| `Payment` | quanto cada unidade vendeu |
| `TreatmentPlan` | onde foi prescrito |
| `WorkSchedule` / `ScheduleException` | a doutora atende terça na unidade A, quinta na B |

**Não** ganham `unit_id`:

| Entidade | Por quê |
|---|---|
| `Patient` | é do negócio; pode ser atendida em qualquer unidade |
| `Procedure` | tabela de preços do negócio (revisar se cada unidade cobrar diferente) |
| `Professional` | a pessoa é uma só; onde ela atende vem do expediente |

### Implicações

- **Escopo por usuário:** recepção da unidade A não deveria ver a agenda da unidade B.
  Sugestão: `Unit` vinculada ao usuário (uma ou várias); `OWNER` vê todas.
- **Seletor de unidade** no cabeçalho, persistido na sessão.
- **Constraint de sobreposição** passa a considerar a unidade? Não: a mesma profissional não
  pode estar em duas unidades ao mesmo tempo, então a regra atual (por profissional)
  continua correta — e agora protege contra um erro novo.
- **FKs compostas** ganham a unidade onde fizer sentido, no mesmo espírito de
  `(tenant_id, id)`.
- **Relatórios por unidade**: agrupar `Payment` e `Appointment` por `unit_id`. É o que
  responde à pergunta original.
- **Migração de dados**: criar uma unidade "Matriz" por tenant e apontar todo o histórico
  para ela. Sem isso, dado existente fica órfão.

### Tamanho

Migration tocando ~8 tabelas, ajuste em quase todos os endpoints, seletor de unidade na
interface e revisão dos testes. **É uma fase inteira**, comparável à Fase G (protocolos).
Não deve ser espremida num commit.

---

## 10. Roadmap de IA (o objetivo original)

Ordem sugerida, com o conceito que cada fase fecha:

1. **Canal + simulador** — evento normalizado; WhatsApp Web e uma página que simula números
   diferentes entram pelo mesmo caminho. Permite desenvolver e testar sem WhatsApp.
2. **Primeiro agente: confirmação de agenda** 🎓 LLM, tool calling, agente
3. **Observabilidade + roteamento de custo** 🎓 OpenTelemetry, Langfuse, cascata S/M/L
4. **Guardrails + HITL** 🎓 filas por papel: doutoras, secretaria, financeiro
5. **RAG** 🎓 pgvector, protocolos e FAQ por clínica, citação de fonte
6. **Pós-procedimento e cobrança** — o campo `FollowUpAt` já é gravado no fechamento do
   atendimento, esperando este agente
7. **Evals** 🎓 golden sets alimentados pelo log de HITL
8. **MCP** 🎓 as mesmas tools por outro transporte

**Style Profile** é o diferencial de produto: tom, formalidade, emojis, saudação e 6–12
mensagens reais da doutora/secretária como few-shot, por clínica e por papel. É o que faz o
agente soar como a clínica em vez de soar como um bot. **Coletar essas mensagens é tarefa de
calendário, não de código** — precisa começar antes da fase que as usa.

---

## 11. Pendências conhecidas

- Endpoints de equipe (`/team`) não têm teste automatizado — exigiria Keycloak em container.
- Sem tela de troca de senha: a dona define a senha e entrega em mãos.
- Sem SMTP: "esqueci minha senha" mostra o formulário e o e-mail nunca chega.
- Sem filtros e ordenação nas tabelas (a estrutura paginada já está pronta).
- Sem busca global.
- Tokens de cor duplicados entre `frontend/app/globals.css` e o tema do Keycloak — o
  Keycloak é aplicação Java separada. Mudou identidade visual, mude nos dois.
- Logo em PNG; vale pedir SVG ao designer (10 KB contra 160 KB, e recolorível por CSS).
- `PLANO.md` está defasado: várias fases mudaram de escopo durante a execução.
