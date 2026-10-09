from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path

from lovlig import Settings, sync_datasets


output_dir = Path(__file__).resolve().parents[2] / "data"


def download_sources() -> str:
    config = Settings(
        dataset_filter="gjeldende",
        raw_data_dir=output_dir / "raw",
        extracted_data_dir=output_dir / "extracted",
        state_file=output_dir / "state.json",
    )
    output = StringIO()

    with redirect_stdout(output):
        sync_datasets(config=config)

    return output.getvalue()

