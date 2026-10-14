import type { AskError } from "@/hooks/useAsk";

const ERROR_MESSAGES: Record<string, string> = {
  invalid_request:
    "Spørsmålet kunne ikke behandles. Det må være mellom 1 og 500 tegn.",
  unauthorized: "Du må logge inn for å stille spørsmål.",
  rate_limited:
    "Du har stilt mange spørsmål på kort tid. Vent litt og prøv igjen.",
};

const FALLBACK_MESSAGE = "Noe gikk galt. Prøv igjen.";

export function ErrorMessage({ error }: { error: AskError }) {
  return (
    <div
      role="alert"
      className="rounded-lg border border-danger/30 bg-danger/10 p-4 text-danger"
    >
      <p>{ERROR_MESSAGES[error.code] ?? FALLBACK_MESSAGE}</p>
      {error.requestId && (
        <p className="mt-2 text-sm">
          Feil-ID: <span className="font-mono select-all">{error.requestId}</span>
        </p>
      )}
    </div>
  );
}
