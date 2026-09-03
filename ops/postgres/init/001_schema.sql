CREATE TABLE IF NOT EXISTS ledger_events (
    event_id UUID NOT NULL,
    stream_id UUID NOT NULL,
    version BIGINT NOT NULL,
    event_type TEXT NOT NULL,
    payload JSONB NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    correlation_id TEXT NOT NULL,
    causation_id TEXT NULL,
    CONSTRAINT pk_ledger_events PRIMARY KEY (stream_id, version),
    CONSTRAINT uq_ledger_events_event_id UNIQUE (event_id)
);

CREATE INDEX IF NOT EXISTS ix_ledger_events_stream
    ON ledger_events (stream_id, version);

CREATE TABLE IF NOT EXISTS ledger_outbox (
    id BIGSERIAL PRIMARY KEY,
    event_id UUID NOT NULL,
    stream_id UUID NOT NULL,
    event_type TEXT NOT NULL,
    payload JSONB NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    correlation_id TEXT NOT NULL,
    published_at TIMESTAMPTZ NULL,
    attempts INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_ledger_outbox_pending
    ON ledger_outbox (published_at, id)
    WHERE published_at IS NULL;

CREATE TABLE IF NOT EXISTS account_projection (
    account_id UUID PRIMARY KEY,
    owner_id TEXT NOT NULL,
    currency CHAR(3) NOT NULL,
    balance NUMERIC(19, 4) NOT NULL DEFAULT 0,
    is_open BOOLEAN NOT NULL,
    version BIGINT NOT NULL,
    last_updated_at TIMESTAMPTZ NOT NULL
);