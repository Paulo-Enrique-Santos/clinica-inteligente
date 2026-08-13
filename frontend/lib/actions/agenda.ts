"use server";

import { redirect } from "next/navigation";
import { apiSend, mensagemDeErro } from "@/lib/api";
import type { EstadoFormulario } from "@/lib/form";

/**
 * Fuso da clínica.
 *
 * O Brasil não tem horário de verão desde 2019, então -03:00 é estável para São Paulo.
 * Quando o sistema atender clínica em outro fuso (ou o país voltar com o horário de
 * verão), isto vira uma configuração por tenant — e a hora combinada com a paciente não
 * pode depender do fuso do servidor onde a aplicação por acaso está rodando.
 */
const FUSO_DA_CLINICA = "-03:00";

function paraInstante(data: string, hora: string): string {
  return `${data}T${hora}:00${FUSO_DA_CLINICA}`;
}

export async function criarAtendimento(
  _anterior: EstadoFormulario,
  formulario: FormData,
): Promise<EstadoFormulario> {
  const resultado = await apiSend("/appointments", "POST", {
    patientId: String(formulario.get("paciente") ?? ""),
    procedureId: String(formulario.get("procedimento") ?? ""),
    professionalId: String(formulario.get("profissional") ?? ""),
    startsAt: paraInstante(
      String(formulario.get("data") ?? ""),
      String(formulario.get("hora") ?? ""),
    ),
    notes: String(formulario.get("observacoes") ?? "").trim() || null,
  });

  if (!resultado.ok) {
    // 409 aqui é conflito de horário — a mensagem vem pronta da API, já dizendo
    // qual janela está ocupada.
    return { erro: mensagemDeErro(resultado.problem) };
  }

  redirect(`/agenda?dia=${String(formulario.get("data") ?? "")}`);
}

export async function alterarStatus(formulario: FormData) {
  const id = String(formulario.get("id") ?? "");
  const dia = String(formulario.get("dia") ?? "");

  await apiSend(`/appointments/${id}/status`, "POST", {
    status: String(formulario.get("status") ?? ""),
    reason: String(formulario.get("motivo") ?? "").trim() || null,
  });

  redirect(`/agenda?dia=${dia}`);
}
