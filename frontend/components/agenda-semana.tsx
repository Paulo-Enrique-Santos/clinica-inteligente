import Link from "next/link";
import type { Atendimento } from "@/lib/api";
import { hora } from "@/lib/formato";
import { cn } from "@/lib/cn";

/** Altura de um minuto na grade. 1,1px dá ~13h visíveis sem rolagem em tela cheia. */
const PX_POR_MINUTO = 1.1;

const CORES: Record<string, string> = {
  Agendado: "bg-surface-muted border-l-border-strong text-ink",
  Confirmado: "bg-primary-soft border-l-primary text-ink",
  Realizado: "bg-success-soft border-l-success text-ink",
  Faltou: "bg-warning-soft border-l-warning text-ink",
  Cancelado: "bg-danger-soft border-l-danger text-ink-muted line-through",
};

const DIAS_CURTOS = ["DOM", "SEG", "TER", "QUA", "QUI", "SEX", "SÁB"];

type Props = {
  dias: string[];
  atendimentos: Atendimento[];
  diaDestacado: string;
  podeAgendar: boolean;
  /** Janela real de atendimento da clínica. Nula quando ninguém definiu expediente. */
  janela: { inicio: string | null; fim: string | null };
};

function emMinutos(hhmm: string) {
  const [h, m] = hhmm.split(":").map(Number);
  return h * 60 + m;
}

/** Minutos desde a meia-noite, no fuso da clínica. */
function minutosDoDia(iso: string) {
  const d = new Date(iso);
  const local = new Date(d.getTime() - 3 * 60 * 60 * 1000);
  return local.getUTCHours() * 60 + local.getUTCMinutes();
}

function diaDoAtendimento(iso: string) {
  const d = new Date(iso);
  return new Date(d.getTime() - 3 * 60 * 60 * 1000).toISOString().slice(0, 10);
}

export function AgendaSemana({
  dias,
  atendimentos,
  diaDestacado,
  podeAgendar,
  janela,
}: Props) {
  const ativos = atendimentos.filter((a) => a.status !== "Cancelado");

  // A grade começa e termina no expediente da clínica, não num palpite. Numa clínica
  // que abre às 9h, desenhar a partir das 8h custa uma faixa morta em todo dia de uso.
  //
  // Sem expediente definido, cai para 8h–18h; e atendimento fora da janela (encaixe,
  // ou marcado antes de o expediente existir) estica a grade para caber — some-lo da
  // tela seria pior do que a faixa extra.
  const inicios = ativos.map((a) => minutosDoDia(a.startsAt));
  const fins = ativos.map((a) => minutosDoDia(a.endsAt));

  const baseInicio = janela.inicio ? emMinutos(janela.inicio) : 8 * 60;
  const baseFim = janela.fim ? emMinutos(janela.fim) : 18 * 60;

  const inicioGrade = Math.max(0, Math.min(baseInicio, ...(inicios.length ? inicios : [baseInicio])));
  const fimGrade = Math.min(24 * 60, Math.max(baseFim, ...(fins.length ? fins : [baseFim])));

  const altura = (fimGrade - inicioGrade) * PX_POR_MINUTO;

  const horasCheias: number[] = [];
  for (let m = Math.ceil(inicioGrade / 60) * 60; m < fimGrade; m += 60) {
    horasCheias.push(m);
  }

  return (
    <div className="overflow-x-auto rounded-card border border-border bg-canvas">
      <div className="min-w-3xl">
        {/* Cabeçalho dos dias */}
        <div className="grid grid-cols-[3.5rem_repeat(7,minmax(7rem,1fr))] border-b border-border">
          <div />
          {dias.map((d) => {
            const data = new Date(`${d}T12:00:00Z`);
            const destaque = d === diaDestacado;

            return (
              <Link
                key={d}
                href={`/agenda?dia=${d}`}
                className="group border-l border-border px-2 py-3 text-center transition-colors hover:bg-surface"
              >
                <p className="text-[0.65rem] font-medium uppercase tracking-wider text-ink-subtle">
                  {DIAS_CURTOS[data.getUTCDay()]}
                </p>
                <p
                  className={cn(
                    "mx-auto mt-1 flex h-7 w-7 items-center justify-center rounded-full font-display text-base",
                    destaque ? "bg-primary text-white" : "text-ink",
                  )}
                >
                  {data.getUTCDate()}
                </p>
              </Link>
            );
          })}
        </div>

        {/* Corpo: coluna de horas + sete colunas de dia */}
        <div
          className="grid grid-cols-[3.5rem_repeat(7,minmax(7rem,1fr))]"
          style={{ height: `${altura}px` }}
        >
          <div className="relative">
            {horasCheias.map((m) => (
              <span
                key={m}
                className="absolute right-2 -translate-y-1/2 text-[0.65rem] text-ink-subtle"
                style={{ top: `${(m - inicioGrade) * PX_POR_MINUTO}px` }}
              >
                {String(Math.floor(m / 60)).padStart(2, "0")}:00
              </span>
            ))}
          </div>

          {dias.map((d) => {
            const doDia = atendimentos.filter((a) => diaDoAtendimento(a.startsAt) === d);

            return (
              <div key={d} className="relative border-l border-border">
                {/* Linhas de hora ao fundo */}
                {horasCheias.map((m) => (
                  <div
                    key={m}
                    className="absolute inset-x-0 border-t border-border/60"
                    style={{ top: `${(m - inicioGrade) * PX_POR_MINUTO}px` }}
                  />
                ))}

                {doDia.map((a) => {
                  const inicio = minutosDoDia(a.startsAt);
                  const fim = minutosDoDia(a.endsAt);
                  const alturaBloco = Math.max((fim - inicio) * PX_POR_MINUTO, 26);

                  return (
                    <div
                      key={a.id}
                      className={cn(
                        "absolute inset-x-1 overflow-hidden rounded-md border-l-[3px] px-2 py-1",
                        CORES[a.status] ?? CORES.Agendado,
                      )}
                      style={{
                        top: `${(inicio - inicioGrade) * PX_POR_MINUTO}px`,
                        height: `${alturaBloco}px`,
                      }}
                      title={`${hora(a.startsAt)}–${hora(a.endsAt)} · ${a.patientName} · ${a.procedureName} · ${a.professionalName}`}
                    >
                      <p className="text-[0.65rem] leading-tight text-ink-muted">
                        {hora(a.startsAt)} – {hora(a.endsAt)}
                      </p>
                      <p className="truncate text-xs font-medium leading-tight">
                        {a.patientName}
                      </p>
                      {alturaBloco > 46 && (
                        <p className="truncate text-[0.65rem] leading-tight text-ink-muted">
                          {a.procedureName}
                        </p>
                      )}
                      {alturaBloco > 66 && (
                        <p className="truncate text-[0.65rem] leading-tight text-ink-subtle">
                          {a.professionalName}
                        </p>
                      )}
                    </div>
                  );
                })}

                {/* Coluna vazia continua clicável: é o gesto natural de "marcar aqui". */}
                {doDia.length === 0 && podeAgendar && (
                  <Link
                    href={`/agenda/novo?dia=${d}`}
                    className="absolute inset-0 flex items-center justify-center text-xs text-ink-subtle opacity-0 transition-opacity hover:opacity-100"
                  >
                    + agendar
                  </Link>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
