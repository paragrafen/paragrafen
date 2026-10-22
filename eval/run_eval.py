"""
Run the eval datasets against the /ask API and write per-question results and a summary.
/ask returns status, answer and sources (note: the sections the answer cites, not every retrieved chunk).
API_URL=http://localhost:5073 uv run python run_eval.py --config baseline --prompt-version v0
"""

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from collections import defaultdict
from datetime import datetime
from pathlib import Path
from statistics import mean

from metrics.abstention import ANSWERED, abstention_metrics, map_status
from metrics.citations import citation_metrics, cited_keys_from_answer
from metrics.retrieval import dedupe, key_from_item, retrieval_set_metrics

EVAL_DIR = Path(__file__).parent
DATASETS = [
    EVAL_DIR / "datasets" / "answerable.jsonl",
    EVAL_DIR / "datasets" / "unanswerable.jsonl",
]
SLICES = ["difficulty"]


def load_jsonl(path: Path) -> list[dict]:
    """Create one dict per question in the .jsonl files."""
    with path.open(encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def load_questions() -> list[dict]:
    """All questions from both datasets."""
    return [q for path in DATASETS for q in load_jsonl(path)]


def ask(api_url: str, question: str, args) -> dict:
    """POST the question to {api_url}/ask and return the JSON response."""
    body = {"question": question}
    request = urllib.request.Request(
        f"{api_url.rstrip('/')}/ask",
        data=json.dumps(body).encode(),
        headers={"content-type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=args.timeout) as response:
        return json.load(response)


def source_contexts(sources: list[dict]) -> list[dict]:
    """Section texts for the faithfulness judge, one entry per section key in source order."""
    texts = defaultdict(list)
    for item in sources:
        if item.get("text"):
            texts[key_from_item(item)].append(item["text"])
    return [{"key": key, "text": "\n".join(parts)} for key, parts in texts.items()]


def evaluate(
    q: dict, response: dict | None, error: str | None, args, judge_client
) -> dict:
    """
    One result row for a question: dataset fields, the API's status, answer and section keys, and the metrics.
    Faithfulness is only judged for answered questions, and only when 'judge_client' is given.
    """
    row = {
        "id": q["id"],
        "question": q["question"],
        "law_area": q["law_area"],
        "difficulty": q["difficulty"],
        "expected_status": q["expected_status"],
        "expected_sections": q["expected_sections"],
        "acceptable_sections": q["acceptable_sections"],
        "api_status": None,
        "status": None,
        "error": error,
    }
    if response is None:
        return row

    sources = response.get("sources") or []
    answer = response.get("answer") or ""
    row.update(
        request_id=response.get("requestId") or response.get("request_id"),
        api_status=response.get("status"),
        status=map_status(response.get("status")),
        answer=answer,
    )
    try:
        retrieved_keys = dedupe(key_from_item(s) for s in sources)
        cited_keys = cited_keys_from_answer(answer, sources)
    except KeyError as e:  # a source without a readable law ID or section
        row["error"] = f"could not score: {e}"
        return row
    row.update(retrieved_keys=retrieved_keys, cited_keys=cited_keys)
    if row["status"] is None:
        row["error"] = f"unknown API status: {response.get('status')!r}"
    elif "sources" not in response:
        row["error"] = "response has no 'sources' field"

    if q["expected_sections"] and "sources" in response:
        row.update(
            retrieval_set_metrics(
                retrieved_keys, q["expected_sections"], q["acceptable_sections"]
            )
        )
    row.update(
        citation_metrics(
            cited_keys, retrieved_keys, q["expected_sections"], q["acceptable_sections"]
        )
    )
    if judge_client is not None and row["status"] == ANSWERED:
        from metrics.faithfulness import judge_faithfulness

        try:
            row.update(
                judge_faithfulness(
                    q["question"], row["answer"], source_contexts(sources), judge_client
                )
            )
        except Exception as e:  # noqa: BLE001 - keep the run going
            row["judge_error"] = f"{type(e).__name__}: {e}"
    return row


def metric_keys(args) -> list[str]:
    """Per-question metrics to average in the summary."""
    keys = [
        "recall",
        "precision",
        "all_found",
        "citation_validity",
        "citation_precision",
        "citation_recall",
    ]
    if args.judge:
        keys += ["faithfulness", "contradicts_sources", "cites_wrong_section"]
    return keys


def aggregate(rows: list[dict], keys: list[str]) -> dict:
    """Abstention metrics plus the mean of each per-question metric."""
    out = {"n": len(rows), **abstention_metrics(rows)}
    for key in keys:
        values = [r[key] for r in rows if r.get(key) is not None]
        out[key] = mean(values) if values else None
    unanswerable = [
        r for r in rows if r["expected_status"] != ANSWERED and r["status"] is not None
    ]
    out["unanswerable_with_citations"] = (
        mean(bool(r.get("cited_keys")) for r in unanswerable) if unanswerable else None
    )
    return out


def summarise(rows: list[dict], keys: list[str]) -> dict:
    """Aggregated metrics for all rows ('overall') and per value of each slice field (difficulty)."""
    summary = {"overall": aggregate(rows, keys)}
    for field in SLICES:
        groups = defaultdict(list)
        for r in rows:
            groups[r[field] or "unknown"].append(r)
        summary[field] = {
            name: aggregate(group, keys) for name, group in sorted(groups.items())
        }
    return summary


def format_cell(value) -> str:
    """Table cell text: "-" for None, three decimals for floats."""
    if value is None:
        return "-"
    if isinstance(value, float):
        return f"{value:.3f}"
    return str(value)


def print_table(title: str, table: dict[str, dict], columns: list[str]) -> None:
    """Print one row per table entry, skipping columns that have no values."""
    columns = [
        c for c in columns if any(row.get(c) is not None for row in table.values())
    ]
    width = max(len(name) for name in table) + 2
    print(f"\n{title}")
    print("".ljust(width) + "".join(c.rjust(max(len(c), 6) + 2) for c in columns))
    for name, row in table.items():
        print(
            name.ljust(width)
            + "".join(
                format_cell(row.get(c)).rjust(max(len(c), 6) + 2) for c in columns
            )
        )


def parse_args(argv=None):
    """Command-line options; 'argv' is for tests (None reads sys.argv)."""
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--api-url", default=os.environ.get("API_URL"), help="defaults to $API_URL"
    )
    parser.add_argument("--config", help="pipeline config label, stored with the run")
    parser.add_argument(
        "--prompt-version", help="prompt version label, stored with the run"
    )
    parser.add_argument(
        "--judge", action="store_true", help="score faithfulness with an LLM judge"
    )
    parser.add_argument(
        "--answer-model",
        default=os.environ.get("ANSWER_MODEL", "gpt-6-luna"),
        help="model the backend answers with, stored with the run",
    )
    parser.add_argument("--limit", type=int, help="only run the first N questions")
    parser.add_argument("--ids", nargs="+", help="only run these question ids")
    parser.add_argument(
        "--timeout", type=float, default=120.0, help="seconds per API request"
    )
    parser.add_argument("--out", type=Path, default=EVAL_DIR / "results")
    return parser.parse_args(argv)


def main(argv=None) -> int:
    """
    Ask every selected question, score it, and write rows.jsonl and summary.json to a new folder in --out.
    """
    args = parse_args(argv)
    if not args.api_url:
        print("Set API_URL or pass --api-url.", file=sys.stderr)
        return 2

    questions = load_questions()
    if args.ids:
        questions = [q for q in questions if q["id"] in set(args.ids)]
    if args.limit:
        questions = questions[: args.limit]

    judge_client, judge_model = None, None
    if args.judge:
        from metrics.faithfulness import JUDGE_MODEL, make_judge_client

        judge_client = make_judge_client()
        judge_model = JUDGE_MODEL

    rows = []
    for i, q in enumerate(questions, 1):
        response, error = None, None
        try:
            response = ask(args.api_url, q["question"], args)
        except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as e:
            error = f"{type(e).__name__}: {e}"
        row = evaluate(q, response, error, args, judge_client)
        rows.append(row)
        print(
            f"[{i}/{len(questions)}] {q['id']} {row['status'] or 'ERROR'}"
            + (f"  ({row['error']})" if row["error"] else "")
        )

    keys = metric_keys(args)
    summary = summarise(rows, keys)
    started = datetime.now().astimezone().strftime("%Y%m%d-%H%M%S")
    run_name = "_".join(filter(None, [started, args.config, args.prompt_version]))
    run_dir = args.out / run_name
    run_dir.mkdir(parents=True, exist_ok=True)
    meta = {
        "run": run_name,
        "api_url": args.api_url,
        "config": args.config,
        "prompt_version": args.prompt_version,
        "judge": args.judge,
        "judge_model": judge_model,
        "answer_model": args.answer_model,
    }
    with (run_dir / "rows.jsonl").open("w", encoding="utf-8") as f:
        for row in rows:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
    (run_dir / "summary.json").write_text(
        json.dumps({"meta": meta, **summary}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    columns = ["n", "over_abstention_rate", "wrongful_answer_rate", "n_errors", *keys]
    print_table("Overall", {"all": summary["overall"]}, columns)
    for field in SLICES:
        print_table(f"By {field}", summary[field], columns)
    print(f"\nWrote {run_dir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
