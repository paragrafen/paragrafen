from pathlib import Path
from bs4 import BeautifulSoup, Tag


def parse_document(
    source_path: Path
) -> dict:
    """Parse a Lovdata file."""
    raw_html = source_path.read_text(encoding="utf-8")
    soup = BeautifulSoup(raw_html, "html.parser")

    # extracts and strips all text in a given field
    def text(node: Tag | None) -> str:
        return " ".join(node.get_text(" ", strip=True).split()) if node else ""

    body = soup.select_one("main.documentBody")
    info = soup.select_one("dl.data-document-key-info")

    metadata = {}

    for field in info.find_all("dd", recursive=False):
        classes = field.get("class", [])
        if not classes or "table-of-contents" in classes:
            continue

        key = classes[0]
        items = field.select(":scope > ul > li")
        metadata[key] = [text(item) for item in items] if items else text(field)

    def links(node: Tag | None) -> list[dict]:
        if node is None:
            return []

        return [
            {
                "rawHref": link["href"],
                "text": text(link),
            }
            for link in node.select("a[href]")
        ]

    dokid = metadata.get("dokid")
    if not dokid:
        raise ValueError(f"Missing dokid: {source_path}")

    paragraphs = []
    provisions = body.select("article.legalArticle")

    # Support documents with no numbered provisions.
    units = provisions or [body]

    for ordinal, provision in enumerate(units, start=1):
        source_anchor = provision.get("id")

        amendment_nodes = provision.select(".changesToParent")
        amendment_notes = [
            {
                "text": text(note),
                "references": links(note),
            }
            for note in amendment_nodes
        ]

        # Work on a copy so the source and original DOM remain intact.
        # clean.find() returns the first html element of provision in this case.
        clean = BeautifulSoup(str(provision), "html.parser")
        content_root = clean.find()

        for excluded in content_root.select(
            ".legalArticleHeader, .changesToParent"
        ):
            excluded.decompose()

        paragraphs.append({
            "kind": "paragraph" if provisions else "body",
            "ordinal": ordinal,
            "sourceAnchor": source_anchor,
            "sectionRef": text(provision.select_one(".legalArticleValue"))
                or provision.get("data-name"),
            "content": text(content_root),
            "amendmentNotes": amendment_notes,
            "references": links(content_root),
        })

    document = {
        "lawId": metadata.get("legacyID"),
        "dokid": dokid,
        # "refId": metadata.get("refid"),
        "docType": dokid.split("/", 1)[0],
        "sourceVariant": source_path.name,
        "shortName": metadata.get("titleShort"),
        "title": metadata.get("title") or text(soup.select_one("head title")),
        "dateInForce": metadata.get("dateInForce"),
        "legalArea": metadata.get("legalArea", []),
        "basedOn": links(info.select_one("dd.basedOn")),
        "paragraphs": paragraphs,
    }

    return document