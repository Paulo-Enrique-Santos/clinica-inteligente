import NextAuth from "next-auth";
import Keycloak from "next-auth/providers/keycloak";
import type { JWT } from "next-auth/jwt";
import { readJwtClaims } from "@/lib/jwt";

export const KEYCLOAK_ISSUER = process.env.AUTH_KEYCLOAK_ISSUER!;

const PAPEIS_DA_CLINICA = ["OWNER", "DOCTOR", "SECRETARY", "FINANCE"];

/**
 * O access token do Keycloak vale 15 minutos. Sem renovacao, a tela quebraria com 401 no
 * meio do expediente da secretaria — entao trocamos o refresh token por um novo par assim
 * que o atual esta perto de vencer.
 */
async function renovarToken(token: JWT): Promise<JWT> {
  try {
    const resposta = await fetch(`${KEYCLOAK_ISSUER}/protocol/openid-connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "refresh_token",
        client_id: process.env.AUTH_KEYCLOAK_ID!,
        client_secret: process.env.AUTH_KEYCLOAK_SECRET!,
        refresh_token: token.refreshToken!,
      }),
    });

    const dados = await resposta.json();
    if (!resposta.ok) throw new Error(dados.error_description ?? "falha ao renovar");

    return {
      ...token,
      accessToken: dados.access_token,
      refreshToken: dados.refresh_token ?? token.refreshToken,
      expiresAt: Math.floor(Date.now() / 1000) + dados.expires_in,
      error: undefined,
    };
  } catch {
    // Sinaliza para a UI mandar a pessoa logar de novo, em vez de ficar tentando
    // chamar a API com um token morto.
    return { ...token, error: "RefreshTokenError" };
  }
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  providers: [
    Keycloak({
      clientId: process.env.AUTH_KEYCLOAK_ID,
      clientSecret: process.env.AUTH_KEYCLOAK_SECRET,
      issuer: KEYCLOAK_ISSUER,
    }),
  ],
  callbacks: {
    async jwt({ token, account }) {
      // Primeiro login: guarda os tokens e extrai clinica e papeis para exibicao.
      if (account) {
        const claims = readJwtClaims(account.access_token);
        const realmAccess = claims?.realm_access as { roles?: string[] } | undefined;

        return {
          ...token,
          accessToken: account.access_token,
          refreshToken: account.refresh_token,
          idToken: account.id_token,
          expiresAt: account.expires_at,
          tenantId: claims?.tenant_id as string | undefined,
          // Só os papéis do negócio. O Keycloak também manda os dele
          // (default-roles-clinica, offline_access, uma_authorization), que não
          // significam nada para a clínica e só poluiriam a tela.
          roles: (realmAccess?.roles ?? []).filter((r) => PAPEIS_DA_CLINICA.includes(r)),
        };
      }

      // Ainda valido? Renova 30s antes de vencer, para nao correr risco de expirar
      // no meio de uma requisicao.
      if (token.expiresAt && Date.now() < token.expiresAt * 1000 - 30_000) {
        return token;
      }

      return renovarToken(token);
    },

    async session({ session, token }) {
      session.accessToken = token.accessToken;
      session.idToken = token.idToken;
      session.tenantId = token.tenantId;
      session.roles = token.roles ?? [];
      session.error = token.error;
      return session;
    },
  },
});
