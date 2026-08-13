"use client";

import { useState } from "react";
import { Busca } from "@/components/ui/busca";
import { Input } from "@/components/ui/field";
import { Button } from "@/components/ui/button";

/**
 * Os procedimentos que a doutora está prescrevendo.
 *
 * Cada linha responde três coisas que a recepção precisa para agendar sem reler a
 * observação clínica: quantas sessões, de quanto em quanto tempo, e em quantos dias
 * começar.
 */
export function ItensDoProtocolo() {
  const [linhas, setLinhas] = useState([0]);

  return (
    <div className="space-y-4">
      {linhas.map((linha, indice) => (
        <div key={linha} className="rounded-control border border-border p-4">
          <div className="flex items-start justify-between gap-3">
            <p className="text-xs font-medium uppercase tracking-wide text-ink-subtle">
              Procedimento {indice + 1}
            </p>
            {linhas.length > 1 && (
              <button
                type="button"
                onClick={() => setLinhas((a) => a.filter((l) => l !== linha))}
                className="text-sm text-ink-subtle hover:text-danger"
              >
                remover
              </button>
            )}
          </div>

          <div className="mt-3">
            <Busca
              name="procedimento"
              recurso="procedimentos"
              label=""
              placeholder="Buscar procedimento"
              obrigatorio={false}
            />
          </div>

          <div className="mt-3 grid grid-cols-3 gap-3">
            <label className="block">
              <span className="mb-1 block text-xs text-ink-muted">Sessões</span>
              <Input name="sessoes" type="number" min={1} defaultValue={1} />
            </label>

            <label className="block">
              <span className="mb-1 block text-xs text-ink-muted">Intervalo (dias)</span>
              <Input name="intervalo" type="number" min={0} placeholder="15" />
            </label>

            <label className="block">
              <span className="mb-1 block text-xs text-ink-muted">Começar em (dias)</span>
              <Input name="inicio" type="number" min={0} defaultValue={0} />
            </label>
          </div>
        </div>
      ))}

      <Button
        type="button"
        variant="secondary"
        size="sm"
        onClick={() => setLinhas((a) => [...a, Math.max(...a) + 1])}
      >
        + outro procedimento
      </Button>
    </div>
  );
}
