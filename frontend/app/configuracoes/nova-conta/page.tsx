import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { criarMembroDaEquipe } from "@/lib/actions/equipe";
import { PAPEIS } from "@/lib/permissoes";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/field";
import { Formulario } from "@/components/ui/form";
import { Alert } from "@/components/ui/alert";

// OWNER fica de fora: quem paga pelo produto recebe esse acesso no onboarding da
// clínica. Permitir criar outra dona pela tela deixaria alguém se promover sozinho.
const PAPEIS_CONCEDIVEIS = PAPEIS.filter((p) => p.chave !== "OWNER");

export default async function NovaContaPage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  if (!session.roles.includes("OWNER")) {
    redirect("/agenda");
  }

  return (
    <AppShell
      atual="/configuracoes"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Nova conta"
        descricao="A pessoa entra com o usuário e a senha que você definir aqui."
      />

      <Card className="max-w-lg">
        <CardBody>
          <Alert tone="info" className="mb-5">
            Anote a senha e entregue à pessoa. Ainda não existe tela para ela trocar a
            senha sozinha — isso entra numa próxima etapa.
          </Alert>

          <Formulario
            acao={criarMembroDaEquipe}
            cancelarHref="/configuracoes"
            rotuloEnviar="Criar conta"
          >
            <div className="grid grid-cols-2 gap-4">
              <Field label="Nome" htmlFor="nome">
                <Input id="nome" name="nome" required autoFocus placeholder="Carla" />
              </Field>

              <Field label="Sobrenome" htmlFor="sobrenome">
                <Input id="sobrenome" name="sobrenome" required placeholder="Menezes" />
              </Field>
            </div>

            <Field
              label="Usuário"
              htmlFor="usuario"
              hint="É com isto que ela entra no sistema. Sem espaços."
            >
              <Input id="usuario" name="usuario" required placeholder="carla.menezes" />
            </Field>

            <Field label="E-mail" htmlFor="email" hint="Opcional por enquanto.">
              <Input id="email" name="email" type="email" placeholder="carla@clinica.com.br" />
            </Field>

            <Field
              label="Senha"
              htmlFor="senha"
              hint="Mínimo de 8 caracteres. Combine algo e avise a pessoa."
            >
              <Input id="senha" name="senha" required minLength={8} />
            </Field>

            <Field label="Papel" htmlFor="papel">
              <Select id="papel" name="papel" required defaultValue="">
                <option value="" disabled>
                  Selecione
                </option>
                {PAPEIS_CONCEDIVEIS.map((p) => (
                  <option key={p.chave} value={p.chave}>
                    {p.nome} — {p.descricao}
                  </option>
                ))}
              </Select>
            </Field>

            <div className="rounded-control border border-border bg-surface px-4 py-3">
              <p className="text-xs text-ink-muted">
                Os dois campos abaixo só valem para <strong>doutora</strong>: o sistema
                cria a ficha de profissional junto e a liga a este login, para a agenda
                dela funcionar já no primeiro acesso.
              </p>

              <div className="mt-3 grid grid-cols-2 gap-4">
                <Field label="Como aparece" htmlFor="nomeExibicao">
                  <Input id="nomeExibicao" name="nomeExibicao" placeholder="Dra. Carla" />
                </Field>

                <Field label="Especialidade" htmlFor="especialidade">
                  <Input id="especialidade" name="especialidade" placeholder="Harmonização" />
                </Field>
              </div>
            </div>
          </Formulario>
        </CardBody>
      </Card>
    </AppShell>
  );
}
