import Link from "next/link";

interface SiteHeaderProps {
  loggedIn?: boolean;
  admin?: boolean;
}

export function SiteHeader({ loggedIn = false, admin = false }: SiteHeaderProps) {
  return (
    <header className="mb-8">
      <div className="flex items-start justify-between gap-4">
        <h1 className="text-3xl font-semibold tracking-tight">
          <span className="text-primary">§</span> Paragrafen
        </h1>
        {loggedIn && (
          <div className="flex items-center gap-2">
            {admin && (
              <Link
                href="/resultater"
                className="rounded-lg border border-border px-3 py-1.5 text-sm text-muted hover:text-foreground"
              >
                Resultater
              </Link>
            )}
            <form action="/api/logout" method="post">
              <button className="cursor-pointer rounded-lg border border-border px-3 py-1.5 text-sm text-muted hover:text-foreground">
                Logg ut
              </button>
            </form>
          </div>
        )}
      </div>
      <p className="mt-2 text-muted">
        Still et spørsmål om norsk lov, og få svar med lenker til paragrafene.
      </p>
    </header>
  );
}
