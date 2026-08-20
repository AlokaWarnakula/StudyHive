"""Internal-only boundary enforcement.

The agent service is never reachable from React or Flutter — only from the ASP.NET Core API,
over a shared secret. This was flagged in security review: `/config/limits` had no validation at
all, so anyone who could reach the port could read it. Every route except `/health` (and FastAPI's
own docs) must go through `require_internal_api_key`.
"""

import hmac

from fastapi import Header, HTTPException, status

from app.settings import settings

INTERNAL_API_KEY_HEADER = "X-Internal-Api-Key"


async def require_internal_api_key(
    x_internal_api_key: str | None = Header(default=None, alias=INTERNAL_API_KEY_HEADER),
) -> None:
    # hmac.compare_digest is constant-time — a plain `==` would let an attacker learn the key
    # one byte at a time from response-time differences.
    if x_internal_api_key is None or not hmac.compare_digest(x_internal_api_key, settings.internal_api_key):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing or invalid internal API key.",
        )
