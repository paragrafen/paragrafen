"""A/B comparison between two runs written by run_eval.py.

uv run python compare.py results/<run-a> results/<run-b>
"""

import argparse
import json
import sys
from pathlib import Path

from run_eval import SLICES, format_cell, load_jsonl

# Metrics where a lower value is better; everything else is higher-is-better.
LOWER_IS_BETTER = {
    "over_abstention_rate",
    "wrongful_answer_rate",
    "n_errors",
    "contradicts_sources",
    "cites_wrong_section",
    "unanswerable_with_citations",
}
SKIP = {"n", "n_answerable", "n_unanswerable"}


def load_run(run_dir: Path) -> tuple[dict, dict[str, dict]]:
    summary = json.loads((run_dir / "summary.json").read_text(encoding="utf-8"))
    rows = {r["id"]: r for r in load_jsonl(run_dir / "rows.jsonl")}
    return summary, rows


def delta_table(a: dict, b: dict) -> list[tuple[str, object, object, str]]:
    """(metric, a, b, delta with a +/- marker for better/worse) for metrics present in both."""
    out = []
    for metric, va in a.items():
        vb = b.get(metric)
        if (
            metric in SKIP
            or not isinstance(va, (int, float))
            or not isinstance(vb, (int, float))
        ):
            continue
        diff = vb - va
        better = diff < 0 if metric in LOWER_IS_BETTER else diff > 0
        marker = "" if abs(diff) < 1e-9 else (" better" if better else " worse")
        out.append((metric, va, vb, f"{diff:+.3f}{marker}"))
    return out


def question_changes(
    rows_a: dict, rows_b: dict, recall_key: str, threshold: float
) -> list[str]:
    """Human-readable lines for questions whose outcome changed between the runs."""
    lines = []
    for qid in sorted(rows_a.keys() & rows_b.keys()):
        a, b = rows_a[qid], rows_b[qid]
        changes = []
        if a.get("status") != b.get("status"):
            ok = b.get("status") == b["expected_status"]
            changes.append(
                f"status {a.get('status')} -> {b.get('status')} ({'fixed' if ok else 'broke'})"
            )
        for key in (recall_key, "faithfulness"):
            va, vb = a.get(key), b.get(key)
            if va is not None and vb is not None and abs(vb - va) >= threshold:
                changes.append(f"{key} {va:.2f} -> {vb:.2f}")
        if changes:
            lines.append(f"  {qid} [{b.get('difficulty')}]: " + "; ".join(changes))
    only = sorted(rows_a.keys() ^ rows_b.keys())
    if only:
        lines.append(f"  in only one run: {', '.join(only)}")
    return lines


def print_deltas(title: str, a: dict, b: dict) -> None:
    rows = delta_table(a, b)
    if not rows:
        return
    width = max(len(r[0]) for r in rows) + 2
    print(f"\n{title}  (n={a.get('n')} -> {b.get('n')})")
    for metric, va, vb, diff in rows:
        print(
            f"  {metric.ljust(width)}{format_cell(va).rjust(8)}{format_cell(vb).rjust(8)}  {diff}"
        )


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("run_a", type=Path, help="baseline run directory")
    parser.add_argument("run_b", type=Path, help="candidate run directory")
    parser.add_argument(
        "--slices",
        action="store_true",
        help="also compare per difficulty",
    )
    parser.add_argument(
        "--threshold",
        type=float,
        default=0.25,
        help="min change in recall/faithfulness to list a question",
    )
    args = parser.parse_args(argv)

    summary_a, rows_a = load_run(args.run_a)
    summary_b, rows_b = load_run(args.run_b)
    print(f"A: {summary_a['meta']['run']}")
    print(f"B: {summary_b['meta']['run']}")
    if rows_a.keys() != rows_b.keys():
        print(
            "Warning: the runs cover different questions, so the overall numbers are not directly comparable."
        )

    print_deltas("Overall", summary_a["overall"], summary_b["overall"])
    if args.slices:
        for field in SLICES:
            for name in sorted(summary_a[field].keys() & summary_b[field].keys()):
                print_deltas(
                    f"{field} = {name}", summary_a[field][name], summary_b[field][name]
                )

    print(
        f"\nChanged questions (status, recall or faithfulness by >= {args.threshold}):"
    )
    lines = question_changes(rows_a, rows_b, "recall", args.threshold)
    print("\n".join(lines) if lines else "  none")
    return 0


if __name__ == "__main__":
    sys.exit(main())
