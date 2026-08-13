import type { InputHTMLAttributes, SelectHTMLAttributes, ReactNode } from "react";
import { cn } from "@/lib/cn";

const CONTROLE =
  "w-full rounded-control border border-border-strong bg-canvas px-3 py-2 text-sm text-ink " +
  "placeholder:text-ink-subtle transition-colors " +
  "hover:border-primary-border focus:border-primary focus:outline-none " +
  "disabled:bg-surface-muted disabled:text-ink-subtle";

export function Field({
  label,
  hint,
  error,
  htmlFor,
  children,
}: {
  label: string;
  hint?: string;
  error?: string;
  htmlFor?: string;
  children: ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={htmlFor} className="block text-sm font-medium text-ink">
        {label}
      </label>
      {children}
      {/* Erro tem prioridade sobre a dica: quando algo deu errado, a instrução
          genérica só rouba atenção. */}
      {error ? (
        <p className="text-xs text-danger">{error}</p>
      ) : hint ? (
        <p className="text-xs text-ink-subtle">{hint}</p>
      ) : null}
    </div>
  );
}

export function Input({
  className,
  ...props
}: InputHTMLAttributes<HTMLInputElement>) {
  return <input className={cn(CONTROLE, className)} {...props} />;
}

export function Select({
  className,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select className={cn(CONTROLE, "pr-8", className)} {...props} />;
}
