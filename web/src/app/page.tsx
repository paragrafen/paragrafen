import { AskPanel } from "@/components/AskPanel";

export default function Home() {
  return (
    <main className="mx-auto w-full max-w-2xl flex-1 px-4 py-12">
      <header className="mb-8">
        <h1 className="text-3xl font-semibold tracking-tight">
          <span className="text-primary">§</span> Paragrafen
        </h1>
        <p className="mt-2 text-muted">
          Still et spørsmål om norsk lov, og få svar med lenker til paragrafene.
        </p>
      </header>
      <AskPanel />
    </main>
  );
}
