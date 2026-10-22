"""
Citation metrics: are the sections the answer cites retrieved and relevant?
- Citations are the [n] links the backend resolves: [1] is sources[0] (AskService.WithCitedSources).
- Mentions are the "§ x" references written in the answer text, checked separately.
"""

import re
from collections import defaultdict

from metrics.retrieval import dedupe, key_from_item

# letters are limited to a-h so "§ 10-8 i loven" isn't read as "§ 10-8i"
_CITATION = re.compile(r"§\s*(\d+(?:-\d+)?(?:\s?[a-h](?![a-zæøå\d]))?)")


# note: can lead to lower precision when several laws are retrieved, and the section reference is ambiguous
def mentioned_keys_from_answer(answer: str, sources: list[dict]) -> list[str]:
    """
    Section keys of the "§ x" references written in the answer, matched against the sources by section number.
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


_MARKER = re.compile(r"\[(\d+)\]")


def cited_keys_from_markers(answer: str, sources: list[dict]) -> list[str]:
    """
    Section keys of the [n] citations in the answer.
    A number outside the sources gives "?/[n]", which counts as not retrieved.
    """
    cited = []
    for number in _MARKER.findall(answer or ""):
        n = int(number)
        cited.append(
            key_from_item(sources[n - 1]) if 1 <= n <= len(sources) else f"?/[{n}]"
        )
    return dedupe(cited)


def mention_validity(mentioned: list[str], retrieved: list[str]) -> float | None:
    """Share of the § references written in the answer that are among the sources."""
    mentioned = set(mentioned)
    return len(mentioned & set(retrieved)) / len(mentioned) if mentioned else None


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
