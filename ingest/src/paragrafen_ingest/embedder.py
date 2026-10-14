from collections.abc import Iterator
import math
import hashlib
import httpx
import tiktoken

MODEL = "text-embedding-3-small"
DIMENSIONS = 1536
MAX_INPUT_TOKENS = 8192

ENCODING = tiktoken.encoding_for_model(MODEL)


def embed_text(document: dict, *, api_key: str) -> list[float]:
    """Generate one embedding for a chunk."""
    chunks = [
        chunk
        for section in document["sections"]
        for chunk in section.get("chunks", [])
    ]

    print(chunks)

    # response = httpx.post(
    #     "https://paragrafen-foundry-dev.cognitiveservices.azure.com/openai/deployments/text-embedding-3-small/embeddings?api-version=2023-05-15",
    #     headers={
    #         "Authorization": f"Bearer {api_key}",
    #     },
    #     json={
    #         "model": MODEL,
    #         "input": text,
    #         "dimensions": DIMENSIONS,
    #         "encoding_format": "float",
    #     },
    #     timeout=60.0,
    # )
    # response.raise_for_status()

    # items = response.json()["data"]

    # if len(items) != 1 or items[0]["index"] != 0:
    #     raise ValueError("Unexpected embedding response")

    # vector = items[0]["embedding"]

    # if (
    #     len(vector) != DIMENSIONS
    #     or not all(math.isfinite(value) for value in vector)
    #     or not any(value != 0 for value in vector)
    # ):
    #     raise ValueError("Invalid embedding vector")

    # return vector

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
            document.get("title"),
            section.get("sectionRef"),
            section.get("heading"),
        ]

        embedding_text = "\n\n".join(
            [
                *[
                    value.strip()
                    for value in context
                    if value and value.strip()
                ],
                content,
            ]
        )
        
        # split_to_fit returns a list of text and tokens, form a chunk for each
        for ordinal, (chunk_text, chunk_tokens) in enumerate(
            split_to_fit(embedding_text)):
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

# Recursively splits the original text until each component is small enough
def split_to_fit(text) -> Iterator[tuple[str, int]]:
    token_count = len(ENCODING.encode(text, disallowed_special=()))

    if token_count <= MAX_INPUT_TOKENS:
        yield text, token_count
        return

    midpoint = len(text) // 2
    yield from split_to_fit(text[:midpoint])
    yield from split_to_fit(text[midpoint:])

def embedding_batches(
    chunks: list[dict],
    *,
    max_chunks: int = 32,
    max_tokens: int = 100_000,
) -> Iterator[list[dict]]:
    batch = []
    tokens = 0

    for chunk in chunks:
        chunk_tokens = chunk["tokenCount"]

        if chunk_tokens > max_tokens:
            raise ValueError("Chunk exceeds the batch token limit")

        if batch and (
            len(batch) >= max_chunks
            or tokens + chunk_tokens > max_tokens
        ):
            yield batch
            batch = []
            tokens = 0

        batch.append(chunk)
        tokens += chunk_tokens

    if batch:
        yield batch
