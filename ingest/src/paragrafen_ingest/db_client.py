import os
from uuid import UUID

import psycopg
from pgvector import Vector
from pgvector.psycopg import register_vector
from psycopg.types.json import Jsonb


def create_connection() -> psycopg.Connection:
    return psycopg.connect(
        os.environ["DATABASE_URL"],
        autocommit=True,
    )


def get_document_hashes(conn: psycopg.Connection) -> dict[tuple[str, str], str]:
    """Gets all the hashes of all the documents stored in the db"""
    with conn.cursor() as cur:
        cur.execute(
            """
                SELECT metadata->>'dokid', source_variant, source_hash
                FROM public.documents;
            """
        )
        return {
            (dokid, source_variant): source_hash
            for dokid, source_variant, source_hash in cur.fetchall()
        }


def get_embeddings_by_hash(
    conn: psycopg.Connection,
    hashes: list[str],
) -> dict[str, list[float]]:
    """Gets all the embeddings of the passed hashes list in the db"""
    if not hashes:
        return {}

    register_vector(conn)

    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT DISTINCT ON (embedding_input_hash)
                embedding_input_hash, embedding
            FROM public.chunks
            WHERE embedding_input_hash = ANY(%s)
            ORDER BY embedding_input_hash, id;
            """,
            (hashes,),
        )

        return {
            input_hash: embedding.tolist() for input_hash, embedding in cur.fetchall()
        }


def upsert_document(conn: psycopg.Connection, document: dict) -> UUID:
    """Insert a parsed document, its sections, and embedded chunks."""
    register_vector(conn)

    # All inserts succeed together or are rolled back together,
    # including when the connection uses autocommit=True.
    with conn.transaction(), conn.cursor() as cur:
        cur.execute(
            """
                INSERT INTO public.documents (
                    metadata,
                    source_hash,
                    source_hash_algorithm,
                    doc_type,
                    source_variant,
                    based_on
                )
                VALUES (%s, %s, %s, %s, %s, %s)
                ON CONFLICT ((metadata ->> 'dokid'), source_variant)
                DO UPDATE SET
                    metadata = EXCLUDED.metadata,
                    source_hash = EXCLUDED.source_hash,
                    source_hash_algorithm = EXCLUDED.source_hash_algorithm,
                    doc_type = EXCLUDED.doc_type,
                    based_on = EXCLUDED.based_on
                RETURNING id;
                """,
            (
                Jsonb(document["metadata"]),
                document["sourceHash"],
                document["sourceHashAlgorithm"],
                document["docType"],
                document["sourceVariant"],
                Jsonb(document.get("basedOn", [])),
            ),
        )
        document_id = cur.fetchone()[0]

        for section in document["sections"]:
            cur.execute(
                """
                    INSERT INTO public.sections (
                        document_id, kind, ordinal, source_anchor,
                        section_ref, content, amendment_notes, "references"
                    )
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                    ON CONFLICT (document_id, ordinal)
                    DO UPDATE SET
                        kind = EXCLUDED.kind,
                        source_anchor = EXCLUDED.source_anchor,
                        section_ref = EXCLUDED.section_ref,
                        content = EXCLUDED.content,
                        amendment_notes = EXCLUDED.amendment_notes,
                        "references" = EXCLUDED."references"
                    RETURNING id;
                    """,
                (
                    document_id,
                    section["kind"],
                    section["ordinal"],
                    section.get("sourceAnchor"),
                    section.get("sectionRef"),
                    section["content"],
                    Jsonb(section.get("amendmentNotes", [])),
                    Jsonb(section.get("references", [])),
                ),
            )
            section_id = cur.fetchone()[0]

            for chunk in section.get("chunks", []):
                if "embedding" not in chunk:
                    continue
                cur.execute(
                    """
                        INSERT INTO public.chunks (
                            section_id, ordinal, embedding_text, token_count,
                            embedding_input_hash, embedding
                        )
                        VALUES (%s, %s, %s, %s, %s, %s)
                        ON CONFLICT (section_id, ordinal)
                        DO UPDATE SET
                            embedding_text = EXCLUDED.embedding_text,
                            token_count = EXCLUDED.token_count,
                            embedding_input_hash = EXCLUDED.embedding_input_hash,
                            embedding = EXCLUDED.embedding
                        """,
                    (
                        section_id,
                        chunk["ordinal"],
                        chunk["embeddingText"],
                        chunk["tokenCount"],
                        chunk["embeddingInputHash"],
                        Vector(chunk["embedding"]),
                    ),
                )

    return document_id


def delete_document(
    conn: psycopg.Connection,
    dokid: str,
    source_variant: str,
) -> None:
    """Deletes a document in the db"""
    with conn.cursor() as cur:
        cur.execute(
            """
            DELETE FROM public.documents
            WHERE metadata->>'dokid' = %s
              AND source_variant = %s;
            """,
            (dokid, source_variant),
        )
