import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
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
              <Field label="Unidade" htmlFor="unidade" hint="ml, un, g, caixa">
                <Input id="unidade" name="unidade" required placeholder="un" />
              </Field>

              <Field
                label="Quantidade mínima"
                htmlFor="minimo"
                hint="Abaixo disto, acende alerta."
              >
                <Input id="minimo" name="minimo" inputMode="decimal" defaultValue="0" />
              </Field>
            </div>
          </Formulario>
        </CardBody>
      </Card>
    </AppShell>
  );
}
