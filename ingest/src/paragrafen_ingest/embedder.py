import hashlib
import os
import time
from collections.abc import Iterator
from functools import cache

import httpx
import tiktoken

MODEL = "text-embedding-3-small"
DIMENSIONS = 1536
MAX_INPUT_TOKENS = 8192
MAX_RETRIES = 5


@cache
def _encoding() -> tiktoken.Encoding:
    return tiktoken.encoding_for_model(MODEL)


def embed_text(chunks, *, api_key: str) -> None:
    """Generate one embedding for each chunk and adds it to the document."""

    for attempt in range(MAX_RETRIES + 1):
        response = httpx.post(
            os.environ["EMBEDDING_URL"],
            headers={
                "api-key": api_key,
            },
            json={
                "input": [chunk["embeddingText"] for chunk in chunks],
                "dimensions": DIMENSIONS,
                "encoding_format": "float",
            },
            timeout=60.0,
        )

        # Other errors, or an exhausted retry budget, should propagate.
        if response.status_code != 429 or attempt == MAX_RETRIES:
            response.raise_for_status()
            break

        # Azure suggests an amout of time to wait before sending the request again
        retry_ms = response.headers.get("retry-after-ms") or response.headers.get(
            "x-ms-retry-after-ms"
        )
        retry_seconds = response.headers.get("retry-after")

        # Makes sure that a retry value was sent, else it waits the nr of attempts x 2 in seconds
        try:
            if retry_ms is not None:
                delay = float(retry_ms) / 1000
            elif retry_seconds is not None:
                delay = float(retry_seconds)
            else:
                delay = 2**attempt
        except ValueError:
            delay = 2**attempt

        delay = max(1.0, delay)
        time.sleep(delay)

    items = response.json()["data"]

    if len(items) != len(chunks):
        raise ValueError(f"Expected {len(chunks)} embeddings, received {len(items)}")

    vectors_by_index = {item["index"]: item["embedding"] for item in items}

    # Resolve all vectors before modifying any chunks.
    vectors = [vectors_by_index[index] for index in range(len(chunks))]
    for chunk, vector in zip(chunks, vectors):
        chunk["embedding"] = vector


def build_embedding_text(document: dict) -> list[dict]:
    """Build one embedding-ready chunk per non-empty provision
       of a document.

    Adds a 'chunks' list to each section and returns all chunks.
    Oversized provisions raise an error and need to be split.
    """
    all_chunks = []

    for section in document["sections"]:
        content = section["content"].strip()
        section["chunks"] = []

        if not content:
            continue

        context = [
            document["metadata"].get("title"),
            section.get("sectionRef"),
        ]

        embedding_text = "\n\n".join(
            [
                *[value.strip() for value in context if value and value.strip()],
                content,
            ]
        )

        # split_to_fit returns a list of text and tokens, form a chunk for each
        for ordinal, (chunk_text, chunk_tokens) in enumerate(
            split_to_fit(embedding_text)
        ):
            chunk = {
                "ordinal": ordinal,
                "embeddingText": chunk_text,
                "tokenCount": chunk_tokens,
                "embeddingInputHash": hashlib.sha256(
                    chunk_text.encode("utf-8")
                ).hexdigest(),
            }
            section["chunks"].append(chunk)
            all_chunks.append(chunk)
    return all_chunks


def split_to_fit(text) -> Iterator[tuple[str, int]]:
    """Recursively splits the original text until each component is small enough
    to fit the max nr of tokens."""

    token_count = len(_encoding().encode(text, disallowed_special=()))

    if token_count <= MAX_INPUT_TOKENS:
        yield text, token_count
        return

    midpoint = len(text) // 2
    yield from split_to_fit(text[:midpoint])
    yield from split_to_fit(text[midpoint:])
