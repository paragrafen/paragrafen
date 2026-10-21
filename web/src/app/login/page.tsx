import type { Metadata } from "next";
import Link from "next/link";
import { AuthForm } from "@/components/AuthForm";
import { Card } from "@/components/Card";
import { SiteHeader } from "@/components/SiteHeader";

export const metadata: Metadata = {
  title: "Logg inn – Paragrafen",
};

export default function LoginPage() {
  return (
    <main className="mx-auto w-full max-w-md flex-1 px-4 py-12">
      <SiteHeader />
      <Card>
        <h2 className="mb-4 text-xl font-semibold">Logg inn</h2>
        <AuthForm mode="login" />
        <p className="mt-4 text-sm text-muted">
          Har du ikke konto?{" "}
          <Link
            href="/registrer"
            className="text-primary underline underline-offset-2"
          >
            Opprett konto
          </Link>
        </p>
      </Card>
    </main>
  );
}
