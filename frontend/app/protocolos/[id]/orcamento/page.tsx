import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Protocolo } from "@/lib/api";
import { hojeNaClinica } from "@/lib/formato";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardBody } from "@/components/ui/card";
import { FormularioDeOrcamento } from "./form";

export default async function OrcamentoPage(props: PageProps<"/protocolos/[id]/orcamento">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  // Fechar valor é da recepção e do financeiro. A doutora prescreve.
  if (!session.roles.some((r) => r === "OWNER" || r === "SECRETARY" || r === "FINANCE")) {
    redirect("/protocolos");
  }

  const { id } = await props.params;
  const protocolos = await apiFetch<Protocolo[]>("/treatment-plans");
  const protocolo = protocolos.find((p) => p.id === id);

  if (!protocolo || protocolo.status !== "Proposto") {
    redirect("/protocolos");
  }

  return (
    <AppShell
      atual="/protocolos"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Fechar orçamento"
        descricao={`${protocolo.patientName} · prescrito por ${protocolo.professionalName}`}
      />

      <Card className="max-w-2xl">
        <CardBody>
          {protocolo.notes && (
            <div className="mb-5 rounded-control bg-surface px-4 py-3 text-sm italic text-ink-muted">
              {protocolo.notes}
            </div>
          )}

          <FormularioDeOrcamento
            protocolo={protocolo.id}
            itens={protocolo.items}
            hoje={hojeNaClinica()}
          />
        </CardBody>
      </Card>

      <p className="mt-6 max-w-2xl text-xs leading-relaxed text-ink-subtle">
        Desmarcar um procedimento não o apaga: ele fica registrado como recusado, e a
        prescrição da doutora continua no histórico da paciente.
      </p>
    </AppShell>
  );
}
