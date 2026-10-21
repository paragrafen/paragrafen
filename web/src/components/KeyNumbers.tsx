import { formatCount, formatMs, formatNok, formatPercent } from "@/lib/format";
import type { CallStats } from "@/types";

export function KeyNumbers({ stats }: { stats: CallStats }) {
  const tiles = [
    ["Spørsmål", formatCount(stats.calls)],
    ["Feilrate", formatPercent(stats.error_rate)],
    ["Kostnad per spørsmål (median)", formatNok(stats.cost_nok_median)],
    ["Kostnad per spørsmål (p95)", formatNok(stats.cost_nok_p95)],
    ["Svartid (median)", formatMs(stats.latency_ms_p50)],
    ["Svartid (p95)", formatMs(stats.latency_ms_p95)],
    ["Total kostnad", formatNok(stats.cost_nok_total)],
    [
      "Tokens inn / ut",
      `${formatCount(stats.input_tokens)} / ${formatCount(stats.output_tokens)}`,
    ],
  ];

  return (
    <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {tiles.map(([label, value]) => (
        <div key={label} className="rounded-xl border border-border bg-surface p-4">
          <dt className="text-sm text-muted">{label}</dt>
          <dd className="mt-1 text-xl font-semibold tabular-nums">{value}</dd>
        </div>
      ))}
    </dl>
  );
}
