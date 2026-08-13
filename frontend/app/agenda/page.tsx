import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, apiFetchOrNull, type Atendimento, type Profissional } from "@/lib/api";
import { cn } from "@/lib/cn";
import { data as formatarData, hora, reais } from "@/lib/formato";
import { AgendaSemana } from "@/components/agenda-semana";
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

/** Mantém a profissional escolhida e a visão ao navegar entre os dias. */
function filtroNaUrl(prof: string, vista?: boolean) {
  return `${prof ? `&prof=${prof}` : ""}${vista ? "&vista=semana" : ""}`;
}

/** Domingo a sábado da semana que contém o dia informado. */
function semanaDe(dia: string) {
  const d = new Date(`${dia}T12:00:00Z`);
  const domingo = new Date(d);
  domingo.setUTCDate(d.getUTCDate() - d.getUTCDay());

  return Array.from({ length: 7 }, (_, i) => {
    const x = new Date(domingo);
    x.setUTCDate(domingo.getUTCDate() + i);
    return x.toISOString().slice(0, 10);
  });
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
  const profFiltro = typeof params.prof === "string" ? params.prof : "";
  const semana = params.vista === "semana";

  // Quem só atende vê a própria agenda; quem organiza a agenda da clínica escolhe.
  // Esta variável controla apenas a INTERFACE — a restrição de verdade é aplicada
  // no servidor, e não some se alguém mexer na query string.
  const soPropriaAgenda =
    session.roles.includes("DOCTOR") &&
    !session.roles.includes("OWNER") &&
    !session.roles.includes("SECRETARY");

  // Na visão semanal a janela vai de domingo a sábado; na diária, só o dia.
  const diasDaSemana = semanaDe(dia);
  const primeiroDia = semana ? diasDaSemana[0] : dia;
  const ultimoDia = semana ? deslocar(diasDaSemana[6], 1) : deslocar(dia, 1);

  // A API recebe a janela no fuso da clínica.
  const de = `${primeiroDia}T00:00:00-03:00`;
  const ate = `${ultimoDia}T00:00:00-03:00`;
  const filtro = !soPropriaAgenda && profFiltro ? `&profissionalId=${profFiltro}` : "";

  const [agenda, profissionais, minhaFicha] = await Promise.all([
    apiFetch<Atendimento[]>(
      `/appointments?de=${encodeURIComponent(de)}&ate=${encodeURIComponent(ate)}${filtro}`,
    ),
    soPropriaAgenda
      ? Promise.resolve([] as Profissional[])
      : apiFetch<Profissional[]>("/professionals"),
    soPropriaAgenda
      ? apiFetchOrNull<Profissional>("/professionals/me")
      : Promise.resolve(null),
  ]);

  const podeAgendar = session.roles.some((r) => r === "OWNER" || r === "SECRETARY");
  const ativos = agenda.filter((a) => a.status !== "Cancelado");

  // Doutora com login ainda não vinculado a uma ficha: a agenda vem vazia por
  // definição. Dizer o motivo evita que ela ache que o sistema perdeu os dados.
  if (soPropriaAgenda && minhaFicha === null) {
    return (
      <AppShell
        atual="/agenda"
        usuario={session.user?.name ?? session.user?.email ?? "—"}
        papeis={session.roles}
      >
        <PageHeader titulo="Agenda" />
        <EmptyState
          title="Seu login ainda não está vinculado a uma profissional"
          description="Peça à responsável pela clínica para ligar o seu acesso à sua ficha em Configurações. Enquanto isso, a agenda aparece vazia."
        />
      </AppShell>
    );
  }

  return (
    <AppShell
      atual="/agenda"
      usuario={session.user?.name ?? session.user?.email ?? "—"}
      papeis={session.roles}
    >
      <PageHeader
        titulo={soPropriaAgenda ? "Minha agenda" : "Agenda"}
        descricao={
          semana
            ? `Semana de ${formatarData(diasDaSemana[0])} a ${formatarData(diasDaSemana[6])} · ${ativos.length} atendimento${ativos.length === 1 ? "" : "s"}`
            : `${porExtenso(dia)} · ${ativos.length} atendimento${ativos.length === 1 ? "" : "s"}`
        }
        acao={
          podeAgendar ? (
            <Link href={`/agenda/novo?dia=${dia}`}>
              <Button>Novo agendamento</Button>
            </Link>
          ) : undefined
        }
      />

      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <Link
            href={`/agenda?dia=${deslocar(dia, semana ? -7 : -1)}${filtroNaUrl(profFiltro, semana)}`}
          >
            <Button variant="secondary" size="sm">
              ← Anterior
            </Button>
          </Link>
          <Link href={`/agenda?dia=${hoje()}${filtroNaUrl(profFiltro, semana)}`}>
            <Button variant="ghost" size="sm">
              Hoje
            </Button>
          </Link>
          <Link
            href={`/agenda?dia=${deslocar(dia, semana ? 7 : 1)}${filtroNaUrl(profFiltro, semana)}`}
          >
            <Button variant="secondary" size="sm">
              Próximo →
            </Button>
          </Link>
        </div>

        {/* Dia ou semana: a recepção quer a lista do dia; a dona quer enxergar a
            ocupação da semana. São perguntas diferentes sobre o mesmo dado. */}
        <div className="flex rounded-full border border-border bg-canvas p-0.5">
          {[
            { rotulo: "Dia", ativo: !semana, href: `/agenda?dia=${dia}${filtroNaUrl(profFiltro)}` },
            {
              rotulo: "Semana",
              ativo: semana,
              href: `/agenda?dia=${dia}${filtroNaUrl(profFiltro, true)}`,
            },
          ].map((v) => (
            <Link
              key={v.rotulo}
              href={v.href}
              className={cn(
                "rounded-full px-4 py-1 text-sm transition-colors",
                v.ativo ? "bg-primary-soft font-medium text-primary" : "text-ink-muted hover:text-ink",
              )}
            >
              {v.rotulo}
            </Link>
          ))}
        </div>
      </div>

      {!soPropriaAgenda && profissionais.length > 0 && (
        <div className="mb-5 flex flex-wrap gap-1">
          <Link
            href={`/agenda?dia=${dia}`}
            className={cn(
              "rounded-control px-3 py-1.5 text-sm transition-colors",
              profFiltro === ""
                ? "bg-primary-soft font-medium text-primary"
                : "text-ink-muted hover:bg-surface-muted hover:text-ink",
            )}
          >
            Todas
          </Link>
          {profissionais.map((p) => (
            <Link
              key={p.id}
              href={`/agenda?dia=${dia}&prof=${p.id}`}
              className={cn(
                "rounded-control px-3 py-1.5 text-sm transition-colors",
                profFiltro === p.id
                  ? "bg-primary-soft font-medium text-primary"
                  : "text-ink-muted hover:bg-surface-muted hover:text-ink",
              )}
            >
              {p.displayName}
            </Link>
          ))}
        </div>
      )}

      {semana ? (
        <AgendaSemana
          dias={diasDaSemana}
          atendimentos={agenda}
          diaDestacado={dia}
          podeAgendar={podeAgendar}
        />
      ) : agenda.length === 0 ? (
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
