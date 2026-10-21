import { cookies } from "next/headers";
import { AskPanel } from "@/components/AskPanel";
import { SiteHeader } from "@/components/SiteHeader";
import { usesMockApi } from "@/lib/api";
import { isAdmin, SESSION_COOKIE } from "@/lib/session";

export default async function Home() {
  const token = (await cookies()).get(SESSION_COOKIE)?.value;

  return (
    <main className="mx-auto w-full max-w-2xl flex-1 px-4 py-12">
      <SiteHeader loggedIn admin={usesMockApi() || isAdmin(token)} />
      <AskPanel />
    </main>
  );
}
