import Link from "next/link";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { apiFetch, type Profissional } from "@/lib/api";
import { data as formatarData } from "@/lib/formato";
import { salvarExcecao, removerExcecao } from "@/lib/actions/expediente";
import { EditorDeExpediente } from "./editor";
import { AppShell, PageHeader } from "@/components/app-shell";
import { Card, CardHeader, CardBody, EmptyState } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Field, Input } from "@/components/ui/field";
import { cn } from "@/lib/cn";

type Expediente = {
  semana: {
    dayOfWeek: number;
    startsAt: string;
    endsAt: string;
    breakStartsAt: string | null;
    breakEndsAt: string | null;
  }[];
  excecoes: {
    id: string;
    date: string;
    closed: boolean;
    startsAt: string | null;
    endsAt: string | null;
    reason: string | null;
  }[];
};

/** "09:00:00" -> "09:00" */
const hhmm = (v: string | null) => v?.slice(0, 5) ?? "";

export default async function ExpedientePage(props: PageProps<"/expediente">) {
  const session = await auth();

  if (!session || session.error) {
    redirect("/");
  }

  const profissionais = await apiFetch<Profissional[]>("/professionals");
  const params = await props.searchParams;
  const escolhida = typeof params.prof === "string" ? params.prof : profissionais[0]?.id;

  const usuario = session.user?.name ?? session.user?.email ?? "—";

  if (profissionais.length === 0 || !escolhida) {
    return (
      <AppShell atual="/agenda" usuario={usuario} papeis={session.roles}>
        <PageHeader titulo="Expediente" />
        <EmptyState
          title="Nenhuma profissional cadastrada"
          description="Cadastre as profissionais em Configurações para definir os horários de atendimento."
        />
      </AppShell>
    );
  }

  const expediente = await apiFetch<Expediente>(`/professionals/${escolhida}/schedule`);

  return (
    <AppShell atual="/agenda" usuario={usuario} papeis={session.roles}>
      <PageHeader
        titulo="Expediente"
        descricao="Define quais horários o sistema oferece ao agendar. Sem expediente, qualquer horário é aceito."
      />

      {profissionais.length > 1 && (
        <div className="mb-5 flex flex-wrap gap-1">
          {profissionais.map((p) => (
            <Link
              key={p.id}
              href={`/expediente?prof=${p.id}`}
              className={cn(
                "rounded-control px-3 py-1.5 text-sm transition-colors",
                p.id === escolhida
                  ? "bg-primary-soft font-medium text-primary"
                  : "text-ink-muted hover:bg-surface-muted hover:text-ink",
              )}
            >
              {p.displayName}
            </Link>
          ))}
        </div>
      )}

      <Card className="mb-8">
        <CardHeader
          title="Semana padrão"
          description="Define os horários que o sistema oferece ao agendar."
        />
        <CardBody>
          <EditorDeExpediente
            profissional={escolhida}
            inicial={Object.fromEntries(
              expediente.semana.map((d) => [
                d.dayOfWeek,
                {
                  inicio: hhmm(d.startsAt),
                  fim: hhmm(d.endsAt),
                  almocoInicio: hhmm(d.breakStartsAt),
                  almocoFim: hhmm(d.breakEndsAt),
                },
              ]),
            )}
          />
        </CardBody>
      </Card>

      <Card>
        <CardHeader
          title="Exceções"
          description="O dia que foge do padrão: folga, congresso, saída mais cedo."
        />
        <CardBody className="space-y-5">
          {expediente.excecoes.length > 0 && (
            <div className="space-y-2">
              {expediente.excecoes.map((e) => (
                <div
                  key={e.id}
                  className="flex flex-wrap items-center justify-between gap-3 rounded-control border border-border px-4 py-3"
                >
                  <div className="flex items-center gap-3">
                    <span className="text-sm text-ink">{formatarData(e.date)}</span>
                    {e.closed ? (
                      <Badge tone="danger">Não atende</Badge>
                    ) : (
                      <Badge tone="primary">
                        {hhmm(e.startsAt)} às {hhmm(e.endsAt)}
                      </Badge>
                    )}
                    {e.reason && <span className="text-xs text-ink-subtle">{e.reason}</span>}
                  </div>

                  <form action={removerExcecao}>
                    <input type="hidden" name="profissional" value={escolhida} />
                    <input type="hidden" name="id" value={e.id} />
                    <Button type="submit" size="sm" variant="ghost">
                      Remover
                    </Button>
                  </form>
                </div>
              ))}
            </div>
          )}

          <form action={salvarExcecao} className="flex flex-wrap items-end gap-3">
            <input type="hidden" name="profissional" value={escolhida} />

            <div className="w-40">
              <Field label="Data" htmlFor="data-excecao">
                <Input id="data-excecao" name="data" type="date" required />
              </Field>
            </div>

            <label className="flex items-center gap-2 pb-2 text-sm text-ink">
              <input type="checkbox" name="fechado" className="accent-primary" />
              Não atende
            </label>

            <div className="w-28">
              <Field label="Início" htmlFor="inicio-excecao">
                <Input id="inicio-excecao" name="inicio" type="time" />
              </Field>
            </div>

            <div className="w-28">
              <Field label="Fim" htmlFor="fim-excecao">
                <Input id="fim-excecao" name="fim" type="time" />
              </Field>
            </div>

            <div className="min-w-40 flex-1">
              <Field label="Motivo" htmlFor="motivo-excecao">
                <Input id="motivo-excecao" name="motivo" placeholder="Congresso" />
              </Field>
            </div>

            <Button type="submit" variant="secondary">
              Adicionar
            </Button>
          </form>
        </CardBody>
      </Card>
    </AppShell>
  );
}
