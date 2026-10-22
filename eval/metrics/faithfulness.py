"""
Faithfulness: LLM-as-judge checks that every claim in the answer is supported by the law text it was given.
The judge sees only the question, the answer and the retrieved section texts, so it measures grounding,
not legal correctness.
"""

import json
import os
from typing import Literal

from pydantic import BaseModel

# note: the judge is in the same model family as the answer model (gpt-6-luna), so scores may be somewhat lenient
JUDGE_MODEL = os.environ.get(
    "JUDGE_MODEL", "gpt-6.1-sol"
)  # the Foundry deployment name

RUBRIC = """\
You are assessing whether an answer from a legal assistant is grounded in the law texts it was given.
The question, the answer and the law texts are in Norwegian.

1. Split the answer into self-contained claims about what the law says (deadlines, amounts, conditions,
   rights, obligations). Leave out pleasantries, referrals to a lawyer and general disclaimers.
   Write each claim in Norwegian, as close to the wording of the answer as possible.
2. Judge each claim against the law texts below only, not against your own knowledge:
   - "supported": the law text says this directly, or it clearly follows from it.
   - "partially_supported": the law text covers part of the claim, but something is added or generalised.
   - "unsupported": the law texts do not say this, or say something different.
3. Give the key of the source that supports the claim (e.g. "LOV-2005-06-17-62/§15-8"), or null.
4. Set contradicts_sources to true if the answer says something that conflicts with the law texts.
5. Set cites_wrong_section to true if the answer refers to a section that does not contain what the
   answer attributes to it.
6. Write the explanation in English.
"""


class ClaimJudgement(BaseModel):
    claim: str
    verdict: Literal["supported", "partially_supported", "unsupported"]
    section_key: str | None


class FaithfulnessJudgement(BaseModel):
    claims: list[ClaimJudgement]
    contradicts_sources: bool
    cites_wrong_section: bool
    explanation: str


_SCHEMA = {
    "type": "object",
    "properties": {
        "claims": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "claim": {"type": "string"},
                    "verdict": {
                        "type": "string",
                        "enum": ["supported", "partially_supported", "unsupported"],
                    },
                    "section_key": {"type": ["string", "null"]},
                },
                "required": ["claim", "verdict", "section_key"],
                "additionalProperties": False,
            },
        },
        "contradicts_sources": {"type": "boolean"},
        "cites_wrong_section": {"type": "boolean"},
        "explanation": {"type": "string"},
    },
    "required": ["claims", "contradicts_sources", "cites_wrong_section", "explanation"],
    "additionalProperties": False,
}

_VERDICT_SCORE = {"supported": 1.0, "partially_supported": 0.5, "unsupported": 0.0}


def make_judge_client():
    """API key if AZURE_OPENAI_API_KEY is set, otherwise Entra ID."""
    from openai import OpenAI

    endpoint = os.environ.get(
        "AZURE_OPENAI_ENDPOINT",
        "https://paragrafen-foundry-dev.cognitiveservices.azure.com/",
    )
    api_key = os.environ.get("AZURE_OPENAI_API_KEY")
    if not api_key:
        from azure.identity import DefaultAzureCredential, get_bearer_token_provider

        api_key = get_bearer_token_provider(
            DefaultAzureCredential(), "https://cognitiveservices.azure.com/.default"
        )
    return OpenAI(base_url=f"{endpoint.rstrip('/')}/openai/v1/", api_key=api_key)


def faithfulness_score(judgement: FaithfulnessJudgement) -> dict:
    """Turn a judgement into metrics. 'faithfulness' is None when the answer makes no claims."""
    claims = judgement.claims
    return {
        "faithfulness": sum(_VERDICT_SCORE[c.verdict] for c in claims) / len(claims)
        if claims
        else None,
        "n_claims": len(claims),
        "n_unsupported": sum(c.verdict == "unsupported" for c in claims),
        "contradicts_sources": float(judgement.contradicts_sources),
        "cites_wrong_section": float(judgement.cites_wrong_section),
    }


def build_prompt(question: str, answer: str, contexts: list[dict]) -> str:
    """'contexts' are {"key": section key, "text": section text}."""
    sources = "\n\n".join(
        f'<source key="{c["key"]}">\n{c["text"]}\n</source>' for c in contexts
    )
    return (
        f"{RUBRIC}\n<law_texts>\n{sources}\n</law_texts>\n\n"
        f"<question>\n{question}\n</question>\n\n<answer>\n{answer}\n</answer>"
    )


def judge_faithfulness(
    question: str,
    answer: str,
    contexts: list[dict],
    client=None,
    model: str = JUDGE_MODEL,
) -> dict:
    """Ask the judge model and return faithfulness metrics plus the raw judgement."""
    if not answer.strip() or not contexts:
        return {"faithfulness": None, "judge_error": "no answer or no source text"}

    client = client or make_judge_client()
    response = client.chat.completions.create(
        model=model,
        max_completion_tokens=16000,
        response_format={
            "type": "json_schema",
            "json_schema": {
                "name": "faithfulness_judgement",
                "schema": _SCHEMA,
                "strict": True,
            },
        },
        messages=[
            {"role": "user", "content": build_prompt(question, answer, contexts)}
        ],
    )
    choice = response.choices[0]
    if choice.message.refusal:
        return {"faithfulness": None, "judge_error": "judge refused"}
    if choice.finish_reason == "length":
        return {"faithfulness": None, "judge_error": "judge hit max_completion_tokens"}
    if choice.finish_reason == "content_filter":
        return {
            "faithfulness": None,
            "judge_error": "judge output was content filtered",
        }

    judgement = FaithfulnessJudgement.model_validate(json.loads(choice.message.content))
    return {**faithfulness_score(judgement), "judgement": judgement.model_dump()}
