import { useEffect, useMemo, useState } from "react";
import { ApiError } from "../../api/client";
import { getRoomUsageReport, type RoomUsageReport } from "../../api/rooms";
import { Screen } from "../../components/AppShell";
import { Bars, KeyValue, Meter, MetricTiles, Tile } from "../../components/ui";
import { useAuthStore } from "../../store/authStore";
import { ReportTabs } from "./ReportsPage";

/**
 * W-18 · Room utilisation report — S2's own report: utilisation by room, peak hours, no-shows.
 */
export function RoomUtilisationPage() {
  const token = useAuthStore((s) => s.accessToken);
  const [month, setMonth] = useState(() => new Date().toISOString().slice(0, 7));
  const [report, setReport] = useState<RoomUsageReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    const [year, monthNumber] = month.split("-").map(Number);
    const from = new Date(Date.UTC(year, monthNumber - 1, 1)).toISOString();
    const to = new Date(Date.UTC(year, monthNumber, 1)).toISOString();
    setLoading(true);
    setError(null);
    getRoomUsageReport(token, from, to)
      .then((data) => !cancelled && setReport(data))
      .catch((reason) => !cancelled && setError(reason instanceof ApiError ? reason.message : "Failed to load room usage."))
      .finally(() => !cancelled && setLoading(false));
    return () => { cancelled = true; };
  }, [token, month]);

  const chart = useMemo(() => {
    const counts = report?.bookingsByHour.map((entry) => entry.bookingCount) ?? [];
    const maximum = Math.max(1, ...counts);
    return {
      values: counts.map((count) => count / maximum * 100),
      peakIndex: counts.length === 0 ? undefined : counts.indexOf(Math.max(...counts)),
      labels: report?.bookingsByHour.map((entry) => `${entry.hour.toString().padStart(2, "0")}:00`) ?? [],
    };
  }, [report]);

  return (
    <Screen
      title="Room utilisation"
      showUser={false}
      actions={
        <input className="input" style={{ width: 170 }} type="month" value={month}
          onChange={(event) => { if (event.target.value) setMonth(event.target.value); }} aria-label="Report month" />
      }
    >
      <ReportTabs />
      {error && <p role="alert" className="form-error">{error}</p>}
      {loading && !report && <div className="state-view">Loading…</div>}
      {report && <>
        <MetricTiles metrics={[
          { label: "Total bookings", value: String(report.totalBookings) },
          { label: "Booked hours", value: report.totalBookedHours.toFixed(1) },
          { label: "Average utilisation", value: `${report.averageUtilisationPercent.toFixed(1)}%`, note: report.busiestRoom ? `Busiest: ${report.busiestRoom}` : undefined },
          { label: "No-shows", value: String(report.noShows) },
        ]} />
        <div className="k2">
          <Tile label="Utilisation by room">
            {report.byRoom.length === 0 ? <div className="state-view">No rooms found.</div> :
              <div style={{ display: "flex", flexDirection: "column", gap: 10, marginTop: 6 }}>
                {report.byRoom.map((row) => <div key={row.roomId}>
                  <KeyValue label={row.roomName}>{`${row.utilisationPercent.toFixed(1)}%`}</KeyValue>
                  <Meter percent={row.utilisationPercent} height={9} />
                </div>)}
              </div>}
          </Tile>
          <Tile label="Bookings by hour of day">
            <Bars values={chart.values} peakIndex={chart.peakIndex} labels={chart.labels} />
          </Tile>
        </div>
      </>}
    </Screen>
  );
}
