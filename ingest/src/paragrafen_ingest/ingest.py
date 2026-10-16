import logging
import os
import shutil
import tarfile
import time
from collections import deque
from pathlib import Path
from urllib.request import urlopen

import psycopg

from paragrafen_ingest.db_client import (
    create_connection,
    delete_document,
    get_chunk_hashes,
    get_document_hashes,
    upsert_document,
)
from paragrafen_ingest.embedder import build_embedding_text, embed_text
from paragrafen_ingest.parser import parse_document

output_dir = Path(__file__).resolve().parents[2] / "data"
TOKEN_BUDGET = 140_000  # maximum tpr
REQUEST_BUDGET = 900  # maximum rpr


logger = logging.getLogger(__name__)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)


def ingest_data(dataset: str) -> None:
    docs = []
    recent_requests = deque()
    destination = Path(
        os.environ.get("INGEST_DATA_DIR", str(output_dir))
    ).expanduser().resolve()
    destination.mkdir(parents=True, exist_ok=True)

    logger.info("Starting ingestion")

    download_sources(destination, dataset)

    logger.info("Data downloaded/updated successfully")

    logger.info("Starting processing documents...")

    if(dataset == "lov"):
        extracted_file_path = destination / "extracted" / "gjeldende-lover" / "nl"
    else: 
        extracted_file_path = destination / "extracted" / "gjeldende-sentrale-forskrifter" / "sf"

    # Check which files changed
    with os.scandir(extracted_file_path) as entries:
        for entry in entries:
            if entry.is_file():
                doc = parse_document(Path(entry.path))
                docs.append(doc)

    logger.info("Finished processing documents successfully")

    with create_connection() as conn:
        stored_file_hashes = get_document_hashes(conn)

        documents_to_ingest = 0

        for document in docs:
            document_key = (document["dokid"], document["sourceVariant"])

            if stored_file_hashes.get(document_key) != document["sourceHash"]:
                documents_to_ingest += 1
                build_embedding_text(document)
                chunks_to_embed = find_changed_chunks(conn, document)
                send_embedding_requests(chunks_to_embed, recent_requests)

                # Update documents values in db
                upsert_document(conn, document)

        logger.info("%s files were changed/added", documents_to_ingest)

        # Find deleted documents
        current_keys = {(doc["dokid"], doc["sourceVariant"]) for doc in docs}
        deleted_keys = set(stored_file_hashes) - current_keys
        for dokid, source_variant in deleted_keys:
            delete_document(conn, dokid, source_variant)

        logger.info("Deleted %s documents", len(deleted_keys))

    logger.info("Successfully inserted/updated database")

    logger.info("Deleting data folder...")
    shutil.rmtree(destination)
    logger.info("Successfully deleted data folder")
    logger.info("Sync finished")


# Finds all chunks text that changed in a document
def find_changed_chunks(conn: psycopg.Connection, document: dict) -> list[dict]:
    # Key: (section ordinal, chunk ordinal)
    stored_hashes = {
        (section_ordinal, chunk_ordinal): chunk_hash
        for section_ordinal, chunk_ordinal, chunk_hash in get_chunk_hashes(
            conn, document["dokid"], document["sourceVariant"]
        )
    }

    changed_chunks = []

    for section in document["sections"]:
        for chunk in section.get("chunks", []):
            key = (section["ordinal"], chunk["ordinal"])
            previous_hash = stored_hashes.get(key)

            if previous_hash != chunk["embeddingInputHash"]:
                changed_chunks.append(chunk)

                logger.info(
                    "%s: section %s, chunk %s is %s",
                    document["dokid"],
                    section["sectionRef"],
                    chunk["ordinal"],
                    "new" if previous_hash is None else "changed",
                )
    return changed_chunks


def download_sources(destination: Path, dataset: str) -> None:
    """Download latest lov/forskrifter and extract them to the data folder"""

    if (dataset == "lov"):
        url = "https://api.lovdata.no/v1/publicData/get/gjeldende-lover.tar.bz2"
        archive_path = destination / "gjeldende-lover.tar.bz2"
        extracted_dir = destination / "extracted" / "gjeldende-lover"
    else: 
        url = "https://api.lovdata.no/v1/publicData/get/gjeldende-sentrale-forskrifter.tar.bz2"
        archive_path = destination / "gjeldende-sentrale-forskrifter.tar.bz2"
        extracted_dir = destination / "extracted" / "gjeldende-sentrale-forskrifter"

    # Stream the download to disk instead of loading it all into memory.
    with (
        urlopen(url, timeout=120) as response,
        archive_path.open("wb") as archive_file,
    ):
        shutil.copyfileobj(response, archive_file)

    # Extract the archive into the selected folder.
    extracted_dir.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive_path, mode="r:bz2") as archive:
        archive.extractall(path=extracted_dir, filter="data")


def batch_chunks(chunks, max_tokens: int = 140_000, max_inputs: int = 2048):
    """Batches chunks requests in order to avoid sending a request
    with an higher cost than the token budget
    """
    batch = []
    batch_tokens = 0

    for chunk in chunks:
        tokens = chunk["tokenCount"]

        if tokens > max_tokens:
            raise ValueError(f"Chunk has {tokens} tokens; batch limit is {max_tokens}")

        if batch and (batch_tokens + tokens > max_tokens or len(batch) >= max_inputs):
            yield batch
            batch = []
            batch_tokens = 0

        batch.append(chunk)
        batch_tokens += tokens

    if batch:
        yield batch


def send_embedding_requests(chunks_to_embed, recent_requests):
    """Ensures that each request is within the budgets, when a request
    exceeds the limit it waits for one minute in order to reset the
    budget."""
    for batch in batch_chunks(chunks_to_embed, max_tokens=TOKEN_BUDGET):
        batch_tokens = sum(chunk["tokenCount"] for chunk in batch)

        while True:
            now = time.monotonic()

            # Discard requests outside the rolling minute.
            while recent_requests and now - recent_requests[0][0] >= 60:
                recent_requests.popleft()

            used_tokens = sum(tokens for _, tokens in recent_requests)

            if (
                used_tokens + batch_tokens <= TOKEN_BUDGET
                and len(recent_requests) < REQUEST_BUDGET
            ):
                break

            wait_seconds = max(0, 60 - (now - recent_requests[0][0]))
            logger.info("Waiting %.1fs for embedding quota", wait_seconds)
            time.sleep(wait_seconds)

        embed_text(
            batch,
            api_key=os.environ["EMBEDDING_API"],
        )

        # Using completion time makes the accounting conservative.
        recent_requests.append((time.monotonic(), batch_tokens))
