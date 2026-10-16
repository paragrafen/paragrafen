CREATE TABLE calls (
    request_id       uuid PRIMARY KEY,
    created_at       timestamptz NOT NULL,
    conversation_id  uuid NOT NULL,
    status           text NOT NULL CHECK (status IN ('answered', 'not_found', 'out_of_scope', 'error')),
    failure_reason   text,
    attempts         smallint NOT NULL CHECK (attempts >= 0),
    model            text,
    input_tokens     integer NOT NULL,
    cached_tokens    integer NOT NULL,
    output_tokens    integer NOT NULL,
    reasoning_tokens integer NOT NULL,
    cost_nok         numeric(10, 6) NOT NULL,
    latency_ms       integer NOT NULL,
    llm_latency_ms   integer NOT NULL
);
