import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody } from "@/components/ui/card";
import { Field, Input } from "@/components/ui/field";
import { Formulario } from "@/components/ui/form";
import { criarProcedimento } from "@/lib/actions/procedimentos";

export default async function NovoProcedimentoPage() {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  // Preço e custo são decisão da dona da clínica, não da recepção.
  if (!session.roles.includes("OWNER")) {
    redirect("/procedimentos");
  }

  return (
    <AppShell
      atual="/procedimentos"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader titulo="Novo procedimento" />

      <Card className="max-w-lg">
        <CardBody>
          <Formulario
            acao={criarProcedimento}
            cancelarHref="/procedimentos"
            rotuloEnviar="Cadastrar"
          >
            <Field label="Nome" htmlFor="nome">
              <Input id="nome" name="nome" required autoFocus placeholder="Limpeza de pele" />
            </Field>

            <Field
              label="Duração (minutos)"
              htmlFor="duracao"
              hint="Define o tamanho do bloco na agenda."
            >
              <Input
                id="duracao"
                name="duracao"
                type="number"
                min={5}
                max={480}
                step={5}
                defaultValue={60}
                required
              />
            </Field>

            <div className="grid grid-cols-2 gap-4">
              <Field label="Preço" htmlFor="preco">
                <Input id="preco" name="preco" inputMode="decimal" placeholder="250,00" required />
              </Field>

              <Field
                label="Custo de insumos"
                htmlFor="custo"
                hint="Preço menos isto é a margem."
              >
                <Input id="custo" name="custo" inputMode="decimal" placeholder="40,00" />
              </Field>
            </div>

            <Field label="Descrição" htmlFor="descricao">
              <Input id="descricao" name="descricao" placeholder="Opcional" />
            </Field>
          </Formulario>
        </CardBody>
      </Card>
    </AppShell>
  );
}
