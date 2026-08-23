import { Screen } from "../../components/AppShell";
import {
  CheckRow,
  Field,
  FixtureNotice,
  KeyValue,
  NotBuiltYet,
  TagOf,
  TextField,
  Tile,
} from "../../components/ui";
import { useFixture } from "../../dev/useFixture";

/**
 * W-26 · Settings — system configuration: booking rules, pricing defaults, email and the agent
 * service. Every change is written to the audit log. Admin only.
 */
export function SettingsPage() {
  const fixture = useFixture((f) => f.settings);

  if (!fixture.enabled) {
    return (
      <Screen title="Settings" crumb="Changes are written to the audit log">
        <NotBuiltYet owner="Admin configuration" what="System settings" />
      </Screen>
    );
  }

  const s = fixture.data;

  return (
    <Screen
      title="Settings"
      crumb="Changes are written to the audit log"
      showUser={false}
      actions={
        <button type="button" className="btn btn-primary">
          Save changes
        </button>
      }
    >
      <FixtureNotice owner="Admin" what="System configuration" />

      <div className="k2">
        <div className="stack">
          <Tile label="Booking rules">
            <TextField label="Bookings allowed per student per week" defaultValue={s.booking.perWeek} />
            <TextField label="Longest single booking (hours)" defaultValue={s.booking.longestHours} />
            <TextField label="How far ahead students can book (days)" defaultValue={s.booking.aheadDays} />
            <TextField label="Hold items for (minutes) before approval expires" defaultValue={s.booking.holdMinutes} />
            <CheckRow label="Require QR check-in at the room" defaultChecked={s.booking.requireQr} />
            <CheckRow label="Cancel bookings with no check-in after 20 minutes" defaultChecked={s.booking.cancelNoShow} />
          </Tile>

          <Tile label="Email (Brevo)">
            <TextField label="Sender name" defaultValue={s.email.senderName} />
            <TextField label="Sender address" defaultValue={s.email.senderAddress} />
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              <CheckRow label="Email students on approval or rejection" defaultChecked={s.email.onDecision} />
              <CheckRow label="Email store officer on low stock" defaultChecked={s.email.onLowStock} />
              <CheckRow label="Daily summary to librarians" defaultChecked={s.email.dailySummary} />
            </div>
            <div className="kv" style={{ borderTop: "1px solid var(--color-divider)", paddingTop: 10 }}>
              <span>Connection</span>
              <TagOf tag={s.email.connection} />
            </div>
          </Tile>
        </div>

        <div className="stack">
          <Tile label="Pricing defaults">
            <div className="k2">
              <TextField label="Room rate per hour (Rs.)" defaultValue={s.pricing.roomRate} />
              <TextField label="Projector use (Rs.)" defaultValue={s.pricing.projector} />
              <TextField label="Sound system (Rs.)" defaultValue={s.pricing.soundSystem} />
              <TextField label="Monthly department budget (Rs.)" defaultValue={s.pricing.monthlyBudget} />
            </div>
          </Tile>

          <Tile label="Agent service">
            <TextField label="Internal URL" defaultValue={s.agent.url} />
            <Field label="Model">
              <select className="input" aria-label="Model" defaultValue={s.agent.model}>
                <option>{s.agent.model}</option>
              </select>
            </Field>
            <TextField label="Timeout per step (seconds)" defaultValue={s.agent.timeoutSeconds} />
            {s.agent.health.map((h) => (
              <KeyValue key={h.label} label={h.label}>
                <TagOf tag={h.tag} />
              </KeyValue>
            ))}
            <button type="button" className="btn btn-secondary btn-block">
              Run a test workflow
            </button>
          </Tile>

          <Tile label="Opening hours">
            <div className="k2">
              <TextField label="Weekdays open" defaultValue={s.hours.weekdayOpen} />
              <TextField label="Weekdays close" defaultValue={s.hours.weekdayClose} />
              <TextField label="Saturday open" defaultValue={s.hours.saturdayOpen} />
              <TextField label="Saturday close" defaultValue={s.hours.saturdayClose} />
            </div>
            <CheckRow label="Closed on Sundays and public holidays" defaultChecked={s.hours.closedSundays} />
          </Tile>
        </div>
      </div>
    </Screen>
  );
}
