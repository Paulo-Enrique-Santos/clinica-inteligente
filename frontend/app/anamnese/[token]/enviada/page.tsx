import Image from "next/image";

export default function FichaEnviadaPage() {
  return (
    <div className="flex flex-1 items-center justify-center bg-surface px-6 py-16">
      <div className="w-full max-w-md text-center">
        <div className="mb-6 flex justify-center">
          <Image
            src="/cliniq-logo.png"
            alt="CLINIQ"
            width={440}
            height={364}
            priority
            className="h-auto w-36"
          />
        </div>

        <div className="rounded-card border border-border bg-canvas p-8 shadow-soft">
          <h1 className="text-xl text-ink">Ficha enviada</h1>
          <p className="mt-3 text-sm leading-relaxed text-ink-muted">
            Obrigado! Sua profissional já tem acesso às informações. Nos vemos no seu
            atendimento.
          </p>
        </div>
      </div>
    </div>
  );
}
