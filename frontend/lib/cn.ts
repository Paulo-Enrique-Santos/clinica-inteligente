/**
 * Junta classes ignorando falsos. Versão mínima do `clsx` — não vale uma
 * dependência para 6 linhas.
 */
export function cn(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(" ");
}
