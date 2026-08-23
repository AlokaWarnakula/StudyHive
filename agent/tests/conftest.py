import pytest

from app.settings import settings


@pytest.fixture(autouse=True)
def _no_gemini_key_by_default(monkeypatch: pytest.MonkeyPatch) -> None:
    """Forces the whole suite to run against the deterministic Planner path by default, regardless
    of whatever GEMINI_API_KEY a developer happens to have in their local agent/.env.

    Without this, a real local key would make ordinary test runs silently place live network calls
    to Gemini — slow, flaky, and exactly the non-determinism `settings.gemini_api_key` being unset
    is supposed to guarantee for local/test runs. Tests that specifically exercise the Gemini path
    opt back in with their own `monkeypatch.setattr(settings, "gemini_api_key", ...)`.
    """
    monkeypatch.setattr(settings, "gemini_api_key", "")
