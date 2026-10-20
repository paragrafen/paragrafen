export function SiteHeader({ loggedIn = false }: { loggedIn?: boolean }) {
  return (
    <header className="mb-8">
      <div className="flex items-start justify-between gap-4">
        <h1 className="text-3xl font-semibold tracking-tight">
          <span className="text-primary">§</span> Paragrafen
        </h1>
        {loggedIn && (
          <form action="/api/logout" method="post">
            <button className="cursor-pointer rounded-lg border border-border px-3 py-1.5 text-sm text-muted hover:text-foreground">
              Logg ut
            </button>
          </form>
        )}
      </div>
      <p className="mt-2 text-muted">
        Still et spørsmål om norsk lov, og få svar med lenker til paragrafene.
      </p>
    </header>
  );
}
