import logging
import re
from contextlib import redirect_stdout
import os
from pathlib import Path
from paragrafen_ingest.parser import parse_document
from paragrafen_ingest.embedder import embed_text, build_embedding_text
from lovlig import Reporter, Settings, sync_datasets


output_dir = Path(__file__).resolve().parents[2] / "data"

logger = logging.getLogger(__name__)
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)

def ingest_data():
    logger.info("Starting ingestion")
    log_path = download_sources()
    logger.info("Data downloaded/updated successfully")

    logger.info("Starting processing documents...")
    files = get_updated_sources(log_path)
    logger.info(f"Updated/Added {len(files)} files")

    if (len(files) == 0):
        logger.info("Sync finished")
        return

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
        
        doc["embedding"] = embed_text(build_embedding_text(doc), api_key=os.environ["CHAT-GPT-API"])

    logger.info("Finished processing documents successfully")
    logger.info("Successfully inserted/updated database")
    
    # Add to Db is WIP
    # delete data folders
    logger.info("Sync finished")


# Uses lovlig api to download/update lovdata.
def download_sources() -> str:
    config = Settings(
        dataset_filter="gjeldende",
        raw_data_dir=output_dir / "raw",
        extracted_data_dir=output_dir / "extracted",
        state_file=output_dir / "state.json",
    )

    log_path = output_dir / "ingestion_log.txt"

    with log_path.open("w", encoding="utf-8") as log_file:
        with redirect_stdout(log_file):
            reporter = Reporter()
            reporter.CHANGE_PREVIEW_LIMIT = None    # does not truncate output
            sync_datasets(config=config, reporter=reporter)

    return log_path

# Forms a list of all files that are added/changed/deleted.
def get_updated_sources(log_path: Path) -> list[dict[str, str]]:
    files = []
    current_status = None
    statuses = {
        "New": "new",
        "Modified": "modified",
        "Removed": "removed",
    }

    with log_path.open(encoding="utf-8") as log_file:
        for line in log_file:
            # Example: "  ⚡ Modified files: 3"
            section = re.match(
                r"^\s*[✓⚡✗]\s+(New|Modified|Removed) files:\s+\d+\s*$",
                line,
            )

            if section:
                current_status = statuses[section.group(1)]
            elif current_status and line.startswith("      ") and line.strip():
                file_path = line.strip()
                if file_path.startswith(("stv/", "ins/", "del/")):
                    continue

                files.append({
                    "path": file_path,
                    "status": current_status,
                })
                
            else:
                current_status = None
    return files
