from fastapi.testclient import TestClient

from app.main import app
from app.settings import settings

client = TestClient(app)


def test_health_does_not_require_an_internal_api_key() -> None:
    response = client.get("/health")
    assert response.status_code == 200


def test_protected_route_without_a_key_is_rejected() -> None:
    response = client.get("/config/limits")
    assert response.status_code == 401


def test_protected_route_with_the_wrong_key_is_rejected() -> None:
    response = client.get("/config/limits", headers={"X-Internal-Api-Key": "not-the-real-key"})
    assert response.status_code == 401


def test_protected_route_with_the_correct_key_succeeds() -> None:
    response = client.get("/config/limits", headers={"X-Internal-Api-Key": settings.internal_api_key})
    assert response.status_code == 200
