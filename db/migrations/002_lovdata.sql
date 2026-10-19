CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA public;
DROP TABLE public.chunks;

CREATE TABLE public.documents (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    metadata jsonb NOT NULL CHECK (jsonb_typeof(metadata) = 'object'),
    source_hash text NOT NULL CHECK (source_hash ~ '^[0-9a-f]{64}$'),
    source_hash_algorithm text NOT NULL CHECK (source_hash_algorithm = 'sha256'),
    doc_type text NOT NULL,
    source_variant text NOT NULL,
    based_on jsonb NOT NULL DEFAULT '[]'::jsonb
        CHECK (jsonb_typeof(based_on) = 'array'),
    CONSTRAINT documents_dokid_required CHECK (
        jsonb_typeof(metadata -> 'dokid') = 'string'
        AND coalesce(length(metadata ->> 'dokid'), 0) > 0
    ),
    search_text tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('pg_catalog.norwegian'::regconfig,
            coalesce(metadata ->> 'title', '')), 'A')
        || setweight(to_tsvector('pg_catalog.norwegian'::regconfig,
            coalesce(metadata ->> 'titleShort', '')), 'A')
    ) STORED
);

CREATE UNIQUE INDEX documents_identity_idx
    ON public.documents ((metadata ->> 'dokid'), source_variant);
CREATE INDEX documents_ref_id_idx
    ON public.documents ((metadata ->> 'refid'));
CREATE INDEX documents_metadata_idx ON public.documents USING gin (metadata);
CREATE INDEX documents_search_text_idx
    ON public.documents USING gin (search_text);

CREATE TABLE public.sections (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id uuid NOT NULL REFERENCES public.documents(id) ON DELETE CASCADE,
    kind text NOT NULL CHECK (kind IN ('provision', 'annex', 'body')),
    ordinal integer NOT NULL CHECK (ordinal >= 1),
    source_anchor text,
    section_ref text,
    content text NOT NULL,
    amendment_notes jsonb NOT NULL DEFAULT '[]'::jsonb
        CHECK (jsonb_typeof(amendment_notes) = 'array'),
    "references" jsonb NOT NULL DEFAULT '[]'::jsonb
        CHECK (jsonb_typeof("references") = 'array'),
    UNIQUE (document_id, ordinal)
);

CREATE INDEX sections_document_ref_idx
    ON public.sections (document_id, section_ref);

CREATE TABLE public.chunks (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    section_id uuid NOT NULL REFERENCES public.sections(id) ON DELETE CASCADE,
    ordinal integer NOT NULL CHECK (ordinal >= 0),
    embedding_text text NOT NULL CHECK (length(btrim(embedding_text)) > 0),
    token_count integer NOT NULL CHECK (token_count > 0 AND token_count <= 8192),
    embedding_input_hash text NOT NULL
        CHECK (embedding_input_hash ~ '^[0-9a-f]{64}$'),
    embedding public.vector(1536) NOT NULL,
    embedding_model text NOT NULL DEFAULT 'text-embedding-3-small'
        CHECK (embedding_model = 'text-embedding-3-small'),
    search_text tsvector GENERATED ALWAYS AS (
        to_tsvector('pg_catalog.norwegian'::regconfig, embedding_text)
    ) STORED,
    UNIQUE (section_id, ordinal),
    -- Cosine distance needs nonzero vectors; pgvector rejects nonfinite values.
    CHECK (public.vector_norm(embedding) > 0)
);

CREATE INDEX chunks_embedding_reuse_idx
    ON public.chunks (embedding_input_hash, embedding_model);
CREATE INDEX chunks_search_text_idx ON public.chunks USING gin (search_text);

COMMENT ON TABLE public.documents IS
    'Parsed Lovdata documents with source metadata and SHA-256 hashes.';
COMMENT ON COLUMN public.documents.metadata IS
    'Source metadata including dokid, refid, titles, dates, ministry, and legalArea.';
COMMENT ON COLUMN public.documents.source_hash IS
    'Lowercase SHA-256 of the original file bytes. Save after successful indexing.';
COMMENT ON COLUMN public.documents.based_on IS
    'Array of enabling-legislation links: {rawHref, text}.';
COMMENT ON COLUMN public.sections.ordinal IS
    'One-based position in the document, not the legal section number.';
COMMENT ON COLUMN public.sections.amendment_notes IS
    'Array of {text, references: [{rawHref, text}]} amendment notes.';
COMMENT ON COLUMN public.sections."references" IS
    'Array of provision cross-references: {rawHref, text}.';
COMMENT ON COLUMN public.chunks.ordinal IS
    'Zero-based position within the section; currently one chunk per nonempty provision.';
COMMENT ON COLUMN public.chunks.embedding_text IS
    'Exact input sent to the embedding model. Authoritative provision content lives in sections.';
COMMENT ON COLUMN public.chunks.embedding_input_hash IS
    'Lowercase SHA-256 of the UTF-8 embedding_text; reuse only with the same model configuration.';
