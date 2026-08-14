"use client";

import { useState } from "react";
import { Formulario } from "@/components/ui/form";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";
import { salvarExpediente } from "@/lib/actions/expediente";

const DIAS = [
  { n: 1, nome: "Segunda", curto: "SEG" },
  { n: 2, nome: "Terça", curto: "TER" },
  { n: 3, nome: "Quarta", curto: "QUA" },
  { n: 4, nome: "Quinta", curto: "QUI" },
  { n: 5, nome: "Sexta", curto: "SEX" },
  { n: 6, nome: "Sábado", curto: "SÁB" },
  { n: 0, nome: "Domingo", curto: "DOM" },
];

type Dia = {
  ativo: boolean;
  inicio: string;
  fim: string;
  almocoInicio: string;
  almocoFim: string;
};

const PADRAO: Dia = {
  ativo: false,
  inicio: "09:00",
  fim: "18:00",
  almocoInicio: "12:00",
  almocoFim: "13:00",
};

/**
 * Editor do expediente semanal.
 *
 * A versão anterior pedia quatro horários por dia, sete vezes — 28 campos para dizer
 * "de segunda a sexta, das 9 às 18". Aqui o gesto principal é preencher um dia e
 * repetir nos outros; e o dia desligado esconde os campos, em vez de mostrá-los
 * inertes.
 */
export function EditorDeExpediente({
  profissional,
  inicial,
}: {
  profissional: string;
  inicial: Record<number, Omit<Dia, "ativo">>;
}) {
  const [dias, setDias] = useState<Record<number, Dia>>(() =>
    Object.fromEntries(
      DIAS.map((d) => [
        d.n,
        inicial[d.n] ? { ativo: true, ...inicial[d.n] } : { ...PADRAO },
      ]),
    ),
  );

  function mudar(n: number, campo: keyof Dia, valor: string | boolean) {
    setDias((atuais) => ({ ...atuais, [n]: { ...atuais[n], [campo]: valor } }));
  }

  /** Copia os horários do primeiro dia ligado para todos os outros que estiverem ligados. */
  function repetirNosDemais() {
    const modelo = DIAS.map((d) => dias[d.n]).find((d) => d.ativo);
    if (!modelo) return;

    setDias((atuais) =>
      Object.fromEntries(
        Object.entries(atuais).map(([n, d]) => [
          n,
          d.ativo
            ? {
                ...d,
                inicio: modelo.inicio,
                fim: modelo.fim,
                almocoInicio: modelo.almocoInicio,
                almocoFim: modelo.almocoFim,
              }
            : d,
        ]),
      ),
    );
  }

  function diasUteis() {
    setDias((atuais) =>
      Object.fromEntries(
        Object.entries(atuais).map(([n, d]) => [n, { ...d, ativo: Number(n) >= 1 && Number(n) <= 5 }]),
      ),
    );
  }

  const ligados = DIAS.filter((d) => dias[d.n].ativo);

  return (
    <Formulario acao={salvarExpediente} rotuloEnviar="Salvar expediente">
      <input type="hidden" name="profissional" value={profissional} />

      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="secondary" size="sm" onClick={diasUteis}>
          Segunda a sexta
        </Button>
        <Button
          type="button"
          variant="secondary"
          size="sm"
          onClick={repetirNosDemais}
          disabled={ligados.length < 2}
        >
          Repetir horário nos demais dias
        </Button>
      </div>

      <div className="space-y-2">
        {DIAS.map(({ n, nome, curto }) => {
          const dia = dias[n];

          return (
            <div
              key={n}
              className={cn(
                "rounded-control border px-4 py-3 transition-colors",
                dia.ativo ? "border-primary-border bg-canvas" : "border-border bg-surface",
              )}
            >
              <div className="flex flex-wrap items-center gap-x-4 gap-y-3">
                <label className="flex w-32 shrink-0 cursor-pointer items-center gap-2.5">
                  <input
                    type="checkbox"
                    name={`ativo-${n}`}
                    checked={dia.ativo}
                    onChange={(e) => mudar(n, "ativo", e.target.checked)}
                    className="accent-primary"
                  />
                  <span className={cn("text-sm", dia.ativo ? "text-ink" : "text-ink-subtle")}>
                    <span className="sm:hidden">{curto}</span>
                    <span className="hidden sm:inline">{nome}</span>
                  </span>
                </label>

                {dia.ativo ? (
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
                    <span className="flex items-center gap-1.5 text-ink-muted">
                      <input
                        type="time"
                        name={`inicio-${n}`}
                        value={dia.inicio}
                        onChange={(e) => mudar(n, "inicio", e.target.value)}
                        className="rounded-control border border-border-strong px-2 py-1 text-ink"
                      />
                      às
                      <input
                        type="time"
                        name={`fim-${n}`}
                        value={dia.fim}
                        onChange={(e) => mudar(n, "fim", e.target.value)}
                        className="rounded-control border border-border-strong px-2 py-1 text-ink"
                      />
                    </span>

                    <span className="flex items-center gap-1.5 text-ink-subtle">
                      almoço
                      <input
                        type="time"
                        name={`almocoInicio-${n}`}
                        value={dia.almocoInicio}
                        onChange={(e) => mudar(n, "almocoInicio", e.target.value)}
                        className="rounded-control border border-border-strong px-2 py-1 text-ink"
                      />
                      às
                      <input
                        type="time"
                        name={`almocoFim-${n}`}
                        value={dia.almocoFim}
                        onChange={(e) => mudar(n, "almocoFim", e.target.value)}
                        className="rounded-control border border-border-strong px-2 py-1 text-ink"
                      />
                    </span>
                  </div>
                ) : (
                  <span className="text-sm text-ink-subtle">Não atende</span>
                )}
              </div>
            </div>
          );
        })}
      </div>

      <p className="text-xs text-ink-subtle">
        {ligados.length === 0
          ? "Sem expediente definido, qualquer horário é aceito no agendamento."
          : `Atende ${ligados.length} ${ligados.length === 1 ? "dia" : "dias"} por semana.`}
      </p>
    </Formulario>
  );
}
