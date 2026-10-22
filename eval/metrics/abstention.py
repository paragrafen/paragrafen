"""
Abstention metrics: over-abstention on answerable questions, wrongful answers on unanswerable ones.
"""

from statistics import mean

ANSWERED = "ANSWERED"
ABSTAINED = "ABSTAINED"

# API status (web/src/types.ts AnswerStatus) -> eval status
STATUS_MAP = {
    "answered": ANSWERED,
    "partial": ANSWERED,  # PARTIAL is in the prompt, not yet in the backend's AnswerStatus
    "not_found": ABSTAINED,
    "out_of_scope": ABSTAINED,
}


def map_status(api_status: str | None) -> str | None:
    """Map an API status to ANSWERED/ABSTAINED. Unknown or missing statuses map to None."""
    if api_status is None:
        return None
    return STATUS_MAP.get(api_status.lower())


def _rate(values) -> float | None:
    """Helper function. Calculate average of observations."""
    values = list(values)
    return mean(values) if values else None


def abstention_metrics(rows: list[dict]) -> dict:
    """
    Run on both datasets together.

    'rows' need 'expected_status' (from the dataset) and 'status' (mapped from the API).
    Rows whose 'status' is None (request failed, unknown status) are counted in n_errors and skipped.
    Rows with a readable status count in the rates even if their retrieval/citation scoring failed;
    those are counted in n_scoring_errors by run_eval.aggregate().
    """

    scored = [r for r in rows if r["status"] is not None]
    answerable = [r for r in scored if r["expected_status"] == ANSWERED]
    unanswerable = [r for r in scored if r["expected_status"] == ABSTAINED]

    return {
        "over_abstention_rate": _rate(r["status"] == ABSTAINED for r in answerable),
        "wrongful_answer_rate": _rate(r["status"] == ANSWERED for r in unanswerable),
        "accuracy": _rate(r["status"] == r["expected_status"] for r in scored),
        "n_answerable": len(answerable),
        "n_unanswerable": len(unanswerable),
        "n_errors": len(rows) - len(scored),
    }
