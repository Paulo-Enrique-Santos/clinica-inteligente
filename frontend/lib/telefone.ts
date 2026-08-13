/**
 * Converte o que a recepção digita para E.164, o formato que o sistema guarda.
 *
 * A recepção escreve "(11) 98765-4321", "11987654321", "+55 11 98765-4321" — todas
 * corretas do ponto de vista humano. Exigir E.164 na tela seria transferir para a
 * secretária um problema que é do software.
 */
export function paraE164(entrada: string): string {
  const digitos = entrada.replace(/\D/g, "");

  if (digitos.length === 0) return "";

  // Já veio com código do país.
  if (entrada.trim().startsWith("+")) return `+${digitos}`;

  // 10 ou 11 dígitos = número brasileiro com DDD, sem o 55.
  if (digitos.length === 10 || digitos.length === 11) return `+55${digitos}`;

  // 12 ou 13 = já tem o 55 na frente.
  if (digitos.length === 12 || digitos.length === 13) return `+${digitos}`;

  return `+${digitos}`;
}

/** E.164 de volta para leitura humana: +5511987654321 -> (11) 98765-4321 */
export function formatarTelefone(e164: string): string {
  const m = e164.match(/^\+55(\d{2})(\d{4,5})(\d{4})$/);
  return m ? `(${m[1]}) ${m[2]}-${m[3]}` : e164;
}
