import { formatCount, formatMs, formatNok, formatTime } from "@/lib/format";
import type { RecentCall } from "@/types";

const STATUS_LABELS: Record<string, string> = {
  answered: "Besvart",
  not_found: "Ikke funnet",
  out_of_scope: "Utenfor tema",
  error: "Feil",
};

const HEADINGS = [
  "Tid",
  "Status",
  "Årsak",
  "Modell",
  "Tokens inn / ut",
  "Kostnad",
  "Svartid",
];

const CELL = "py-2 pr-4 whitespace-nowrap";

export function RecentCallsTable({ calls }: { calls: RecentCall[] }) {
  if (calls.length === 0) {
    return <p className="text-muted">Ingen spørsmål ennå.</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm tabular-nums">
        <thead className="text-muted">
          <tr>
            {HEADINGS.map((heading) => (
              <th key={heading} className={`${CELL} font-medium`}>
                {heading}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {calls.map((call) => (
            <tr key={call.request_id} className="border-t border-border">
              <td className={CELL}>{formatTime(call.created_at)}</td>
              <td
                className={`${CELL} ${call.status === "error" ? "text-danger" : ""}`}
              >
                {STATUS_LABELS[call.status] ?? call.status}
              </td>
              <td className={`${CELL} font-mono text-xs`}>
                {call.failure_reason ?? ""}
              </td>
              <td className={`${CELL} font-mono text-xs`}>{call.model ?? ""}</td>
              <td className={CELL}>
                {formatCount(call.input_tokens)} / {formatCount(call.output_tokens)}
              </td>
              <td className={CELL}>{formatNok(call.cost_nok)}</td>
              <td className={CELL}>{formatMs(call.latency_ms)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
