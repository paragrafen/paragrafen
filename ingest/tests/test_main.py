from unittest.mock import patch

from paragrafen_ingest import main


def test_main_runs():
    with patch("paragrafen_ingest.ingest_data") as mock_ingest:
        main()

    mock_ingest.assert_called_once_with()
