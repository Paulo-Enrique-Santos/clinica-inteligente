import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody } from "@/components/ui/card";
import { Field, Input, Select } from "@/components/ui/field";
import { Formulario } from "@/components/ui/form";
import { criarItemDeEstoque } from "@/lib/actions/estoque";

export default async function NovoItemPage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  if (!session.roles.some((r) => r === "OWNER" || r === "FINANCE")) {
    redirect("/estoque");
  }

  return (
    <AppShell
      atual="/estoque"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader titulo="Novo item de estoque" />

      <Card className="max-w-lg">
        <CardBody>
          <Formulario
            acao={criarItemDeEstoque}
            cancelarHref="/estoque"
            rotuloEnviar="Cadastrar"
          >
            <Field label="Nome" htmlFor="nome">
              <Input
                id="nome"
                name="nome"
                required
                autoFocus
                placeholder="Toxina botulínica"
              />
            </Field>

            <div className="grid grid-cols-2 gap-4">
              <Field
                label="Compra em"
                htmlFor="embalagem"
                hint="Como chega do fornecedor."
              >
                <Input id="embalagem" name="embalagem" required placeholder="frasco" />
              </Field>

              <Field label="Consome em" htmlFor="unidade" hint="Como é aplicado.">
                <Input id="unidade" name="unidade" required placeholder="ml" />
              </Field>
            </div>

            <Field
              label="Conteúdo por embalagem"
              htmlFor="conteudo"
              hint="Um frasco de 10 ml? Escreva 10. Uma caixa com 50 pares? Escreva 50."
            >
              <Input
                id="conteudo"
                name="conteudo"
                inputMode="decimal"
                defaultValue="1"
                required
              />
            </Field>

            <Field
              label="Como controlar o consumo"
              htmlFor="modo"
              hint="Escolha pela realidade da clínica, não pelo ideal: pedir quantidade de algo que ninguém mede produz número inventado."
            >
              <Select id="modo" name="modo" defaultValue="Informado">
                <option value="Informado">
                  A profissional informa quanto usou (preenchimento, toxina, agulha)
                </option>
                <option value="PorAbertura">
                  Baixa a embalagem inteira ao abrir (creme, luva)
                </option>
              </Select>
            </Field>

            <Field
              label="Quantidade mínima"
              htmlFor="minimo"
              hint="Na unidade de consumo. Abaixo disto, acende alerta."
            >
              <Input id="minimo" name="minimo" inputMode="decimal" defaultValue="0" />
            </Field>
          </Formulario>
        </CardBody>
      </Card>
    </AppShell>
  );
}
