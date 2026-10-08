CREATE TABLE chunks (
    chunk_id     text PRIMARY KEY,
    law          text NOT NULL,
    section      text NOT NULL,
    text         text NOT NULL,
    url          text NOT NULL,
    content_hash text NOT NULL,
    data_version text NOT NULL,
    search_text  tsvector GENERATED ALWAYS AS (to_tsvector('norwegian', text)) STORED
);

CREATE INDEX chunks_search_text_idx ON chunks USING gin (search_text);
CREATE INDEX chunks_law_idx ON chunks (law);
