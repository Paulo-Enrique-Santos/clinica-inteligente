"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";

export async function criarProtocolo(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const procedimentos = formulario.getAll("procedimento").map(String);
  const sessoes = formulario.getAll("sessoes").map(String);
  const intervalos = formulario.getAll("intervalo").map(String);
  const inicios = formulario.getAll("inicio").map(String);

  const items = procedimentos
    .map((procedureId, i) => ({
      procedureId,
      sessions: Number(sessoes[i] ?? 1) || 1,
      intervalDays: intervalos[i] ? Number(intervalos[i]) : null,
      startAfterDays: Number(inicios[i] ?? 0) || 0,
    }))
    // Linha em branco é linha que a doutora abriu e não usou.
    .filter((i) => i.procedureId);

  if (items.length === 0) {
    return { erro: "Inclua ao menos um procedimento no protocolo." };
  }

  const resultado = await apiSend("/treatment-plans", "POST", {
    patientId: String(formulario.get("paciente") ?? ""),
    professionalId: String(formulario.get("profissional") ?? ""),
    notes: String(formulario.get("observacoes") ?? "").trim() || null,
    items,
  });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem) };
  }

  redirect("/protocolos");
}

export async function fecharOrcamento(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const protocolo = String(formulario.get("protocolo") ?? "");

  // Só os itens marcados. Desmarcar é como a recepção registra "a paciente não quis
  // este agora" — e o item continua no protocolo, como recusado.
  const aceitos = formulario.getAll("aceito").map(String).filter(Boolean);

  const resultado = await apiSend(`/treatment-plans/${protocolo}/orcamento`, "POST", {
    acceptedItemIds: aceitos,
    forma: String(formulario.get("forma") ?? "AVista"),
    primeiroVencimento: String(formulario.get("vencimento") ?? ""),
    parcelas: Number(formulario.get("parcelas") ?? 1) || 1,
    sinal: Number(String(formulario.get("sinal") ?? "0").replace(",", ".")) || 0,
    observacoes: String(formulario.get("observacoes") ?? "").trim() || null,
  });

  if (!resultado.ok) {
    return { erro: mensagemDeErro(resultado.problem) };
  }

  redirect("/protocolos");
}
