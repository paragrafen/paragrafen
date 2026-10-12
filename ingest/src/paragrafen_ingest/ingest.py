import re
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path

from lovlig import Reporter, Settings, sync_datasets


output_dir = Path(__file__).resolve().parents[2] / "data"

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
    print(f"Updated/Added {len(files)} files")
    return files
