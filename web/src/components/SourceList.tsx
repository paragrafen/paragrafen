import type { Source } from "@/types";

export function SourceList({ sources }: { sources: Source[] }) {
  return (
    <section className="mt-6 border-t border-border pt-4">
      <h2 className="text-sm font-semibold uppercase tracking-wide text-muted">
        Kilder
      </h2>
      <ol className="mt-2 space-y-1">
        {sources.map((source, i) => (
          <li key={source.chunk_id}>
            <span className="text-muted">[{i + 1}]</span>{" "}
            <a
              href={source.url}
              target="_blank"
              rel="noreferrer"
              className="text-primary underline underline-offset-2 hover:text-primary-hover"
            >
              {source.law} {source.section}
            </a>
          </li>
        ))}
      </ol>
    </section>
  );
}
