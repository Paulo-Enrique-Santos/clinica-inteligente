"use server";

import { redirect } from "next/navigation";
import { apiSend } from "@/lib/api";

export async function darBaixa(formulario: FormData) {
  const id = String(formulario.get("id") ?? "");
  const filtro = String(formulario.get("filtro") ?? "");

  await apiSend(`/payments/${id}/baixa`, "POST", {
    method: String(formulario.get("metodo") ?? "Pix"),
  });

  redirect(`/financeiro${filtro ? `?filtro=${filtro}` : ""}`);
}

export async function estornarCobranca(formulario: FormData) {
  const id = String(formulario.get("id") ?? "");
  const filtro = String(formulario.get("filtro") ?? "");

  await apiSend(`/payments/${id}/estornar`, "POST", {
    motivo: String(formulario.get("motivo") ?? "").trim() || null,
  });

  redirect(`/financeiro${filtro ? `?filtro=${filtro}` : ""}`);
}

export async function cancelarCobranca(formulario: FormData) {
  const id = String(formulario.get("id") ?? "");
  const filtro = String(formulario.get("filtro") ?? "");

  await apiSend(`/payments/${id}/cancelar`, "POST");

  redirect(`/financeiro${filtro ? `?filtro=${filtro}` : ""}`);
}
