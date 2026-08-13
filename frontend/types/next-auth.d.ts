import type { DefaultSession } from "next-auth";

declare module "next-auth" {
  interface Session {
    /** Repassado no header Authorization das chamadas a API .NET. Nunca vai ao navegador. */
    accessToken?: string;
    /** Necessario para o logout federado no Keycloak (id_token_hint). */
    idToken?: string;
    /** Clinica do usuario. Aqui e so para exibicao — quem manda e o claim dentro do token. */
    tenantId?: string;
    roles: string[];
    error?: string;
    user: DefaultSession["user"];
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    accessToken?: string;
    refreshToken?: string;
    idToken?: string;
    expiresAt?: number;
    tenantId?: string;
    roles?: string[];
    error?: string;
  }
}
