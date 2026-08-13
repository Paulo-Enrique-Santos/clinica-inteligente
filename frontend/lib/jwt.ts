/**
 * Le o payload de um JWT SEM validar a assinatura.
 *
 * Isso e seguro aqui porque o token acabou de chegar do Keycloak por canal confiavel, e o
 * uso e apenas de exibicao (mostrar a clinica e o papel na tela). Quem valida assinatura,
 * emissor, audience e expiracao de verdade e a API .NET, a cada requisicao.
 *
 * Nunca use este resultado para decidir permissao.
 */
export function readJwtClaims(token: string | undefined): Record<string, unknown> | null {
  if (!token) return null;

  const payload = token.split(".")[1];
  if (!payload) return null;

  try {
    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(Buffer.from(normalized, "base64").toString("utf8"));
  } catch {
    return null;
  }
}
