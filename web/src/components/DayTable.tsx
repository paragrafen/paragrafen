import {
  formatCount,
  formatDate,
  formatMs,
  formatNok,
  formatPercent,
} from "@/lib/format";
import type { DayStats } from "@/types";

const HEADINGS = [
  "Dato",
  "Spørsmål",
  "Besvart",
  "Ikke funnet",
  "Utenfor tema",
  "Feil",
  "Feilrate",
  "Kostnad (median)",
  "Svartid (p95)",
  "Kostnad totalt",
];

const CELL = "py-2 pr-4 whitespace-nowrap";

export function DayTable({ days }: { days: DayStats[] }) {
  if (days.length === 0) {
    return <p className="text-muted">Ingen spørsmål de siste 14 dagene.</p>;
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
          {days.map(({ date, stats }) => (
            <tr key={date} className="border-t border-border">
              <td className={CELL}>{formatDate(date)}</td>
              <td className={CELL}>{formatCount(stats.calls)}</td>
              <td className={CELL}>{formatCount(stats.answered)}</td>
              <td className={CELL}>{formatCount(stats.not_found)}</td>
              <td className={CELL}>{formatCount(stats.out_of_scope)}</td>
              <td className={CELL}>{formatCount(stats.errors)}</td>
              <td className={CELL}>{formatPercent(stats.error_rate)}</td>
              <td className={CELL}>{formatNok(stats.cost_nok_median)}</td>
              <td className={CELL}>{formatMs(stats.latency_ms_p95)}</td>
              <td className={CELL}>{formatNok(stats.cost_nok_total)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
