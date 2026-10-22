"""
Citation metrics: are the sections the answer cites retrieved and relevant?
"""

import re
from collections import defaultdict

from metrics.retrieval import dedupe, key_from_item

# letters are limited to a-h so "§ 10-8 i loven" isn't read as "§ 10-8i"
_CITATION = re.compile(r"§\s*(\d+(?:-\d+)?(?:\s?[a-h](?![a-zæøå\d]))?)")


# note: can lead to lower precision when several laws are retrieved, and the section reference is ambiguous
def cited_keys_from_answer(answer: str, sources: list[dict]) -> list[str]:
    """
    Section keys the answer refers to, matched against the source chunks by section number.
    - A section number found in several retrieved laws counts as cited in each of them.
    - A section number that matches no source gives "?/§x", which counts as not retrieved.
    Only "§ x" is read, so a number without its own § ("§ 14-2 og 14-3") is not counted.
    """

    laws_by_section = defaultdict(list)
    for source in sources:
        law, section = key_from_item(source).split("/", 1)
        if law not in laws_by_section[section]:
            laws_by_section[section].append(law)

    cited = []
    for ref in _CITATION.findall(answer or ""):
        section = "§" + re.sub(r"\s+", "", ref)
        cited += [f"{law}/{section}" for law in laws_by_section.get(section) or ["?"]]
    return dedupe(cited)


def citation_metrics(
    cited: list[str],
    retrieved: list[str],
    expected: list[str],
    acceptable: list[str] = (),
) -> dict:
    """
    Metrics for one question. All arguments are section keys.
    - validity: share of cited sections that were retrieved.
    - precision: share of cited sections in expected + acceptable.
    - recall: share of expected sections that are cited.
    """

    cited = set(cited)
    valid = cited & set(retrieved)
    relevant = set(expected) | set(acceptable)

    return {
        "n_cited": len(cited),
        "citation_validity": len(valid) / len(cited) if cited else None,
        "citation_precision": len(cited & relevant) / len(cited)
        if cited and relevant
        else None,
        "citation_recall": len(cited & set(expected)) / len(expected)
        if expected
        else None,
    }
