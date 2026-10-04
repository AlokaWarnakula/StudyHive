/**
 * The library's own clock. Asia/Colombo is fixed UTC+05:30, so staff see the same times as the
 * students' app whatever time zone their browser is in (AUDIT C-14 / CW-11).
 */
const day = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", weekday: "short", day: "numeric", month: "short" });
const time = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", hour: "2-digit", minute: "2-digit", hour12: false });

/** "14:05" */
export function colomboTime(iso: string): string {
  return time.format(new Date(iso));
}

/** "Mon 6 Oct" */
export function colomboDay(iso: string): string {
  return day.format(new Date(iso));
}

/** "Mon 6 Oct · 14:00–16:00" */
export function colomboSlot(startsAt: string, endsAt: string): string {
  return `${colomboDay(startsAt)} · ${colomboTime(startsAt)}–${colomboTime(endsAt)}`;
}

/** Today's date in Colombo, "2026-10-06", as the API's dueOn expects. */
export function colomboToday(): string {
  return new Date(Date.now() + 330 * 60_000).toISOString().slice(0, 10);
}

/** "Mon 6 Oct, 14:05" (CW-11) */
export function colomboStamp(iso: string): string {
  return `${colomboDay(iso)}, ${colomboTime(iso)}`;
}
