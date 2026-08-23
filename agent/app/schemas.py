"""Wire contracts shared with StudyHive.Api (see api/src/StudyHive.Api/Contracts/PlannerContracts.cs).

JSON over the wire is camelCase everywhere in this project (DOCS §12 shared conventions) — Python
field names stay snake_case (PEP 8) and carry a camelCase `alias` so pydantic validates incoming
requests against the C# client's actual JSON shape and FastAPI serializes responses back the same way.
"""

from __future__ import annotations

from datetime import date, time
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class PlannerRequestItem(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    consumable_id: UUID = Field(alias="consumableId")
    quantity: int


class PlannerRequest(BaseModel):
    """Input contract (DOCS §11 agent-io table). `student_eligible`/`eligibility_reasons` are the
    server's own verdict — see app/agents/planner.py for why this agent never recomputes them."""

    model_config = ConfigDict(populate_by_name=True)

    objective: str
    student_id: UUID = Field(alias="studentId")
    group_size: int = Field(alias="groupSize")
    preferred_date_from: date = Field(alias="preferredDateFrom")
    preferred_date_to: date = Field(alias="preferredDateTo")
    preferred_time_from: time = Field(alias="preferredTimeFrom")
    preferred_time_to: time = Field(alias="preferredTimeTo")
    sessions_required: int = Field(alias="sessionsRequired")
    session_duration_minutes: int = Field(alias="sessionDurationMinutes")
    budget: float
    student_eligible: bool = Field(alias="studentEligible")
    eligibility_reasons: list[str] = Field(default_factory=list, alias="eligibilityReasons")
    requested_items: list[PlannerRequestItem] = Field(default_factory=list, alias="requestedItems")


class PlanStep(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    n: int
    agent: str
    action: str
    params: dict[str, object] = Field(default_factory=dict)


class PlannerResponse(BaseModel):
    """Output contract (DOCS §11 agent-io table): `{ planId, eligible, reasons[], steps[] }`."""

    model_config = ConfigDict(populate_by_name=True)

    plan_id: UUID = Field(alias="planId")
    eligible: bool
    reasons: list[str] = Field(default_factory=list)
    steps: list[PlanStep] = Field(default_factory=list)
