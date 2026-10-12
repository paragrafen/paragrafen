import type { AnswerStatus, AskResponse } from "@/types";
import { AnswerText } from "./AnswerText";
import { Card } from "./Card";
import { SourceList } from "./SourceList";

const STATUS_MESSAGES: Record<Exclude<AnswerStatus, "answered">, string> = {
  not_found: "Fant ikke noe svar i lovteksten.",
  out_of_scope: "Spørsmålet ligger utenfor det jeg kan svare på.",
};

export function Answer({ data }: { data: AskResponse }) {
  if (data.status !== "answered") {
    return (
      <Card>
        <p className="text-muted">{STATUS_MESSAGES[data.status]}</p>
      </Card>
    );
  }

  return (
    <Card>
      <AnswerText answer={data.answer} sources={data.sources} />
      {data.sources.length > 0 && <SourceList sources={data.sources} />}
    </Card>
  );
}
