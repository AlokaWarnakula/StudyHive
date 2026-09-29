import pytest
from pydantic import ValidationError

from app.settings import DEFAULT_GROK_BASE_URL, DEFAULT_GROK_MODEL, PLACEHOLDER_INTERNAL_API_KEY, Settings


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


def test_grok_settings_default_when_unset() -> None:
    s = Settings(_env_file=None, environment="development", internal_api_key=PLACEHOLDER_INTERNAL_API_KEY)
    assert s.grok_api_key == ""
    assert s.grok_base_url == DEFAULT_GROK_BASE_URL
    assert s.grok_model == DEFAULT_GROK_MODEL


def test_grok_model_and_base_url_blank_fall_back_to_the_defaults() -> None:
    """.env.example ships GROK_MODEL blank — copying it verbatim into .env must not turn into an
    empty model id (or URL) that breaks every Grok call once GROK_API_KEY is set."""
    s = Settings(
        environment="development",
        internal_api_key=PLACEHOLDER_INTERNAL_API_KEY,
        grok_model="",
        grok_base_url="",
    )
    assert s.grok_model == DEFAULT_GROK_MODEL
    assert s.grok_base_url == DEFAULT_GROK_BASE_URL


def test_grok_model_override_is_respected() -> None:
    s = Settings(
        environment="development",
        internal_api_key=PLACEHOLDER_INTERNAL_API_KEY,
        grok_model="grok-4.3",
    )
    assert s.grok_model == "grok-4.3"
