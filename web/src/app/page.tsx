"use client";

import { SubmitEvent, useState } from "react";
import type { AskResponse } from "@/types";

const MAX_LENGTH = 500;

function Answer({ data }: { data: AskResponse }) {
  if (data.status === "not_found")
    return <p>Fant ikke noe svar i lovteksten.</p>;
  if (data.status === "out_of_scope")
    return <p>Spørsmålet ligger utenfor det jeg kan svare på.</p>;

  const parts = data.answer.split(/(\[\d+\])/);
  return (
    <p>
      {parts.map((part, i) => {
        const match = part.match(/^\[(\d+)\]$/);
        const source = match && data.sources[Number(match[1]) - 1];
        return source ? (
          <a key={i} href={source.url} target="_blank" rel="noreferrer">
            {part}
          </a>
        ) : (
          part
        );
      })}
    </p>
  );
}

export default function Home() {
  const [question, setQuestion] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [data, setData] = useState<AskResponse | null>(null);

  async function submit(e: SubmitEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setData(null);
    try {
      const res = await fetch("/api/ask", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ question }),
      });
      if (!res.ok) throw new Error();
      setData(await res.json());
    } catch {
      setError("Noe gikk galt. Prøv igjen.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main>
      <h1>Paragrafen</h1>
      <p>
        Still et spørsmål om norsk lov, og få svar med lenker til paragrafene.
      </p>
      <p>Ikke skriv inn personopplysninger i spørsmålet.</p>

      <form onSubmit={submit}>
        <textarea
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          maxLength={MAX_LENGTH}
          rows={4}
          placeholder="Hvor mange feriedager har jeg krav på?"
        />
        <button disabled={loading || !question.trim()}>
          {loading ? "Søker..." : "Spør"}
        </button>
      </form>

      {error && <p role="alert">{error}</p>}
      {data && <Answer data={data} />}

      <footer>
        <p>Dette er ikke juridisk rådgivning.</p>
        <p>
          Lovtekster fra <a href="https://lovdata.no">Lovdata</a>, lisensiert
          under NLOD 2.0.
        </p>
      </footer>
    </main>
  );
}
