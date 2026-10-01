# StudyHive agent service (FastAPI + uvicorn). Build context: agent/
#   docker build -t studyhive-agent agent
# Secrets (INTERNAL_API_KEY, GROK_API_KEY, ...) are runtime environment variables only; .env is
# excluded from the build context, and Settings reads the process environment when it is absent.

FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

WORKDIR /app

# Production dependencies only (requirements-dev.txt adds pytest).
COPY requirements.txt .
RUN pip install -r requirements.txt

COPY app/ app/

RUN useradd --system --no-create-home --uid 10001 agent
USER agent

EXPOSE 8001
# PORT is injected by the host (Railway); HOST can be set to "::" where private networking is IPv6.
CMD ["sh", "-c", "exec uvicorn app.main:app --host ${HOST:-0.0.0.0} --port ${PORT:-8001}"]
