"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";

export async function concluirAtendimento(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const id = String(formulario.get("id") ?? "");
  const dia = String(formulario.get("dia") ?? "");

  // Os insumos chegam como pares repetidos (item + quantidade), na ordem em que a
  // doutora acrescentou as linhas.
  const itens = formulario.getAll("insumo").map(String);
  const quantidades = formulario.getAll("quantidade").map(String);

  const supplies = itens
    .map((item, i) => ({
      stockItemId: item,
      quantity: Number((quantidades[i] ?? "0").replace(",", ".")),
    }))
    // Linha em branco é linha que a doutora abriu e não usou.
    .filter((s) => s.stockItemId && s.quantity > 0);

  const horas = Number(formulario.get("followUp") ?? 0);

  const resultado = await apiSend(`/appointments/${id}/concluir`, "POST", {
    notes: String(formulario.get("observacoes") ?? "").trim() || null,
    supplies,
    followUpInHours: horas > 0 ? horas : null,
  });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem) };
  }

  redirect(`/agenda?dia=${dia}`);
}
