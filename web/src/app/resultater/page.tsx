import type { Metadata } from "next";
import Link from "next/link";
import { ResultsPanel } from "@/components/ResultsPanel";
import { SiteHeader } from "@/components/SiteHeader";

export const metadata: Metadata = {
  title: "Resultater – Paragrafen",
};

export default function Results() {
  return (
    <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-12">
      <SiteHeader loggedIn />
      <Link
        href="/"
        className="text-sm text-primary underline underline-offset-2"
      >
        ← Tilbake til spørsmål
      </Link>
      <h2 className="mt-4 mb-6 text-2xl font-semibold">Resultater</h2>
      <ResultsPanel />
    </main>
  );
}
