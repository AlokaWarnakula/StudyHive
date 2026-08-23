import { useNavigate } from "react-router-dom";
import { Screen } from "../../components/AppShell";
import { Icon } from "../../components/Icon";
import { FixtureNotice, NotBuiltYet } from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-15 · Room calendar — GET /api/rooms/schedule?from=&to=: booked, held and maintenance blocks
 * in one week grid. Owned by S2.
 */
export function RoomCalendarPage() {
  const navigate = useNavigate();
  const fixture = useFixture((f) => f.calendar);

  if (!fixture.enabled) {
    return (
      <Screen title="Room calendar" onBack={() => navigate("/rooms")}>
        <NotBuiltYet owner="S2 rooms" what="The room calendar" />
      </Screen>
    );
  }

  const c = fixture.data;

  return (
    <Screen
      title="Room calendar"
      onBack={() => navigate("/rooms")}
      showUser={false}
      actions={
        <>
          <button type="button" className="btn btn-secondary">
            Today
          </button>
          <button type="button" className="btn btn-ghost btn-icon" aria-label="Previous week">
            <Icon name="chevron-left" />
          </button>
          <b style={{ fontSize: 14 }}>{c.range}</b>
          <button type="button" className="btn btn-ghost btn-icon" aria-label="Next week">
            <Icon name="chevron-right" />
          </button>
          <select className="input" style={{ width: 150 }} aria-label="Room filter">
            <option>All rooms</option>
          </select>
        </>
      }
    >
      <FixtureNotice owner="S2" what="The week's bookings and maintenance blocks" />

      <div className="bar" style={{ fontSize: 12, gap: 16 }}>
        <span className="cal-key">
          <span className="cal-swatch cal-swatch--approved" />
          Approved
        </span>
        <span className="cal-key">
          <span className="cal-swatch cal-swatch--pending" />
          Pending
        </span>
        <span className="cal-key">
          <span className="cal-swatch cal-swatch--maintenance" />
          Maintenance
        </span>
      </div>

      <div className="table-scroll">
        <div className="cal" role="table" aria-label={`Room calendar, ${c.range}`}>
          <div className="cal-corner" />
          {c.days.map((d) => (
            <div key={d} className="cal-head">
              <b>{d}</b>
            </div>
          ))}

          {c.slots.map((slot) => (
            <div key={slot} style={{ display: "contents" }}>
              <div className="cal-slot text-muted">{slot}</div>
              {c.days.map((_, dayIndex) => {
                const entry = c.entries.find((e) => e.slot === slot && e.day === dayIndex);
                return (
                  <div key={`${slot}-${dayIndex}`} className="cal-cell">
                    {entry && <div className={`cal-ev cal-ev--${entry.kind}`}>{entry.label}</div>}
                  </div>
                );
              })}
            </div>
          ))}
        </div>
      </div>
    </Screen>
  );
}
