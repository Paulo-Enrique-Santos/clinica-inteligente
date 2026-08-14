"use client";

import { useState } from "react";
import type { ItemDoProtocolo } from "@/lib/api";
import { Formulario } from "@/components/ui/form";
import { Field, Input, Select } from "@/components/ui/field";
import { fecharOrcamento } from "@/lib/actions/protocolos";
import { cn } from "@/lib/cn";

const MOEDA = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

const FORMAS = [
  { valor: "AVista", rotulo: "À vista" },
  { valor: "SinalMaisParcelas", rotulo: "Sinal + parcelas" },
  { valor: "Parcelado", rotulo: "Parcelado (cartão ou PIX recorrente)" },
];

/**
 * Fechamento do orçamento.
 *
 * O total muda conforme a recepção desmarca o que a paciente não quis — e ela precisa
 * ver esse número enquanto conversa, não depois de enviar.
 */
export function FormularioDeOrcamento({
  protocolo,
  itens,
  hoje,
}: {
  protocolo: string;
  itens: ItemDoProtocolo[];
  /** Vem pronto do servidor: ler o relógio durante o render torna o componente impuro. */
  hoje: string;
}) {
  const [aceitos, setAceitos] = useState<string[]>(itens.map((i) => i.id));
  const [forma, setForma] = useState("AVista");

  const total = itens
    .filter((i) => aceitos.includes(i.id))
    .reduce((soma, i) => soma + i.total, 0);

  function alternar(id: string) {
    setAceitos((atuais) =>
      atuais.includes(id) ? atuais.filter((x) => x !== id) : [...atuais, id],
    );
  }

  return (
    <Formulario acao={fecharOrcamento} cancelarHref="/protocolos" rotuloEnviar="Gerar cobranças">
      <input type="hidden" name="protocolo" value={protocolo} />

      <div className="space-y-2">
        <p className="text-sm font-medium text-ink">O que a paciente vai fazer</p>

        {itens.map((i) => {
          const marcado = aceitos.includes(i.id);

          return (
            <label
              key={i.id}
              className={cn(
                "flex cursor-pointer items-center justify-between gap-3 rounded-control border px-4 py-3 transition-colors",
                marcado ? "border-primary bg-primary-soft" : "border-border bg-canvas",
              )}
            >
              <div className="flex items-center gap-3">
                <input
                  type="checkbox"
                  name="aceito"
                  value={i.id}
                  checked={marcado}
                  onChange={() => alternar(i.id)}
                  className="accent-primary"
                />
                <div>
                  <p className={cn("text-sm", marcado ? "text-ink" : "text-ink-muted")}>
                    {i.sessions}× {i.procedureName}
                  </p>
                  {(i.intervalDays || i.startAfterDays > 0) && (
                    <p className="text-xs text-ink-subtle">
                      {i.intervalDays ? `a cada ${i.intervalDays} dias` : ""}
                      {i.intervalDays && i.startAfterDays > 0 ? " · " : ""}
                      {i.startAfterDays > 0 ? `começa em ${i.startAfterDays} dias` : ""}
                    </p>
                  )}
                </div>
              </div>

              <span className={cn("text-sm", marcado ? "text-ink" : "text-ink-subtle")}>
                {MOEDA.format(i.total)}
              </span>
            </label>
          );
        })}
      </div>

      <div className="flex items-center justify-between rounded-control bg-surface px-4 py-3">
        <span className="text-sm text-ink-muted">Total</span>
        <span className="font-display text-xl text-ink">{MOEDA.format(total)}</span>
      </div>

      <Field
        label="Meio de pagamento"
        htmlFor="meio"
        hint="Dinheiro e cartão já entram como recebidos. Só o PIX parcelado fica pendente de baixa."
      >
        <Select id="meio" name="meio" defaultValue="Pix">
          <option value="Pix">PIX</option>
          <option value="Dinheiro">Dinheiro</option>
          <option value="Credito">Cartão de crédito</option>
          <option value="Debito">Cartão de débito</option>
          <option value="Transferencia">Transferência</option>
        </Select>
      </Field>

      <Field label="Forma de pagamento" htmlFor="forma">
        <Select
          id="forma"
          name="forma"
          value={forma}
          onChange={(e) => setForma(e.target.value)}
        >
          {FORMAS.map((f) => (
            <option key={f.valor} value={f.valor}>
              {f.rotulo}
            </option>
          ))}
        </Select>
      </Field>

      <div className="grid grid-cols-2 gap-4">
        <Field label="Primeiro vencimento" htmlFor="vencimento">
          <Input
            id="vencimento"
            name="vencimento"
            type="date"
            required
            defaultValue={hoje}
          />
        </Field>

        {forma !== "AVista" && (
          <Field label="Parcelas" htmlFor="parcelas">
            <Input id="parcelas" name="parcelas" type="number" min={1} max={24} defaultValue={2} />
          </Field>
        )}
      </div>

      {forma === "SinalMaisParcelas" && (
        <Field
          label="Sinal"
          htmlFor="sinal"
          hint="Pago agora; o restante é dividido nas parcelas, a partir do mês seguinte."
        >
          <Input id="sinal" name="sinal" inputMode="decimal" placeholder="500,00" />
        </Field>
      )}

      <Field label="Observações da cobrança" htmlFor="observacoes">
        <Input id="observacoes" name="observacoes" placeholder="Opcional" />
      </Field>
    </Formulario>
  );
}
