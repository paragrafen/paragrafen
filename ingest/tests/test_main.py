import hashlib
import socket
from collections import deque
from types import SimpleNamespace
from unittest.mock import Mock, patch

import httpx
import pytest

from paragrafen_ingest import embedder, ingest, main


@pytest.fixture(autouse=True)
def block_external_calls(monkeypatch):
    """Fail rather than accidentally using credentials, network, or tokenizer data."""

    def blocked(*args, **kwargs):
        pytest.fail("Unexpected external call in an offline unit test")

    monkeypatch.setattr(socket.socket, "connect", blocked)
    monkeypatch.setattr(socket.socket, "connect_ex", blocked)
    monkeypatch.setattr(ingest, "urlopen", blocked)
    monkeypatch.setattr(ingest.psycopg, "connect", blocked)
    monkeypatch.setattr(embedder.httpx, "post", blocked)
    monkeypatch.setattr(embedder, "_encoding", blocked)
    monkeypatch.setenv("EMBEDDING_API", "fake-test-key")
    monkeypatch.setenv("EMBEDDING_URL", "https://example.invalid/embeddings")


@pytest.fixture
def fake_clock(monkeypatch):
    clock = SimpleNamespace(now=100.0, waits=[])

    def sleep(seconds):
        clock.waits.append(seconds)
        clock.now += seconds

    fake_time = SimpleNamespace(monotonic=lambda: clock.now, sleep=sleep)
    monkeypatch.setattr(ingest, "time", fake_time)
    monkeypatch.setattr(embedder, "time", fake_time)
    return clock


@pytest.fixture
def fake_encoding(monkeypatch):
    # Character counting is deterministic and needs no tokenizer download.
    encoding = SimpleNamespace(encode=lambda text, **kwargs: list(text))
    monkeypatch.setattr(embedder, "_encoding", lambda: encoding)
    return encoding


def response(status=200, *, headers=None, data=None):
    return httpx.Response(
        status,
        headers=headers,
        json={
            "data": data
            if data is not None
            else [
                {"index": 0, "embedding": [0.1, 0.2]},
            ]
        },
        request=httpx.Request("POST", "https://example.invalid/embeddings"),
    )


def test_main_passes_manually_selected_dataset():
    with patch("paragrafen_ingest.ingest_data", autospec=True) as mock_ingest:
        main()

    mock_ingest.assert_called_once_with("lov")


@pytest.mark.parametrize(
    ("tokens", "max_tokens", "max_inputs", "expected_sizes"),
    [
        ([], 10, 10, []),
        ([4, 6], 10, 10, [2]),
        ([4, 6, 1], 10, 10, [2, 1]),
        ([6, 6, 6], 10, 10, [1, 1, 1]),
        ([1, 1, 1], 10, 2, [2, 1]),
    ],
)
def test_batch_chunks(tokens, max_tokens, max_inputs, expected_sizes):
    chunks = [{"tokenCount": count} for count in tokens]

    batches = list(ingest.batch_chunks(chunks, max_tokens, max_inputs))

    assert [len(batch) for batch in batches] == expected_sizes
    assert [chunk for batch in batches for chunk in batch] == chunks
    assert all(sum(c["tokenCount"] for c in batch) <= max_tokens for batch in batches)


def test_batch_chunks_rejects_oversized_chunk():
    with pytest.raises(ValueError, match="batch limit"):
        list(ingest.batch_chunks([{"tokenCount": 11}], max_tokens=10))


@pytest.mark.parametrize(
    ("history", "tokens", "request_budget", "expected_waits"),
    [
        ([], 5, 900, []),
        ([(90.0, 6)], 4, 900, []),
        ([(90.0, 6)], 5, 900, [50.0]),
        ([(40.0, 10)], 5, 900, []),
        ([(90.0, 1)], 1, 1, [50.0]),
    ],
)
def test_pacing(
    history, tokens, request_budget, expected_waits, monkeypatch, fake_clock
):
    monkeypatch.setattr(ingest, "TOKEN_BUDGET", 10)
    monkeypatch.setattr(ingest, "REQUEST_BUDGET", request_budget)
    send = Mock()
    monkeypatch.setattr(ingest, "embed_text", send)
    recent_requests = deque(history)
    chunks = [{"tokenCount": tokens}]

    ingest.send_embedding_requests(chunks, recent_requests)

    assert fake_clock.waits == expected_waits
    send.assert_called_once_with(chunks, api_key="fake-test-key")
    assert recent_requests[-1] == (fake_clock.now, tokens)


def test_pacing_preserves_quota_between_calls(monkeypatch, fake_clock):
    monkeypatch.setattr(ingest, "TOKEN_BUDGET", 10)
    send = Mock()
    monkeypatch.setattr(ingest, "embed_text", send)
    recent_requests = deque()

    ingest.send_embedding_requests([{"tokenCount": 6}], recent_requests)
    ingest.send_embedding_requests([{"tokenCount": 6}], recent_requests)

    assert fake_clock.waits == [60.0]
    assert send.call_count == 2


def test_empty_input_sends_no_requests(monkeypatch, fake_clock):
    send = Mock()
    monkeypatch.setattr(ingest, "embed_text", send)

    ingest.send_embedding_requests([], deque())

    send.assert_not_called()
    assert fake_clock.waits == []


@pytest.mark.parametrize(
    ("headers", "expected_delay"),
    [
        ({"retry-after-ms": "2500"}, 2.5),
        ({"x-ms-retry-after-ms": "3000"}, 3.0),
        ({"retry-after": "4"}, 4.0),
        ({"retry-after-ms": "2500", "retry-after": "10"}, 2.5),
        ({}, 1.0),
        ({"retry-after": "invalid"}, 1.0),
    ],
)
def test_429_retries_then_succeeds(headers, expected_delay, monkeypatch, fake_clock):
    post = Mock(side_effect=[response(429, headers=headers), response()])
    monkeypatch.setattr(embedder.httpx, "post", post)
    chunks = [{"embeddingText": "Example"}]

    embedder.embed_text(chunks, api_key="fake-test-key")

    assert post.call_count == 2
    assert fake_clock.waits == [expected_delay]
    assert chunks[0]["embedding"] == [0.1, 0.2]


def test_429_stops_after_retry_budget(monkeypatch, fake_clock):
    post = Mock(side_effect=[response(429) for _ in range(6)])
    monkeypatch.setattr(embedder.httpx, "post", post)
    chunks = [{"embeddingText": "Example"}]

    with pytest.raises(httpx.HTTPStatusError):
        embedder.embed_text(chunks, api_key="fake-test-key")

    assert post.call_count == 6
    assert fake_clock.waits == [1, 2, 4, 8, 16]
    assert "embedding" not in chunks[0]


@pytest.mark.parametrize("status", [400, 401, 403, 500])
def test_non_429_errors_are_not_retried(status, monkeypatch, fake_clock):
    post = Mock(return_value=response(status))
    monkeypatch.setattr(embedder.httpx, "post", post)

    with pytest.raises(httpx.HTTPStatusError):
        embedder.embed_text([{"embeddingText": "Example"}], api_key="fake-test-key")

    post.assert_called_once()
    assert fake_clock.waits == []


def test_embeddings_are_mapped_by_response_index(monkeypatch):
    post = Mock(
        return_value=response(
            data=[
                {"index": 1, "embedding": [0.2]},
                {"index": 0, "embedding": [0.1]},
            ]
        )
    )
    monkeypatch.setattr(embedder.httpx, "post", post)
    chunks = [{"embeddingText": "First"}, {"embeddingText": "Second"}]

    embedder.embed_text(chunks, api_key="fake-test-key")

    assert [chunk["embedding"] for chunk in chunks] == [[0.1], [0.2]]
    assert post.call_args.kwargs["json"]["input"] == ["First", "Second"]


@pytest.mark.parametrize(
    ("data", "error"),
    [
        ([{"index": 0, "embedding": [0.1]}], ValueError),
        ([{"index": 0, "embedding": [0.1]}] * 2, KeyError),
    ],
)
def test_invalid_response_does_not_partially_modify_chunks(data, error, monkeypatch):
    monkeypatch.setattr(embedder.httpx, "post", Mock(return_value=response(data=data)))
    chunks = [{"embeddingText": "First"}, {"embeddingText": "Second"}]

    with pytest.raises(error):
        embedder.embed_text(chunks, api_key="fake-test-key")

    assert all("embedding" not in chunk for chunk in chunks)


def test_split_to_fit_preserves_text(monkeypatch, fake_encoding):
    monkeypatch.setattr(embedder, "MAX_INPUT_TOKENS", 4)
    original = "abcdefghij"

    parts = list(embedder.split_to_fit(original))

    assert "".join(text for text, _ in parts) == original
    assert all(0 < tokens <= 4 for _, tokens in parts)
    assert all(tokens == len(text) for text, tokens in parts)


def test_build_embedding_text_skips_empty_sections_and_hashes_input(fake_encoding):
    document = {
        "metadata": {"title": "Example law"},
        "sections": [
            {"sectionRef": "Section 1", "content": " Content "},
            {"sectionRef": "Section 2", "content": "  "},
        ],
    }

    chunks = embedder.build_embedding_text(document)

    assert len(chunks) == 1
    chunk = chunks[0]
    assert chunk["embeddingText"] == "Example law\n\nSection 1\n\nContent"
    assert chunk["tokenCount"] == len(chunk["embeddingText"])
    assert (
        chunk["embeddingInputHash"]
        == hashlib.sha256(chunk["embeddingText"].encode("utf-8")).hexdigest()
    )
    assert document["sections"][0]["chunks"][0] is chunk
    assert document["sections"][1]["chunks"] == []


def test_find_changed_chunks_returns_only_changed_and_new(monkeypatch):
    embedding = [0.1, 0.2]
    get_embeddings = Mock(return_value={"same": embedding})
    monkeypatch.setattr(ingest, "get_embeddings_by_hash", get_embeddings)
    chunks = [
        {"ordinal": 0, "embeddingInputHash": "same"},
        {"ordinal": 1, "embeddingInputHash": "changed"},
        {"ordinal": 2, "embeddingInputHash": "new"},
    ]
    document = {
        "dokid": "NL/lov/example",
        "sourceVariant": "example.xml",
        "sections": [{"ordinal": 1, "sectionRef": "Section 1", "chunks": chunks}],
    }
    conn = object()

    changed = ingest.find_changed_chunks(conn, document)

    assert changed == chunks[1:]
    assert chunks[0]["embedding"] == embedding
    assert all("embedding" not in chunk for chunk in changed)
    get_embeddings.assert_called_once()
    lookup_conn, hashes = get_embeddings.call_args.args
    assert lookup_conn is conn
    assert set(hashes) == {"same", "changed", "new"}


def test_find_changed_chunks_reuses_embeddings_after_section_insertion(
    monkeypatch, fake_encoding
):
    document = {
        "metadata": {"title": "Example law"},
        "sections": [
            {"ordinal": 1, "sectionRef": "Section 1", "content": "First"},
            {"ordinal": 2, "sectionRef": "Section 2", "content": "Second"},
        ],
    }
    original_chunks = embedder.build_embedding_text(document)
    stored_embeddings = {
        chunk["embeddingInputHash"]: [float(index + 1)]
        for index, chunk in enumerate(original_chunks)
    }
    get_embeddings = Mock(return_value=stored_embeddings)
    monkeypatch.setattr(ingest, "get_embeddings_by_hash", get_embeddings)

    document["sections"].insert(
        1, {"ordinal": 2, "sectionRef": "Section 1 a", "content": "Inserted"}
    )
    document["sections"][2]["ordinal"] = 3
    chunks = embedder.build_embedding_text(document)

    changed = ingest.find_changed_chunks(object(), document)

    assert changed == [chunks[1]]
    assert "embedding" not in chunks[1]
    assert chunks[0]["embedding"] == [1.0]
    assert chunks[2]["embedding"] == [2.0]
