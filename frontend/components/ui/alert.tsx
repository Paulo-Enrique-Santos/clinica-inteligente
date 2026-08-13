import { cn } from "@/lib/cn";

type Tone = "danger" | "warning" | "success" | "info";

const TONES: Record<Tone, string> = {
  danger: "bg-danger-soft text-danger",
  warning: "bg-warning-soft text-warning",
  success: "bg-success-soft text-success",
  info: "bg-primary-soft text-primary",
};

export function Alert({
  children,
  tone = "danger",
  className,
}: {
  children: React.ReactNode;
  tone?: Tone;
  className?: string;
}) {
  return (
    <div
      // role=alert faz o leitor de tela anunciar o erro sem a pessoa precisar
      // procurar onde a mensagem apareceu.
      role="alert"
      className={cn("rounded-control px-3.5 py-3 text-sm", TONES[tone], className)}
    >
      {children}
    </div>
  );
}
