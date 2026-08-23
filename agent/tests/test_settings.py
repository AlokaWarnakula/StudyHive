import pytest
from pydantic import ValidationError

from app.settings import DEFAULT_GEMINI_MODEL, PLACEHOLDER_INTERNAL_API_KEY, Settings


def test_placeholder_key_outside_development_fails_to_start() -> None:
    with pytest.raises(ValidationError, match="INTERNAL_API_KEY must be set"):
        Settings(environment="production", internal_api_key=PLACEHOLDER_INTERNAL_API_KEY)


def test_missing_key_outside_development_fails_to_start() -> None:
    with pytest.raises(ValidationError, match="INTERNAL_API_KEY must be set"):
        Settings(environment="production", internal_api_key="")


def test_placeholder_key_is_allowed_in_development() -> None:
    s = Settings(environment="development", internal_api_key=PLACEHOLDER_INTERNAL_API_KEY)
    assert s.internal_api_key == PLACEHOLDER_INTERNAL_API_KEY


def test_real_key_is_allowed_outside_development() -> None:
    s = Settings(environment="production", internal_api_key="a-real-shared-secret")
    assert s.internal_api_key == "a-real-shared-secret"


def test_nothing_configured_at_all_fails_to_start() -> None:
    """The exact gap flagged in review: an operator who sets neither ENVIRONMENT nor
    INTERNAL_API_KEY must not silently get a working-but-insecure service. `_env_file=None`
    bypasses any local .env so this reflects the class defaults alone, same as a bare deploy."""
    with pytest.raises(ValidationError, match="INTERNAL_API_KEY must be set"):
        Settings(_env_file=None)


def test_gemini_model_defaults_when_unset() -> None:
    s = Settings(environment="development", internal_api_key=PLACEHOLDER_INTERNAL_API_KEY)
    assert s.gemini_model == DEFAULT_GEMINI_MODEL


def test_gemini_model_blank_falls_back_to_the_default() -> None:
    """.env.example ships GEMINI_MODEL blank — copying it verbatim into .env must not turn into an
    empty model id that breaks every Gemini call once GEMINI_API_KEY is set."""
    s = Settings(environment="development", internal_api_key=PLACEHOLDER_INTERNAL_API_KEY, gemini_model="")
    assert s.gemini_model == DEFAULT_GEMINI_MODEL


def test_gemini_model_override_is_respected() -> None:
    s = Settings(
        environment="development",
        internal_api_key=PLACEHOLDER_INTERNAL_API_KEY,
        gemini_model="gemini-2.5-flash",
    )
    assert s.gemini_model == "gemini-2.5-flash"
