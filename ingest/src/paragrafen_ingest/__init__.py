import logging

from paragrafen_ingest.ingest import download_sources

logger = logging.getLogger(__name__)


def main() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )

    logger.info("Starting ingestion")
    download_sources()
    logger.info("Ingestion completed")