import math

import httpx
import tiktoken


MODEL = "text-embedding-3-small"
DIMENSIONS = 1536
MAX_INPUT_TOKENS = 8192

ENCODING = tiktoken.encoding_for_model(MODEL)


def embed_text(text: str, *, api_key: str) -> list[float]:
    """Generate one embedding for a document's text."""
    if not text.strip():
        raise ValueError("Cannot embed empty text")

    token_count = len(
        ENCODING.encode(text, disallowed_special=())
    )

    print(f"Embedding input: {token_count} tokens")

    if token_count > MAX_INPUT_TOKENS:
        raise ValueError(
            f"Document contains {token_count} tokens; "
            f"the model accepts at most {MAX_INPUT_TOKENS}"
        )

    response = httpx.post(
        "https://api.openai.com/v1/embeddings",
        headers={
            "Authorization": f"Bearer {api_key}",
        },
        json={
            "model": MODEL,
            "input": text,
            "dimensions": DIMENSIONS,
            "encoding_format": "float",
        },
        timeout=60.0,
    )
    response.raise_for_status()

    items = response.json()["data"]

    if len(items) != 1 or items[0]["index"] != 0:
        raise ValueError("Unexpected embedding response")

    vector = items[0]["embedding"]

    if (
        len(vector) != DIMENSIONS
        or not all(math.isfinite(value) for value in vector)
        or not any(value != 0 for value in vector)
    ):
        raise ValueError("Invalid embedding vector")

    return vector

# Builds one string with the whole document ensuring no white space
def build_embedding_text(document):
    embedding_text = "\n\n".join([
        document["title"],
        *[
            f"{p.get('sectionRef') or ''}\n{p['content']}".strip()
            for p in document["paragraphs"]
            if p["content"].strip()
        ],
    ])

    print(embedding_text)
    return embedding_text