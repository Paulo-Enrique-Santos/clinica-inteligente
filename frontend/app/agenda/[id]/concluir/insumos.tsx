"use client";

import { useState } from "react";
import { Busca } from "@/components/ui/busca";
import { Input } from "@/components/ui/field";
import { Button } from "@/components/ui/button";

/**
 * Linhas de insumo usadas no atendimento.
 *
 * Quantidade é digitada, não sugerida pela tabela do procedimento: preenchimento sai por
 * ml com consumo exato, creme rende "sei lá quantas". É a diferença entre o previsto e o
 * usado que vai revelar a margem real.
 */
export function Insumos() {
  const [linhas, setLinhas] = useState<number[]>([0]);

  return (
    <div className="space-y-3">
      {linhas.map((linha, indice) => (
        <div key={linha} className="flex items-end gap-2">
          <div className="flex-1">
            <Busca
              name="insumo"
              recurso="estoque"
              label={indice === 0 ? "Insumos utilizados" : ""}
              placeholder="Buscar item do estoque"
              obrigatorio={false}
            />
          </div>

          <div className="w-28">
            <Input name="quantidade" inputMode="decimal" placeholder="Qtd." />
          </div>

          {linhas.length > 1 && (
            <Button
              type="button"
              variant="ghost"
              onClick={() => setLinhas((atuais) => atuais.filter((l) => l !== linha))}
            >
              ×
            </Button>
          )}
        </div>
      ))}

      <Button
        type="button"
        variant="secondary"
        size="sm"
        onClick={() => setLinhas((atuais) => [...atuais, Math.max(...atuais) + 1])}
      >
        + outro insumo
      </Button>
    </div>
  );
}
