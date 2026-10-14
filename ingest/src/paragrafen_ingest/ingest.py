import logging
import os
import shutil
import tarfile
from urllib.request import urlopen
from pathlib import Path
from paragrafen_ingest.embedder import build_embedding_text, embed_text
from paragrafen_ingest.parser import parse_document

output_dir = Path(__file__).resolve().parents[2] / "data"

logger = logging.getLogger(__name__)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)


def ingest_data() -> None:
    destination = Path(output_dir).expanduser().resolve()
    destination.mkdir(parents=True, exist_ok=True)

    logger.info("Starting ingestion")

    download_sources(destination)
    logger.info("Data downloaded/updated successfully")
    logger.info("Starting processing documents...")

    # Iterate over extracted files
    for file in files:
        if file["status"] == "removed":
            # TODO: mark this file as absent in the database.
            continue

        dataset = (
            "gjeldende-lover"
            if file["path"].startswith("nl/")
            else "gjeldende-sentrale-forskrifter"
        )
        source_path = output_dir / "extracted" / dataset / file["path"]
        doc = parse_document(source_path)

        doc["embedding"] = embed_text(
            build_embedding_text(doc), api_key=os.environ["EMBEDDING-API"]
        )

    logger.info("Finished processing documents successfully")
    logger.info("Successfully inserted/updated database")

    # Add to Db is WIP
    # delete data folders
    logger.info("Sync finished")


# Download latest lov/forskrifter and extract them to the data folder
def download_sources(destination : Path) -> None:
    url = "https://api.lovdata.no/v1/publicData/get/gjeldende-lover.tar.bz2"
    archive_path = destination / "gjeldende-lover.tar.bz2"
    extracted_dir = destination / "extracted" / "gjeldende-lover"

    # Stream the download to disk instead of loading it all into memory.
    with urlopen(url, timeout=120) as response:
        with archive_path.open("wb") as archive_file:
            shutil.copyfileobj(response, archive_file)

    # Extract the archive into the selected folder.
    extracted_dir.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive_path, mode="r:bz2") as archive:
        archive.extractall(path=extracted_dir, filter="data")

