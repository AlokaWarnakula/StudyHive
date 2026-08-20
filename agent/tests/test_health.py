from fastapi.testclient import TestClient

from app.main import app
from app.settings import settings

client = TestClient(app)


def test_health_returns_healthy() -> None:
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "healthy"}


def test_config_limits_matches_plan_defaults() -> None:
    response = client.get("/config/limits", headers={"X-Internal-Api-Key": settings.internal_api_key})
    assert response.status_code == 200
    body = response.json()
    assert body["wholeWorkflowTimeoutSeconds"] == 180
    assert body["maxRetriesPerStep"] == 2
