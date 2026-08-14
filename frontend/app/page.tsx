import Image from "next/image";
import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { entrar } from "@/lib/actions";
import { Button } from "@/components/ui/button";

export default async function Home() {
  const session = await auth();

  if (session && !session.error) {
    redirect("/inicio");
  }

  return (
    <div className="flex flex-1 items-center justify-center bg-surface px-6 py-16">
      <div className="w-full max-w-sm">
        {/* Marca acima do cartão, com respiro generoso: é o gesto que faz a tela
            parecer cuidada em vez de formulário de sistema. */}
        <div className="mb-8 flex justify-center">
          <Image
            src="/cliniq-logo.png"
            alt="CLINIQ — Clínica Inteligente"
            width={440}
            height={364}
            priority
            className="h-auto w-52"
          />
        </div>

        <div className="rounded-card border border-border bg-canvas p-8 shadow-soft">
          <h2 className="text-lg text-ink">Entrar</h2>
          <p className="mt-1.5 text-sm text-ink-muted">
            Use o e-mail e a senha cadastrados pela sua clínica.
          </p>

          <form action={entrar} className="mt-6">
            <Button type="submit" size="lg" full>
              Continuar
            </Button>
          </form>
        </div>

        <p className="mt-6 text-center text-xs leading-relaxed text-ink-subtle">
          Em desenvolvimento: entre como{" "}
          <code className="rounded bg-surface-muted px-1.5 py-0.5 font-mono text-ink-muted">
            bia.secretaria
          </code>{" "}
          ou{" "}
          <code className="rounded bg-surface-muted px-1.5 py-0.5 font-mono text-ink-muted">
            rita.owner
          </code>{" "}
          com a senha{" "}
          <code className="rounded bg-surface-muted px-1.5 py-0.5 font-mono text-ink-muted">
            dev123
          </code>{" "}
          para ver clínicas diferentes.
        </p>
      </div>
    </div>
  );
}
