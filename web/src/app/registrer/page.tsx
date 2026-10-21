import type { Metadata } from "next";
import Link from "next/link";
import { AuthForm } from "@/components/AuthForm";
import { Card } from "@/components/Card";
import { SiteHeader } from "@/components/SiteHeader";

export const metadata: Metadata = {
  title: "Opprett konto – Paragrafen",
};

export default function RegisterPage() {
  return (
    <main className="mx-auto w-full max-w-md flex-1 px-4 py-12">
      <SiteHeader />
      <Card>
        <h2 className="mb-4 text-xl font-semibold">Opprett konto</h2>
        <AuthForm mode="register" />
        <p className="mt-4 text-sm text-muted">
          Har du allerede konto?{" "}
          <Link href="/login" className="text-primary underline underline-offset-2">
            Logg inn
          </Link>
        </p>
      </Card>
    </main>
  );
}
