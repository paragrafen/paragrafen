import json
from types import SimpleNamespace

import pytest

from metrics.abstention import abstention_metrics, map_status
from metrics.citations import (
    citation_metrics,
    cited_keys_from_markers,
    mention_validity,
    mentioned_keys_from_answer,
)
from metrics.faithfulness import (
    FaithfulnessJudgement,
    faithfulness_score,
    judge_faithfulness,
    make_judge_client,
)
from metrics.retrieval import (
    key_from_item,
    retrieval_metrics,
    retrieval_set_metrics,
    section_key,
)

AML = "LOV-2005-06-17-62"


# --- section keys ---


def test_section_key_removes_whitespace():
    assert section_key("LOV-1898-12-10-1", "§ 1") == "LOV-1898-12-10-1/§1"
    assert section_key("LOV-1981-04-08-7", "§ 42 a") == "LOV-1981-04-08-7/§42a"


def test_key_from_item_uses_law_and_section():
    assert (
        key_from_item({"chunk_id": "x:y", "law": AML, "section": "§ 15-8"})
        == f"{AML}/§15-8"
    )
    with pytest.raises(KeyError):
        key_from_item({"chunk_id": f"{AML}:§15-8", "section": "§ 15-8"})


def test_key_from_item_takes_law_id_from_url_when_law_is_a_title():
    source = {
        "law": "Lov om arbeidsmiljø, arbeidstid og stillingsvern mv. (arbeidsmiljøloven)",
        "section": "§ 15-8",
        "url": "https://lovdata.no/dokument/NL/lov/2005-06-17-62",
    }
    assert key_from_item(source) == f"{AML}/§15-8"


def test_key_from_item_reads_law_id_from_url_with_section():
    source = {
        "law": "Arbeidsmiljøloven",
        "section": "§ 15-8",
        "url": "https://lovdata.no/dokument/NL/lov/2005-06-17-62/%C2%A715-8",
    }
    assert key_from_item(source) == f"{AML}/§15-8"


def test_key_from_item_takes_regulation_id_from_url():
    source = {
        "law": "Forskrift om utførelse av arbeid",
        "section": "§ 1-1",
        "url": "https://lovdata.no/dokument/SF/forskrift/2011-12-06-1357",
    }
    assert key_from_item(source) == "FOR-2011-12-06-1357/§1-1"


def test_key_from_item_without_law_id_or_lovdata_url():
    with pytest.raises(KeyError):
        key_from_item({"law": "Arbeidsmiljøloven", "section": "§ 15-8"})
    with pytest.raises(KeyError):
        key_from_item(
            {
                "law": "Arbeidsmiljøloven",
                "section": "§ 15-8",
                "url": "https://example.com/x",
            }
        )


# --- retrieval ---


def test_retrieval_dedupes_chunks_of_the_same_section():
    retrieved = [f"{AML}/§15-8", f"{AML}/§15-8", f"{AML}/§15-7"]
    m = retrieval_metrics(retrieved, [f"{AML}/§15-7"], k=2)
    assert m["recall@2"] == 1.0
    assert m["mrr"] == 0.5


def test_retrieval_counts_acceptable_only_for_precision():
    m = retrieval_metrics(["a", "b", "c"], expected=["a", "x"], k=3, acceptable=["b"])
    assert m["recall@3"] == 0.5
    assert m["precision@3"] == pytest.approx(2 / 3)
    assert m["all_found@3"] == 0.0


def test_retrieval_miss_and_empty_retrieval():
    assert retrieval_metrics([], ["a"], k=5) == {
        "recall@5": 0.0,
        "precision@5": 0.0,
        "mrr": 0.0,
        "all_found@5": 0.0,
    }


def test_retrieval_rejects_empty_expected():
    with pytest.raises(ValueError):
        retrieval_metrics(["a"], [], k=5)


# --- abstention ---


def test_map_status():
    assert map_status("answered") == "ANSWERED"
    assert map_status("not_found") == "ABSTAINED"
    assert map_status("out_of_scope") == "ABSTAINED"
    assert map_status("partial") == "ANSWERED"
    assert map_status("maybe") is None
    assert map_status(None) is None


def test_abstention_rates():
    rows = [
        {"expected_status": "ANSWERED", "status": "ANSWERED"},
        {"expected_status": "ANSWERED", "status": "ABSTAINED"},
        {"expected_status": "ABSTAINED", "status": "ANSWERED"},
        {"expected_status": "ABSTAINED", "status": None},
    ]
    m = abstention_metrics(rows)
    assert m["over_abstention_rate"] == 0.5
    assert m["wrongful_answer_rate"] == 1.0
    assert m["n_errors"] == 1
    assert (m["n_answerable"], m["n_unanswerable"]) == (2, 1)


def test_abstention_empty_slice_returns_none():
    m = abstention_metrics([{"expected_status": "ABSTAINED", "status": "ABSTAINED"}])
    assert m["over_abstention_rate"] is None
    assert m["wrongful_answer_rate"] == 0.0


def test_retrieval_set_metrics_ignore_order_and_duplicates():
    retrieved = [f"{AML}/§15-7", f"{AML}/§15-8", f"{AML}/§15-8", f"{AML}/§1-1"]
    m = retrieval_set_metrics(
        retrieved, [f"{AML}/§15-8", f"{AML}/§15-9"], acceptable=[f"{AML}/§15-7"]
    )
    assert m == {"recall": 0.5, "precision": 2 / 3, "all_found": 0.0}
    with pytest.raises(ValueError):
        retrieval_set_metrics(retrieved, [])


# --- citations ---


HL = "LOV-1999-03-26-17"
SOURCES = [
    {"law": AML, "section": "§ 10-8"},
    {"law": AML, "section": "§ 10-9"},
    {"law": AML, "section": "§ 42 a"},
    {"law": HL, "section": "§ 10-8"},
]


def test_mentioned_keys_from_answer_one_section_per_sign():
    assert mentioned_keys_from_answer("Se § 10-9 i loven.", SOURCES) == [f"{AML}/§10-9"]
    assert mentioned_keys_from_answer("Se § 10-9 og § 42 a", SOURCES) == [
        f"{AML}/§10-9",
        f"{AML}/§42a",
    ]
    assert mentioned_keys_from_answer("jf. §10-9, § 42 a", SOURCES) == [
        f"{AML}/§10-9",
        f"{AML}/§42a",
    ]
    # a number without its own § is not a citation
    assert mentioned_keys_from_answer("§ 10-9 og 42 a", SOURCES) == [f"{AML}/§10-9"]
    assert mentioned_keys_from_answer("etter § 10-9 og 11 timer", SOURCES) == [
        f"{AML}/§10-9"
    ]
    assert mentioned_keys_from_answer("§ 10-9, 2. ledd", SOURCES) == [f"{AML}/§10-9"]


def test_mentioned_keys_from_answer_ambiguous_and_unmatched():
    # § 10-8 exists in both retrieved laws, so both count as cited
    assert mentioned_keys_from_answer("§ 10-8", SOURCES) == [
        f"{AML}/§10-8",
        f"{HL}/§10-8",
    ]
    # a section that was never retrieved can't be given a law
    assert mentioned_keys_from_answer("§ 99", SOURCES) == ["?/§99"]
    assert mentioned_keys_from_answer("Ingen paragrafer her.", SOURCES) == []


def test_cited_keys_from_markers_follow_the_sources():
    assert cited_keys_from_markers("A [2]. B [1]. C [2].", SOURCES) == [
        f"{AML}/§10-9",
        f"{AML}/§10-8",
    ]
    # a citation without a written § still counts
    assert cited_keys_from_markers("Du har krav på pause [1].", SOURCES) == [
        f"{AML}/§10-8"
    ]


def test_cited_keys_from_markers_outside_sources_and_none():
    assert cited_keys_from_markers("Se [9].", SOURCES) == ["?/[9]"]
    assert cited_keys_from_markers("§ 10-8 uten lenke.", SOURCES) == []


def test_mention_validity():
    assert mention_validity([f"{AML}/§10-8", "?/§99"], [f"{AML}/§10-8"]) == 0.5
    assert mention_validity([], [f"{AML}/§10-8"]) is None


def test_citations_flag_sections_that_were_not_retrieved():
    m = citation_metrics(cited=["a", "z"], retrieved=["a", "b"], expected=["a", "b"])
    assert m["citation_validity"] == 0.5
    assert m["citation_precision"] == 0.5
    assert m["citation_recall"] == 0.5


def test_citations_without_citations_or_expected():
    m = citation_metrics(cited=[], retrieved=["a"], expected=[])
    assert m == {
        "n_cited": 0,
        "citation_validity": None,
        "citation_precision": None,
        "citation_recall": None,
    }


# --- faithfulness ---

JUDGEMENT = {
    "claims": [
        {
            "claim": "Fristen er 12 måneder",
            "verdict": "supported",
            "section_key": f"{AML}/§15-8",
        },
        {
            "claim": "Gjelder også ved ferie",
            "verdict": "partially_supported",
            "section_key": None,
        },
        {
            "claim": "Arbeidsgiver må betale bot",
            "verdict": "unsupported",
            "section_key": None,
        },
    ],
    "contradicts_sources": False,
    "cites_wrong_section": True,
    "explanation": "",
}


def test_faithfulness_score():
    m = faithfulness_score(FaithfulnessJudgement.model_validate(JUDGEMENT))
    assert m["faithfulness"] == 0.5
    assert m["n_unsupported"] == 1
    assert m["cites_wrong_section"] == 1.0


def test_faithfulness_no_claims_is_none():
    judgement = FaithfulnessJudgement(
        claims=[], contradicts_sources=False, cites_wrong_section=False, explanation=""
    )
    assert faithfulness_score(judgement)["faithfulness"] is None


class FakeClient:
    def __init__(self, finish_reason="stop", payload=JUDGEMENT, refusal=None):
        self.calls = []
        message = SimpleNamespace(content=json.dumps(payload), refusal=refusal)
        response = SimpleNamespace(
            choices=[SimpleNamespace(finish_reason=finish_reason, message=message)]
        )
        create = lambda **kwargs: self.calls.append(kwargs) or response
        self.chat = SimpleNamespace(completions=SimpleNamespace(create=create))


def test_judge_faithfulness_parses_structured_output():
    client = FakeClient()
    m = judge_faithfulness(
        "Spørsmål?",
        "Svar.",
        [{"key": f"{AML}/§15-8", "text": "Lovtekst"}],
        client,
        model="judge-deployment",
    )
    assert m["faithfulness"] == 0.5
    assert m["judgement"]["claims"][0]["verdict"] == "supported"
    call = client.calls[0]
    assert call["model"] == "judge-deployment"
    assert call["response_format"]["json_schema"]["strict"] is True
    assert "Lovtekst" in call["messages"][0]["content"]


def test_judge_faithfulness_handles_refusal_length_and_missing_context():
    contexts = [{"key": "k", "text": "t"}]
    assert (
        judge_faithfulness("q", "a", contexts, FakeClient(refusal="no"))["judge_error"]
        == "judge refused"
    )
    assert (
        judge_faithfulness("q", "a", contexts, FakeClient("length"))["faithfulness"]
        is None
    )
    assert judge_faithfulness("q", "a", [], FakeClient())["judge_error"]


def test_make_judge_client_uses_api_key(monkeypatch):
    monkeypatch.setenv(
        "AZURE_OPENAI_ENDPOINT", "https://test.cognitiveservices.azure.com/"
    )
    monkeypatch.setenv("AZURE_OPENAI_API_KEY", "dummy")
    client = make_judge_client()
    assert type(client).__name__ == "OpenAI"
    assert str(client.base_url) == "https://test.cognitiveservices.azure.com/openai/v1/"
    assert client.api_key == "dummy"


def test_make_judge_client_uses_entra_id_without_api_key(monkeypatch):
    pytest.importorskip("azure.identity")
    monkeypatch.setenv(
        "AZURE_OPENAI_ENDPOINT", "https://test.cognitiveservices.azure.com/"
    )
    monkeypatch.delenv("AZURE_OPENAI_API_KEY", raising=False)
    client = make_judge_client()
    assert client._api_key_provider is not None
