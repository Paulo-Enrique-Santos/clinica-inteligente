import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Atendimento } from "@/lib/api";
import { hora, reais } from "@/lib/formato";
import { formatarTelefone } from "@/lib/telefone";
import { alterarStatus } from "@/lib/actions/agenda";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

const TOM_POR_STATUS = {
  Agendado: "neutral",
  Confirmado: "primary",
  Realizado: "success",
  Faltou: "warning",
  Cancelado: "danger",
} as const;

function ehDataValida(valor: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(valor) && !Number.isNaN(Date.parse(valor));
}

function hoje() {
  // Fuso da clínica (-03:00): usar a data do servidor mostraria o dia errado durante
  // a madrugada se a aplicação rodar em UTC.
  const agora = new Date(Date.now() - 3 * 60 * 60 * 1000);
  return agora.toISOString().slice(0, 10);
}

function deslocar(dia: string, dias: number) {
  const d = new Date(`${dia}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + dias);
  return d.toISOString().slice(0, 10);
}

function porExtenso(dia: string) {
  return new Date(`${dia}T12:00:00Z`).toLocaleDateString("pt-BR", {
    weekday: "long",
    day: "2-digit",
    month: "long",
    timeZone: "UTC",
  });
}

export default async function AgendaPage(props: PageProps<"/agenda">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const params = await props.searchParams;
  const bruto = typeof params.dia === "string" ? params.dia : "";
  const dia = ehDataValida(bruto) ? bruto : hoje();

  // A API recebe a janela do dia inteiro no fuso da clínica.
  const de = `${dia}T00:00:00-03:00`;
  const ate = `${deslocar(dia, 1)}T00:00:00-03:00`;

  const agenda = await apiFetch<Atendimento[]>(
    `/appointments?de=${encodeURIComponent(de)}&ate=${encodeURIComponent(ate)}`,
  );

  const podeAgendar = session.roles.some((r) => r === "OWNER" || r === "SECRETARY");
  const ativos = agenda.filter((a) => a.status !== "Cancelado");

  return (
    <AppShell
      atual="/agenda"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo="Agenda"
        descricao={`${porExtenso(dia)} · ${ativos.length} atendimento${ativos.length === 1 ? "" : "s"}`}
        acao={
          podeAgendar ? (
            <Link href={`/agenda/novo?dia=${dia}`}>
              <Button>Novo agendamento</Button>
            </Link>
          ) : undefined
        }
      />

      <div className="mb-5 flex items-center gap-2">
        <Link href={`/agenda?dia=${deslocar(dia, -1)}`}>
          <Button variant="secondary" size="sm">
            ← Anterior
          </Button>
        </Link>
        <Link href={`/agenda?dia=${hoje()}`}>
          <Button variant="ghost" size="sm">
            Hoje
          </Button>
        </Link>
        <Link href={`/agenda?dia=${deslocar(dia, 1)}`}>
          <Button variant="secondary" size="sm">
            Próximo →
          </Button>
        </Link>
      </div>

      {agenda.length === 0 ? (
        <EmptyState
          title="Nenhum atendimento neste dia"
          description="Use as setas para navegar entre os dias ou agende um novo atendimento."
          action={
            podeAgendar ? (
              <Link href={`/agenda/novo?dia=${dia}`}>
                <Button>Novo agendamento</Button>
              </Link>
            ) : undefined
          }
        />
      ) : (
        <div className="space-y-3">
          {agenda.map((a) => (
            <Card key={a.id} className="px-5 py-4">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="flex gap-4">
                  {/* O horário é a informação que a recepção procura primeiro na
                      tela, então ganha peso visual próprio. */}
                  <div className="w-16 shrink-0">
                    <p className="font-display text-lg text-ink">{hora(a.startsAt)}</p>
                    <p className="text-xs text-ink-subtle">{hora(a.endsAt)}</p>
                  </div>

                  <div>
                    <p className="text-ink">{a.patientName}</p>
                    <p className="mt-0.5 text-sm text-ink-muted">
                      {a.procedureName} · {a.professionalName}
                    </p>
                    <p className="mt-1 text-xs text-ink-subtle">
                      {formatarTelefone(a.patientPhone)} · {reais(a.price)}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <Badge
                    tone={TOM_POR_STATUS[a.status as keyof typeof TOM_POR_STATUS] ?? "neutral"}
                  >
                    {a.status}
                  </Badge>

                  {podeAgendar && a.status === "Agendado" && (
                    <BotaoStatus id={a.id} dia={dia} status="Confirmado" rotulo="Confirmar" />
                  )}
                  {podeAgendar && (a.status === "Agendado" || a.status === "Confirmado") && (
                    <>
                      <BotaoStatus
                        id={a.id}
                        dia={dia}
                        status="Realizado"
                        rotulo="Realizado"
                        variante="secondary"
                      />
                      <BotaoStatus
                        id={a.id}
                        dia={dia}
                        status="Faltou"
                        rotulo="Faltou"
                        variante="ghost"
                      />
                    </>
                  )}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </AppShell>
  );
}

function BotaoStatus({
  id,
  dia,
  status,
  rotulo,
  variante = "primary",
}: {
  id: string;
  dia: string;
  status: string;
  rotulo: string;
  variante?: "primary" | "secondary" | "ghost";
}) {
  return (
    <form action={alterarStatus}>
      <input type="hidden" name="id" value={id} />
      <input type="hidden" name="dia" value={dia} />
      <input type="hidden" name="status" value={status} />
      <Button type="submit" size="sm" variant={variante}>
        {rotulo}
      </Button>
    </form>
  );
}
