import { cookies } from "next/headers";
import Link from "next/link";
import { usesMockApi } from "@/lib/api";
import { isAdmin, SESSION_COOKIE } from "@/lib/session";

export async function AdminLink() {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;
  if (!usesMockApi() && !isAdmin(token)) return null;

  return (
    <Link
      href="/resultater"
      className="rounded-lg border border-border px-3 py-1.5 text-sm text-muted hover:text-foreground"
    >
      Resultater
    </Link>
  );
}
