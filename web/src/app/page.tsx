import { AskPanel } from "@/components/AskPanel";
import { SiteHeader } from "@/components/SiteHeader";

export default function Home() {
  return (
    <main className="mx-auto w-full max-w-2xl flex-1 px-4 py-12">
      <SiteHeader loggedIn />
      <AskPanel />
    </main>
  );
}
