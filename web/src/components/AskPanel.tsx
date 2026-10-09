"use client";

import { useAsk } from "@/hooks/useAsk";
import { Answer } from "./Answer";
import { ErrorMessage } from "./ErrorMessage";
import { QuestionForm } from "./QuestionForm";

export function AskPanel() {
  const { ask, data, error, loading } = useAsk();

  return (
    <div className="space-y-6">
      <QuestionForm onSubmit={ask} loading={loading} />
      {error && <ErrorMessage message={error} />}
      <div aria-live="polite">{data && <Answer data={data} />}</div>
    </div>
  );
}
