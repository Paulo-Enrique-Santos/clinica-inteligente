"use client";

import { useEffect, useState } from "react";
import { Busca } from "@/components/ui/busca";
import { Field, Input } from "@/components/ui/field";
import { cn } from "@/lib/cn";

/**
 * Campos do agendamento.
 *
 * Vivem num componente de cliente porque um depende do outro: só dá para oferecer
 * horários depois de saber a profissional, o procedimento (que define a duração) e a
 * data. A alternativa — deixar a recepção digitar um horário qualquer e descobrir no
 * envio que não cabe — é justamente o que a Fase D veio resolver.
 */
export function CamposDeAgendamento({ diaInicial }: { diaInicial: string }) {
  const [procedimento, setProcedimento] = useState<string | null>(null);
  const [profissional, setProfissional] = useState<string | null>(null);
  const [data, setData] = useState(diaInicial);
  const [escolhido, setEscolhido] = useState<string | null>(null);

  // O resultado carrega consigo a combinação que o gerou. Assim "carregando" e a lista
  // válida são DERIVADOS, e o efeito não precisa chamar setState de forma síncrona —
  // que é o que provoca render em cascata.
  const [resultado, setResultado] = useState({ chave: "", horarios: [] as string[] });

  const pronto = Boolean(procedimento && profissional && data);
  const chave = `${profissional}|${procedimento}|${data}`;

  const carregando = pronto && resultado.chave !== chave;
  const horarios = resultado.chave === chave ? resultado.horarios : [];

  // Se o horário escolhido saiu da lista (mudou o procedimento, por exemplo), a escolha
  // deixa de valer sozinha — ela pode não caber mais.
  const horarioValido = escolhido && horarios.includes(escolhido) ? escolhido : null;

  useEffect(() => {
    if (!pronto) return;

    let cancelado = false;

    fetch(`/api/slots?prof=${profissional}&proc=${procedimento}&data=${data}`)
      .then((r) => (r.ok ? r.json() : []))
      .then((lista: string[]) => {
        if (!cancelado) setResultado({ chave, horarios: lista });
      })
      .catch(() => {
        if (!cancelado) setResultado({ chave, horarios: [] });
      });

    return () => {
      cancelado = true;
    };
  }, [profissional, procedimento, data, pronto, chave]);

  return (
    <>
      <Busca name="paciente" recurso="pacientes" label="Paciente" placeholder="Nome ou telefone" />

      <Busca
        name="procedimento"
        recurso="procedimentos"
        label="Procedimento"
        placeholder="Comece a digitar"
        aoEscolher={(o) => setProcedimento(o?.id ?? null)}
      />

      <Busca
        name="profissional"
        recurso="profissionais"
        label="Profissional"
        placeholder="Comece a digitar"
        aoEscolher={(o) => setProfissional(o?.id ?? null)}
      />

      <Field label="Data" htmlFor="data">
        <Input
          id="data"
          name="data"
          type="date"
          required
          value={data}
          onChange={(e) => setData(e.target.value)}
        />
      </Field>

      <div className="space-y-1.5">
        <label className="block text-sm font-medium text-ink">Horário</label>
        <input type="hidden" name="hora" value={horarioValido ?? ""} required />

        {!pronto ? (
          <p className="rounded-control border border-dashed border-border-strong px-3 py-6 text-center text-sm text-ink-subtle">
            Escolha procedimento, profissional e data para ver os horários livres.
          </p>
        ) : carregando ? (
          <p className="rounded-control border border-border px-3 py-6 text-center text-sm text-ink-subtle">
            Buscando horários…
          </p>
        ) : horarios.length === 0 ? (
          <p className="rounded-control bg-warning-soft px-3 py-4 text-center text-sm text-warning">
            Nenhum horário livre nesta data. Tente outro dia ou revise o expediente da
            profissional.
          </p>
        ) : (
          <div className="flex flex-wrap gap-1.5">
            {horarios.map((h) => (
              <button
                key={h}
                type="button"
                onClick={() => setEscolhido(h)}
                className={cn(
                  "rounded-control border px-3 py-1.5 text-sm transition-colors",
                  horarioValido === h
                    ? "border-primary bg-primary text-white"
                    : "border-border-strong text-ink hover:bg-surface-muted",
                )}
              >
                {h}
              </button>
            ))}
          </div>
        )}
      </div>
    </>
  );
}
