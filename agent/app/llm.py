"""The agent service's one LLM seam: a single OpenAI-compatible chat call to xAI Grok.

The LLM never decides anything (PLAN.md §4) — callers only ever use the reply as a short descriptive
string and must validate it themselves. Untrusted text (e.g. a student's objective) goes in `data`,
which is sent as a JSON user message, never concatenated into the system instructions.

`chat()` returns None — never raises — when GROK_API_KEY is unset, the request fails or times out,
or the reply is empty/malformed, so every caller can fall straight back to its deterministic path.
Tests monkeypatch `chat` itself; nothing in the test suite reaches the network.
"""

from __future__ import annotations

import json
import logging
from typing import Any

import httpx

from app.settings import settings

logger = logging.getLogger(__name__)

MAX_TOKENS = 150


def chat(instructions: str, data: dict[str, Any]) -> str | None:
    """Asks Grok to act on `data` following `instructions`; returns the stripped reply text or None."""
    if not settings.grok_api_key:
        return None

    try:
        response = httpx.post(
            f"{settings.grok_base_url.rstrip('/')}/chat/completions",
            headers={"Authorization": f"Bearer {settings.grok_api_key}"},
            json={
                "model": settings.grok_model,
                "temperature": 0.2,
                "max_tokens": MAX_TOKENS,
                "messages": [
                    {"role": "system", "content": instructions},
                    {"role": "user", "content": json.dumps(data, ensure_ascii=False)},
                ],
            },
            timeout=settings.tool_call_timeout_seconds,
        )
        response.raise_for_status()
        text = response.json()["choices"][0]["message"]["content"]
    except Exception as exc:
        # Deliberately no exc_info: an httpx error can carry the request, and the request carries
        # the Authorization header. The exception type is enough to diagnose from.
        logger.warning("Grok chat call failed (%s); caller falls back to its deterministic path", type(exc).__name__)
        return None

    if not isinstance(text, str) or not text.strip():
        return None
    return text.strip()
