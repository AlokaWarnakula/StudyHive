import uuid

from fastapi.testclient import TestClient

from app.agents.scheduling import SCHEDULING_GRAPH
from app.main import app
from app.settings import settings

client = TestClient(app)
AUTH_HEADERS = {"X-Internal-Api-Key": settings.internal_api_key}


def test_scheduling_is_a_compiled_langgraph_workflow() -> None:
    nodes = set(SCHEDULING_GRAPH.get_graph().nodes)
    assert {"search_rooms", "propose_slots", "no_suitable_room"}.issubset(nodes)


def _room(
    *,
    name: str = "B-204",
    capacity: int = 8,
    hourly_rate: float = 150.0,
    equipment: list[str] | None = None,
    bookings: list[dict[str, str]] | None = None,
    maintenance: list[dict[str, str]] | None = None,
) -> dict[str, object]:
    return {
        "roomId": str(uuid.uuid4()),
        "roomName": name,
        "capacity": capacity,
        "hourlyRate": hourly_rate,
        "isActive": True,
        "equipmentTypeIds": equipment or [],
        "bookings": bookings or [],
        "maintenanceWindows": maintenance or [],
    }


def _request(*, rooms: list[dict[str, object]], **overrides: object) -> dict[str, object]:
    body: dict[str, object] = {
        "groupSize": 4,
        "preferredDateFrom": "2026-09-20",
        "preferredDateTo": "2026-09-20",
        "preferredTimeFrom": "09:00:00",
        "preferredTimeTo": "12:00:00",
        "sessionsRequired": 1,
        "sessionDurationMinutes": 60,
        "requiredEquipmentTypeIds": [],
        "rooms": rooms,
    }
    body.update(overrides)
    return body


def test_scheduling_endpoint_requires_internal_api_key() -> None:
    response = client.post("/scheduling/propose", json=_request(rooms=[_room()]))
    assert response.status_code == 401


def test_scheduling_selects_the_lowest_cost_suitable_room() -> None:
    expensive = _room(name="Expensive", hourly_rate=400)
    affordable = _room(name="Affordable", hourly_rate=100)

    response = client.post(
        "/scheduling/propose",
        json=_request(rooms=[expensive, affordable]),
        headers=AUTH_HEADERS,
    )

    assert response.status_code == 200
    body = response.json()
    assert len(body["slots"]) == 1
    assert body["slots"][0]["roomId"] == affordable["roomId"]
    assert body["conflicts"] == []


def test_scheduling_filters_capacity_and_required_equipment() -> None:
    equipment_id = str(uuid.uuid4())
    too_small = _room(name="Small", capacity=2, equipment=[equipment_id])
    missing_equipment = _room(name="No projector", capacity=10)
    suitable = _room(name="Suitable", capacity=10, equipment=[equipment_id])

    response = client.post(
        "/scheduling/propose",
        json=_request(
            rooms=[too_small, missing_equipment, suitable],
            requiredEquipmentTypeIds=[equipment_id],
        ),
        headers=AUTH_HEADERS,
    )

    assert response.status_code == 200
    assert response.json()["slots"][0]["roomId"] == suitable["roomId"]


def test_scheduling_graph_returns_a_clear_conflict_when_no_room_is_suitable() -> None:
    response = client.post(
        "/scheduling/propose",
        json=_request(rooms=[_room(capacity=2)], groupSize=10),
        headers=AUTH_HEADERS,
    )

    assert response.status_code == 200
    assert response.json() == {
        "slots": [],
        "conflicts": [
            "No active room satisfies the requested capacity and equipment requirements."
        ],
    }


def test_scheduling_moves_past_a_booking_conflict() -> None:
    room = _room(
        bookings=[{"startsAt": "2026-09-20T09:00:00+05:30", "endsAt": "2026-09-20T10:00:00+05:30"}]
    )

    response = client.post(
        "/scheduling/propose", json=_request(rooms=[room]), headers=AUTH_HEADERS
    )

    assert response.status_code == 200
    assert response.json()["slots"][0]["startsAt"] == "2026-09-20T10:00:00+05:30"


def test_scheduling_avoids_maintenance() -> None:
    room = _room(
        maintenance=[{"startsAt": "2026-09-20T09:00:00+05:30", "endsAt": "2026-09-20T11:00:00+05:30"}]
    )

    response = client.post(
        "/scheduling/propose", json=_request(rooms=[room]), headers=AUTH_HEADERS
    )

    assert response.status_code == 200
    assert response.json()["slots"][0]["startsAt"] == "2026-09-20T11:00:00+05:30"


def test_no_availability_returns_no_slots_and_a_clear_conflict() -> None:
    room = _room(
        bookings=[{"startsAt": "2026-09-20T09:00:00+05:30", "endsAt": "2026-09-20T12:00:00+05:30"}]
    )

    response = client.post(
        "/scheduling/propose", json=_request(rooms=[room]), headers=AUTH_HEADERS
    )

    assert response.status_code == 200
    body = response.json()
    assert body["slots"] == []
    assert body["conflicts"]


def test_invalid_request_is_rejected_by_the_contract() -> None:
    response = client.post(
        "/scheduling/propose",
        json=_request(rooms=[_room()], groupSize=0),
        headers=AUTH_HEADERS,
    )

    assert response.status_code == 422
