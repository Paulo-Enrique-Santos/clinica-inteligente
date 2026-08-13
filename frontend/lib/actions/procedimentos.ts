"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";

/** "250,00" e "250.00" viram 250. A recepção digita com vírgula. */
function paraNumero(valor: FormDataEntryValue | null): number {
  return Number(String(valor ?? "0").replace(/\./g, "").replace(",", ".")) || 0;
}

export async function criarProcedimento(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const resultado = await apiSend("/procedures", "POST", {
    name: String(formulario.get("nome") ?? "").trim(),
    durationMinutes: Number(formulario.get("duracao") ?? 0),
    price: paraNumero(formulario.get("preco")),
    suppliesCost: paraNumero(formulario.get("custo")),
    description: String(formulario.get("descricao") ?? "").trim() || null,
  });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem) };
  }

  redirect("/procedimentos");
}
