from paragrafen_ingest import main


def test_main_runs(capsys):
    main()
    assert "paragrafen-ingest" in capsys.readouterr().out
