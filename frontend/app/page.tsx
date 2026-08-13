import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { entrar } from "@/lib/actions";

export default async function Home() {
  const session = await auth();

  if (session && !session.error) {
    redirect("/pacientes");
  }

  return (
    <div className="flex flex-1 items-center justify-center bg-zinc-50 p-6 dark:bg-zinc-950">
      <main className="w-full max-w-sm rounded-xl border border-zinc-200 bg-white p-8 dark:border-zinc-800 dark:bg-zinc-900">
        <h1 className="text-xl font-semibold text-zinc-900 dark:text-zinc-50">
          Clínica Inteligente
        </h1>
        <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-400">
          Entre com o seu e-mail e senha da clínica.
        </p>

        <form action={entrar} className="mt-6">
          <button
            type="submit"
            className="w-full rounded-lg bg-zinc-900 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-zinc-700 dark:bg-zinc-50 dark:text-zinc-900 dark:hover:bg-zinc-200"
          >
            Entrar
          </button>
        </form>

        <p className="mt-6 text-xs leading-relaxed text-zinc-500">
          Você será levado à tela de login do Keycloak. Em desenvolvimento, use{" "}
          <code className="rounded bg-zinc-100 px-1 py-0.5 dark:bg-zinc-800">
            bia.secretaria
          </code>{" "}
          ou{" "}
          <code className="rounded bg-zinc-100 px-1 py-0.5 dark:bg-zinc-800">
            rita.owner
          </code>{" "}
          com a senha{" "}
          <code className="rounded bg-zinc-100 px-1 py-0.5 dark:bg-zinc-800">dev123</code>{" "}
          para ver clínicas diferentes.
        </p>
      </main>
    </div>
  );
}
