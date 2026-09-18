"""Scheduling Agent (S2).

The agent receives an allow-listed room snapshot from StudyHive.Api, filters it by
capacity/equipment, checks booking and maintenance conflicts, and proposes at most one
session per day. It never accesses PostgreSQL or accepts free-text instructions.
"""

from __future__ import annotations

from datetime import date, datetime, timedelta
from typing import Literal, TypedDict, cast
from zoneinfo import ZoneInfo

from langgraph.graph import END, START, StateGraph

from app.schemas import (
    SchedulingRequest,
    SchedulingResponse,
    SchedulingRoom,
    SchedulingSlot,
    SchedulingTimeBlock,
)

COLOMBO = ZoneInfo("Asia/Colombo")
SEARCH_INCREMENT = timedelta(minutes=30)


class SchedulingState(TypedDict, total=False):
    """Typed state passed between the S2 Scheduling Agent's LangGraph nodes."""

    request: SchedulingRequest
    candidate_rooms: list[SchedulingRoom]
    response: SchedulingResponse


def search_rooms(request: SchedulingRequest) -> list[SchedulingRoom]:
    """Allow-listed tool: retain active rooms satisfying capacity and equipment needs."""
    required = set(request.required_equipment_type_ids)
    rooms = [
        room
        for room in request.rooms
        if room.is_active
        and room.capacity >= request.group_size
        and required.issubset(set(room.equipment_type_ids))
    ]
    return sorted(rooms, key=lambda room: (room.hourly_rate, room.room_name, str(room.room_id)))


def _overlaps(starts_at: datetime, ends_at: datetime, block: SchedulingTimeBlock) -> bool:
    return block.starts_at < ends_at and block.ends_at > starts_at


def check_availability(room: SchedulingRoom, starts_at: datetime, ends_at: datetime) -> bool:
    """Allow-listed tool: reject a slot overlapping a confirmed room booking."""
    return not any(_overlaps(starts_at, ends_at, booking) for booking in room.bookings)


def check_maintenance(room: SchedulingRoom, starts_at: datetime, ends_at: datetime) -> bool:
    """Allow-listed tool: reject a slot overlapping a maintenance window."""
    return not any(_overlaps(starts_at, ends_at, window) for window in room.maintenance_windows)


def _dates(from_date: date, to_date: date):
    current = from_date
    while current <= to_date:
        yield current
        current += timedelta(days=1)


def propose_slots(request: SchedulingRequest, rooms: list[SchedulingRoom]) -> SchedulingResponse:
    """Allow-listed tool: choose the lowest-cost conflict-free room/time for each session."""
    conflicts: list[str] = []
    slots: list[SchedulingSlot] = []
    duration = timedelta(minutes=request.session_duration_minutes)

    if request.preferred_date_to < request.preferred_date_from:
        return SchedulingResponse(conflicts=["Preferred end date is before the start date."])

    if request.preferred_time_to <= request.preferred_time_from:
        return SchedulingResponse(conflicts=["Preferred end time must be later than the start time."])

    if not rooms:
        return SchedulingResponse(
            conflicts=["No active room satisfies the requested capacity and equipment requirements."]
        )

    for day in _dates(request.preferred_date_from, request.preferred_date_to):
        window_start = datetime.combine(day, request.preferred_time_from, tzinfo=COLOMBO)
        window_end = datetime.combine(day, request.preferred_time_to, tzinfo=COLOMBO)
        candidate_start = window_start
        chosen: SchedulingSlot | None = None

        while candidate_start + duration <= window_end and chosen is None:
            candidate_end = candidate_start + duration
            for room in rooms:
                if check_availability(room, candidate_start, candidate_end) and check_maintenance(
                    room, candidate_start, candidate_end
                ):
                    chosen = SchedulingSlot(
                        roomId=room.room_id,
                        roomName=room.room_name,
                        startsAt=candidate_start,
                        endsAt=candidate_end,
                        hourlyRate=room.hourly_rate,
                    )
                    break
            candidate_start += SEARCH_INCREMENT

        if chosen is None:
            conflicts.append(f"No conflict-free room is available on {day.isoformat()}.")
            continue

        slots.append(chosen)
        if len(slots) == request.sessions_required:
            break

    if len(slots) < request.sessions_required:
        conflicts.append(
            f"Requested {request.sessions_required} session(s), but only {len(slots)} could be scheduled."
        )

    return SchedulingResponse(slots=slots, conflicts=conflicts)


def _search_rooms_node(state: SchedulingState) -> SchedulingState:
    return {"candidate_rooms": search_rooms(state["request"])}


def _route_after_room_search(
    state: SchedulingState,
) -> Literal["propose_slots", "no_suitable_room"]:
    return "propose_slots" if state["candidate_rooms"] else "no_suitable_room"


def _propose_slots_node(state: SchedulingState) -> SchedulingState:
    return {
        "response": propose_slots(state["request"], state["candidate_rooms"]),
    }


def _no_suitable_room_node(state: SchedulingState) -> SchedulingState:
    return {
        "response": SchedulingResponse(
            conflicts=[
                "No active room satisfies the requested capacity and equipment requirements."
            ]
        )
    }


def _build_scheduling_graph():
    graph = StateGraph(SchedulingState)
    graph.add_node("search_rooms", _search_rooms_node)
    graph.add_node("propose_slots", _propose_slots_node)
    graph.add_node("no_suitable_room", _no_suitable_room_node)
    graph.add_edge(START, "search_rooms")
    graph.add_conditional_edges(
        "search_rooms",
        _route_after_room_search,
        {
            "propose_slots": "propose_slots",
            "no_suitable_room": "no_suitable_room",
        },
    )
    graph.add_edge("propose_slots", END)
    graph.add_edge("no_suitable_room", END)
    return graph.compile()


SCHEDULING_GRAPH = _build_scheduling_graph()


def schedule(request: SchedulingRequest) -> SchedulingResponse:
    """Run the S2 tools through the compiled LangGraph workflow."""
    result = SCHEDULING_GRAPH.invoke({"request": request})
    return cast(SchedulingResponse, result["response"])
