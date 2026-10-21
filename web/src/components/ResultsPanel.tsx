"use client";

import { useResults } from "@/hooks/useResults";
import { formatTime } from "@/lib/format";
import { Card } from "./Card";
import { DayTable } from "./DayTable";
import { ErrorMessage } from "./ErrorMessage";
import { KeyNumbers } from "./KeyNumbers";
import { RecentCallsTable } from "./RecentCallsTable";

const ERROR_MESSAGES = {
  forbidden:
    "Denne siden er kun for administratorer. Er du nettopp gjort til admin, logg ut og inn igjen.",
};

export function ResultsPanel() {
  const { data, error } = useResults();

  if (error) return <ErrorMessage error={error} messages={ERROR_MESSAGES} />;
  if (!data) return <p className="text-muted">Henter tall …</p>;

  return (
    <div className="space-y-6">
      <KeyNumbers stats={data.total} />
      <Card>
        <h3 className="mb-4 text-lg font-semibold">Per dag</h3>
        <DayTable days={data.days} />
      </Card>
      <Card>
        <h3 className="mb-4 text-lg font-semibold">Siste spørsmål</h3>
        <RecentCallsTable calls={data.recent_calls} />
      </Card>
      <p className="text-sm text-muted">
        Oppdatert {formatTime(data.generated_at)}. Kostnad og svartid (median
        og p95) gjelder spørsmål som nådde språkmodellen. Kostnaden er beregnet
        fra tokenbruk og listepris.
      </p>
    </div>
  );
}
