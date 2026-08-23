from pydantic import field_validator, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

# The well-known placeholder from .env.example. Anyone who reads this source file (or the repo
# history) knows this value, so it must never authenticate a real deployment.
PLACEHOLDER_INTERNAL_API_KEY = "dev-only-internal-key-DO-NOT-USE-IN-PRODUCTION"

DEFAULT_GEMINI_MODEL = "gemini-3.6-flash"


class Settings(BaseSettings):
    """Loaded from environment variables / .env. Never commit real API keys — see .env.example."""

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # Mirrors ASPNETCORE_ENVIRONMENT's role in api/src/StudyHive.Api/Program.cs: "development" is
    # the only value that unlocks any insecure fallback below. Defaults to "production" — the
    # strict/fail-safe side — not "development", so an operator who sets up a real deployment and
    # forgets ENVIRONMENT entirely still gets the placeholder-key guard below, rather than quietly
    # inheriting local-dev leniency by omission. Local dev opts in explicitly via .env (see
    # .env.example, copied to `agent/.env` per the README's setup steps).
    environment: str = "production"

    groq_api_key: str = ""
    gemini_api_key: str = ""
    # Lets whoever holds the key point at whichever Gemini Flash model their Google AI Studio
    # account currently has access to, without a code change — model availability/naming changes
    # faster than this file does.
    gemini_model: str = DEFAULT_GEMINI_MODEL
    internal_api_key: str = PLACEHOLDER_INTERNAL_API_KEY

    # Mirrors api/appsettings.json WorkflowLimits — see DOCS Master Plan sec. 11.
    tool_call_timeout_seconds: int = 15
    single_agent_timeout_seconds: int = 45
    whole_workflow_timeout_seconds: int = 180
    max_retries_per_step: int = 2
    max_llm_tokens_per_run: int = 8000

    @field_validator("gemini_model", mode="before")
    @classmethod
    def _blank_gemini_model_means_use_the_default(cls, v: str | None) -> str:
        # Unlike GROQ_API_KEY/GEMINI_API_KEY (blank == intentionally disabled), an empty
        # GEMINI_MODEL isn't a valid model id — .env.example ships this line blank so copying it
        # verbatim must still resolve to DEFAULT_GEMINI_MODEL, not an empty string that breaks
        # every Gemini call once a key is configured.
        return v or DEFAULT_GEMINI_MODEL

    @model_validator(mode="after")
    def _require_a_real_internal_api_key_outside_development(self) -> "Settings":
        # Security review, P1: a missing INTERNAL_API_KEY outside development used to fall back
        # to this public placeholder, so an operator who forgot to set it would silently ship a
        # service anyone could authenticate against by reading this file. Fail fast instead, the
        # same way Program.cs already does for Jwt:SigningKey and ConnectionStrings:Default.
        if self.environment.lower() != "development" and (
            not self.internal_api_key or self.internal_api_key == PLACEHOLDER_INTERNAL_API_KEY
        ):
            raise ValueError(
                "INTERNAL_API_KEY must be set to a real secret when ENVIRONMENT is not 'development'."
            )
        return self


settings = Settings()
