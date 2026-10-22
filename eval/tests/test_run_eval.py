import json
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer

import pytest

import compare
import run_eval

AML = "LOV-2005-06-17-62"


def fake_response(question: str) -> dict:
    """Retrieves aml. § 10-8 twice (two chunks) and § 10-9; the answer cites § 10-8."""
    if "maraton" in question:
        return {
            "request_id": "r-0",
            "status": "out_of_scope",
            "answer": "",
            "sources": [],
        }
    return {
        "request_id": "r-1",
        "conversation_id": "c-1",
        "status": "answered",
        "answer": "Etter § 10-8 skal du ha minst 11 timer fri.",
        "sources": [
            {
                "chunk_id": "c-9",
                "law": AML,
                "description": "Nattarbeid",
                "section": "§ 10-9",
                "text": "Nattarbeid …",
            },
            {
                "chunk_id": "c-8a",
                "law": AML,
                "description": "Arbeidsfri",
                "section": "§ 10-8",
                "text": "11 timer …",
            },
            {
                "chunk_id": "c-8b",
                "law": AML,
                "description": "Arbeidsfri",
                "section": "§ 10-8",
                "text": "35 timer …",
            },
        ],
        "asked_at": "2026-10-15T12:00:00Z",
    }


class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers["content-length"])))
        assert set(body) == {"question"}
        data = json.dumps(fake_response(body["question"])).encode()
        self.send_response(200)
        self.send_header("content-type", "application/json")
        self.end_headers()
        self.wfile.write(data)

    def log_message(self, *args):
        pass


@pytest.fixture
def api_url():
    server = HTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    yield f"http://127.0.0.1:{server.server_port}"
    server.shutdown()


def test_load_questions_have_difficulty():
    questions = run_eval.load_questions()
    assert {q["expected_status"] for q in questions} == {"ANSWERED", "ABSTAINED"}
    assert all(q["difficulty"] in {"easy", "medium", "hard"} for q in questions)
    assert not any("difficulty" in (q["notes"] or "") for q in questions)
    assert not any("category" in q for q in questions)
    assert len({q["id"] for q in questions}) == len(questions)


def test_end_to_end_run_and_compare(api_url, tmp_path, capsys):
    out = tmp_path / "results"
    args = [
        "--api-url",
        api_url,
        "--ids",
        "ans-0001",
        "ans-0002",
        "unans-0012",
        "--out",
        str(out),
    ]
    assert run_eval.main([*args, "--config", "a"]) == 0
    assert run_eval.main([*args, "--config", "b"]) == 0

    run_a, run_b = sorted(out.iterdir())
    rows = {r["id"]: r for r in run_eval.load_jsonl(run_a / "rows.jsonl")}
    assert rows["ans-0001"]["retrieved_keys"] == [f"{AML}/§10-9", f"{AML}/§10-8"]
    assert rows["ans-0001"]["cited_keys"] == [f"{AML}/§10-8"]
    assert rows["ans-0001"]["request_id"] == "r-1"
    assert rows["ans-0001"]["recall"] == 1.0
    assert rows["ans-0001"]["citation_validity"] == 1.0
    assert rows["ans-0002"]["citation_precision"] == 0.0
    assert rows["unans-0012"]["status"] == "ABSTAINED"
    assert "recall" not in rows["unans-0012"]

    summary = json.loads((run_a / "summary.json").read_text(encoding="utf-8"))
    assert summary["overall"]["n"] == 3
    assert summary["overall"]["wrongful_answer_rate"] == 0.0
    assert summary["overall"]["recall"] == 1.0
    assert summary["meta"]["config"] == "a"
    assert set(summary["difficulty"]) == {"easy"}
    assert "category" not in summary and "law_area" not in summary

    assert compare.main([str(run_a), str(run_b), "--slices"]) == 0
    assert "Changed questions" in capsys.readouterr().out


def test_failed_request_is_recorded_not_raised(tmp_path):
    assert (
        run_eval.main(
            [
                "--api-url",
                "http://127.0.0.1:9",
                "--ids",
                "ans-0001",
                "--out",
                str(tmp_path),
                "--timeout",
                "2",
            ]
        )
        == 0
    )
    (run_dir,) = tmp_path.iterdir()
    (row,) = run_eval.load_jsonl(run_dir / "rows.jsonl")
    assert row["status"] is None and row["error"]
    summary = json.loads((run_dir / "summary.json").read_text(encoding="utf-8"))
    assert summary["overall"]["n_errors"] == 1


def test_source_without_section_is_recorded_not_raised():
    (q,) = [q for q in run_eval.load_questions() if q["id"] == "ans-0001"]
    response = fake_response(q["question"])
    response["sources"][0]["section"] = ""

    row = run_eval.evaluate(q, response, None, None, None)

    assert row["status"] == "ANSWERED"
    assert row["error"].startswith("could not score")
    assert "recall" not in row
