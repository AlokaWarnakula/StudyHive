import pytest

from app.settings import settings


@pytest.fixture(autouse=True)
def _no_grok_key_by_default(monkeypatch: pytest.MonkeyPatch) -> None:
    """Forces the whole suite to run against the deterministic path by default, regardless of
    whatever GROK_API_KEY a developer happens to have in their local agent/.env.

    Without this, a real local key would make ordinary test runs silently place live network calls
    to xAI — slow, flaky, billed, and exactly the non-determinism `settings.grok_api_key` being unset
    is supposed to guarantee for local/test runs. Tests that exercise the LLM path monkeypatch the
    `app.llm.chat` seam (or `httpx.post` inside it) and never reach the network.
    """
    monkeypatch.setattr(settings, "grok_api_key", "")
