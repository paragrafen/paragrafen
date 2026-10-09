export function SiteFooter() {
  return (
    <footer className="border-t border-border">
      <div className="mx-auto max-w-2xl space-y-1 px-4 py-6 text-sm text-muted">
        <p>Dette er ikke juridisk rådgivning.</p>
        <p>
          Lovtekster fra{" "}
          <a
            href="https://lovdata.no"
            className="underline underline-offset-2 hover:text-foreground"
          >
            Lovdata
          </a>
          , lisensiert under NLOD 2.0.
        </p>
      </div>
    </footer>
  );
}
