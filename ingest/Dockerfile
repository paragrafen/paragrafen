FROM python:3.14-slim
COPY --from=ghcr.io/astral-sh/uv:0.12.16 /uv /bin/uv
WORKDIR /app
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy UV_NO_DEV=1 PATH="/app/.venv/bin:$PATH"
COPY pyproject.toml uv.lock ./
RUN uv sync --locked --no-install-project
COPY src/ src/
RUN uv sync --locked
RUN useradd --system app
USER app
CMD ["python", "-m", "paragrafen_ingest"]
