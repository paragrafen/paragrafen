# from paragrafen_ingest.ingest import download_sources

from paragrafen_ingest.parser import parse_document
from paragrafen_ingest.embedder import build_embedding_text, embed_text
from pathlib import Path
import os
import json

def main() -> None:

    output_dir = Path(__file__).resolve().parents[2] / "data"

    file_path = output_dir / "extracted" / "gjeldende-lover" / "nl" / "nl-20200417-029.xml"

    doc = parse_document(file_path)

    embed_text(doc, api_key=os.environ["EMBEDDING-API"])    

    
    output_path = output_dir / "doc.json"
    with output_path.open("w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)

    print(f"Saved document to {output_path.resolve()}")
