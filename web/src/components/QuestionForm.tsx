import { SubmitEvent, useId, useState } from "react";

const MAX_LENGTH = 500;

interface QuestionFormProps {
  onSubmit: (question: string) => void;
  loading: boolean;
}

export function QuestionForm({ onSubmit, loading }: QuestionFormProps) {
  const id = useId();
  const [question, setQuestion] = useState("");
  const trimmed = question.trim();

  function handleSubmit(e: SubmitEvent) {
    e.preventDefault();
    onSubmit(trimmed);
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <label htmlFor={id} className="block font-medium">
        Ditt spørsmål
      </label>
      <textarea
        id={id}
        value={question}
        onChange={(e) => setQuestion(e.target.value)}
        maxLength={MAX_LENGTH}
        rows={4}
        placeholder="Hvor mange feriedager har jeg krav på?"
        className="w-full resize-y rounded-lg border border-border bg-surface p-3 placeholder:text-muted focus:border-primary focus:ring-2 focus:ring-primary/30 focus:outline-none"
      />
      <div className="flex items-center justify-between gap-4">
        <p className="text-sm text-muted">
          Ikke skriv inn personopplysninger i spørsmålet.
        </p>
        <button
          disabled={loading || !trimmed}
          className="cursor-pointer rounded-lg bg-primary px-5 py-2 font-medium text-primary-foreground hover:bg-primary-hover disabled:cursor-not-allowed disabled:opacity-50"
        >
          {loading ? "Søker..." : "Spør"}
        </button>
      </div>
    </form>
  );
}
