"""
Retrieval metrics on section keys: recall@k, precision@k, MRR, all_found.
A section key is "{lawId}/{sectionRef without whitespace}", e.g. "LOV-2005-06-17-62/§15-8".

Retrieval metrics for one question. Run on answerable questions only.
"""

import re


def section_key(law_id: str, section: str) -> str:
    """
    Remove whitespace from sectionRef.
    ("LOV-1981-04-08-7", "§ 42 a") -> "LOV-1981-04-08-7/§42a".
    """
    return f"{law_id.strip()}/{re.sub(r'\s+', '', section)}"


_LOVDATA_DOCUMENT_URL = re.compile(
    r"/dokument/(?:NL/lov|SF/forskrift)/(\d{4}-\d{2}-\d{2}-\d+)(?:[/?#]|$)"
)
_ID_PREFIX = {"lov": "LOV", "forskrift": "FOR"}


def law_id_from_url(url: str | None) -> str | None:
    """
    Law or regulation ID from a Lovdata document URL, or None for any other URL.
    "https://lovdata.no/dokument/NL/lov/2005-06-17-62" -> "LOV-2005-06-17-62",
    "https://lovdata.no/dokument/SF/forskrift/2005-06-22-697" -> "FOR-2005-06-22-697".
    """
    match = _LOVDATA_DOCUMENT_URL.search(url or "")
    if not match:
        return None
    kind = "forskrift" if "/forskrift/" in match.group(0) else "lov"
    return f"{_ID_PREFIX[kind]}-{match.group(1)}"


def key_from_item(item: dict) -> str:
    """
    Section key for one source from the API.
    """
    law = item.get("law") or ""
    law_id = (
        law if law.startswith(("LOV-", "FOR-")) else law_id_from_url(item.get("url"))
    )
    if not law_id or not item.get("section"):
        raise KeyError(
            f"source needs a law ID ('law' or a Lovdata 'url') and 'section' to build a section key: {item!r}"
        )
    return section_key(law_id, item["section"])


def dedupe(keys):
    """Many chunks can belong to one §. Keep the first (highest-ranked) occurrence of each key."""
    seen, out = set(), []
    for k in keys:
        if k not in seen:
            seen.add(k)
            out.append(k)
    return out


# note: this might be redundant, if we sort retrieved[] in backend by vector distance
def retrieval_set_metrics(
    retrieved: list[str], expected: list[str], acceptable: list[str] = ()
) -> dict:
    """
    Order-independent metrics for one question, over every retrieved section.
    Used while the API returns no rank or score; recall and precision cover the whole list, not a top k.
    """

    if not expected:
        raise ValueError("retrieval_set_metrics needs at least one expected section")

    retrieved, exp = set(retrieved), set(expected)
    hits = exp & retrieved

    return {
        "recall": len(hits) / len(exp),  # share of expected docs retrieved
        "precision": len((exp | set(acceptable)) & retrieved)
        / max(len(retrieved), 1),  # share of retrieved docs that are relevant
        "all_found": float(hits == exp),  # were all expected docs retrieved
    }


def retrieval_metrics(
    retrieved: list[str], expected: list[str], k: int, acceptable: list[str] = ()
) -> dict:
    """
    Metrics for one question. 'retrieved' is the ranked list of section keys.
    Recall, MRR and all_found use 'expected' only; precision also counts 'acceptable' as relevant.
    """

    if not expected:
        raise ValueError("retrieval_metrics needs at least one expected section")

    retrieved = dedupe(retrieved)
    top, exp = retrieved[:k], set(expected)
    relevant = exp | set(acceptable)
    hits = exp & set(top)
    rr = next((1 / (i + 1) for i, key in enumerate(retrieved) if key in exp), 0.0)

    return {
        f"recall@{k}": len(hits)
        / len(exp),  # completeness: proportion of relevant docs that were retrieved
        f"precision@{k}": len(relevant & set(top))
        / max(len(top), 1),  # accuracy: proportion of retrieved docs that were relevant
        "mrr": rr,  # mean reciprocal rank
        f"all_found@{k}": float(hits == exp),  # were all expected docs retrieved
    }
