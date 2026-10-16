import logging
import os
import shutil
import tarfile
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

logger = logging.getLogger(__name__)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)


def ingest_data() -> None:
    docs = []
    destination = Path(output_dir).expanduser().resolve()
    destination.mkdir(parents=True, exist_ok=True)

    logger.info("Starting ingestion")

    download_sources(destination)

    logger.info("Data downloaded/updated successfully")

    logger.info("Starting processing documents...")

    # Check which files changed
    with os.scandir(destination / "extracted" / "gjeldende-lover" / "nl") as entries:
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

                # Remove this loop
                for start in range(0, len(chunks_to_embed), 16):
                    embed_text(
                        chunks_to_embed[start : start + 16],
                        api_key=os.environ["EMBEDDING_API"],
                    )

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
    os.remove(destination)
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


def download_sources(destination: Path) -> None:
    """Download latest lov/forskrifter and extract them to the data folder"""
    url = "https://api.lovdata.no/v1/publicData/get/gjeldende-lover.tar.bz2"
    archive_path = destination / "gjeldende-lover.tar.bz2"
    extracted_dir = destination / "extracted" / "gjeldende-lover"

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
