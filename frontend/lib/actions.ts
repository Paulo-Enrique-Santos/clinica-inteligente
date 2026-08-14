"use server";

import { redirect } from "next/navigation";
import { auth, signIn, signOut, KEYCLOAK_ISSUER } from "@/auth";

export async function entrar() {
  await signIn("keycloak", { redirectTo: "/inicio" });
}

/**
 * Logout federado.
 *
 * Só apagar a sessão do Next não basta: o usuário continuaria logado no Keycloak, e o
 * próximo clique em "Entrar" o traria de volta sem pedir senha — péssimo num computador
 * compartilhado, que é exatamente o caso da recepção de uma clínica.
 */
export async function sair() {
  const session = await auth();
  const idToken = session?.idToken;

  await signOut({ redirect: false });

  const url = new URL(`${KEYCLOAK_ISSUER}/protocol/openid-connect/logout`);
  if (idToken) {
    url.searchParams.set("id_token_hint", idToken);
  }
  url.searchParams.set(
    "post_logout_redirect_uri",
    process.env.APP_BASE_URL ?? "http://localhost:3000",
  );

  redirect(url.toString());
}
