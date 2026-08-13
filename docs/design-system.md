# Sistema de design

Guia vivo em **http://localhost:3000/design** — paleta, tipografia e todos os componentes
numa tela só. Alterou token? Confira ali antes de sair aplicando nas telas.

## A ideia em uma frase

Branco dominante, neutros **quentes**, um acento rosé e muito respiro. A serifa nos títulos
é o que carrega a elegância — sem ela o resultado fica clean, mas genérico.

## Regras que evitam inconsistência

**Nunca escreva cor literal.** Sem `text-[#9C5566]`, sem `bg-zinc-50`. Só tokens
(`text-primary`, `bg-surface`). Os tokens vivem no `@theme` de
[app/globals.css](../frontend/app/globals.css); é o único lugar que muda quando a
identidade visual mudar.

**Os neutros são quentes, não cinzas.** `surface` é `#FDFBFA`, não branco puro; `border` puxa
para o bege. Usar `zinc`/`slate` do Tailwind ao lado disso destoa na hora — o olho percebe
o cinza azulado brigando com o rosé mesmo sem saber nomear.

**Hierarquia de texto, três níveis e só:**
- `text-ink` — conteúdo de verdade (nome da paciente, valor, título)
- `text-ink-muted` — apoio e contexto (descrições, células secundárias)
- `text-ink-subtle` — metadado e rótulo (cabeçalho de tabela, data de cadastro)

**O primário é escuro de propósito.** `#9C5566` com texto branco dá 5.2:1, passando em
WCAG AA. Rosé mais claro fica bonito no Figma e ilegível na recepção com luz do dia batendo
na tela. Se precisar de rosa claro, é fundo (`primary-soft`), nunca texto.

**Serifa só em título.** `font-display` em `h1`/`h2`/`h3` e em elementos decorativos curtos
(as iniciais no avatar). Em tabela e formulário, sans — serifa em texto pequeno e denso
cansa a leitura.

## Componentes

| Componente | Arquivo |
|---|---|
| `Button` (primary/secondary/ghost/danger) | `components/ui/button.tsx` |
| `Card`, `CardHeader`, `CardBody`, `EmptyState` | `components/ui/card.tsx` |
| `Badge` (6 tons) | `components/ui/badge.tsx` |
| `Field`, `Input`, `Select` | `components/ui/field.tsx` |
| `AppShell`, `PageHeader` | `components/app-shell.tsx` |

`EmptyState` merece atenção: num sistema recém-implantado quase toda tela começa vazia, e é
a primeira impressão que a clínica tem do produto.

## A tela de login vive em outro lugar

O login é servido pelo **Keycloak**, não pelo Next — e isso é proposital. A alternativa
(coletar e-mail e senha numa tela nossa e postar no Keycloak) faria a senha trafegar pela
nossa aplicação e jogaria fora, de graça, o reset de senha, o 2FA, a proteção contra força
bruta e o controle de sessão.

Para a tela ter a nossa cara mesmo assim, existe um tema próprio em
`infra/keycloak/themes/clinica/`. Ele usa `parent=base`: herdamos os templates de todos os
fluxos (reset, OTP, verificação de e-mail, erro) e sobrescrevemos apenas o `template.ftl`,
que é a casca da página, mais o CSS.

> ⚠️ **Os tokens estão duplicados.** `infra/keycloak/themes/clinica/login/resources/css/clinica.css`
> repete os valores de `frontend/app/globals.css`. O Keycloak é uma aplicação Java separada,
> sem acesso ao build do Tailwind — não há como compartilhar. **Mudou a identidade visual,
> mude nos dois lugares.**

Em desenvolvimento o cache de temas está desligado no `docker-compose.yml`; edite o CSS e
recarregue a página. Em produção, ligue de volta.

## Decisões deliberadas

**Sem dark mode.** O pedido é um sistema branco e clean. Um segundo tema dobraria o custo de
revisão de cada tela sem servir a ninguém dentro de uma clínica.

**Seções não construídas aparecem esmaecidas** na navegação, em vez de escondidas. A equipe
enxerga para onde o sistema vai, e ninguém clica num link que dá 404.

**Foco visível no tom da marca.** `:focus-visible` com contorno rosé. A secretária opera no
teclado o dia inteiro; sumir com o foco para "ficar limpo" é hostil.
