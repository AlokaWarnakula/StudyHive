import { useEffect, type ReactNode } from "react";
import type { StatusTag } from "../dev/fixtures";
import { Icon } from "./Icon";

/**
 * The handful of pieces every reference screen is assembled from — the tile, the key/value row,
 * the meter, the bar chart, the step timeline, the dialog and the pagination footer. They map 1:1
 * onto the reference document's own `.tile` / `.kv` / `.bars` / `.tl` / `.dialog` / `.pg` classes,
 * so a screen here reads like the frame it was drawn from.
 */

/* ── tags ─────────────────────────────────────────────────────────────────── */

export function Tag({ tone = "neutral", children }: { tone?: StatusTag["tone"]; children: ReactNode }) {
  return <span className={`tag tag-${tone}`}>{children}</span>;
}

/** Renders a `StatusTag` from the fixture set (or any {label, tone} pair). */
export function TagOf({ tag }: { tag: StatusTag }) {
  return <Tag tone={tag.tone}>{tag.label}</Tag>;
}

/* ── layout ───────────────────────────────────────────────────────────────── */

export function Tile({
  label,
  children,
  className = "",
  accented = false,
  action,
}: {
  label?: string;
  children?: ReactNode;
  className?: string;
  /** The reference outlines a tile in the accent colour when it is the screen's active panel. */
  accented?: boolean;
  action?: ReactNode;
}) {
  return (
    <div className={`tile ${className}`} style={accented ? { borderColor: "var(--color-accent)" } : undefined}>
      {(label || action) && (
        <div className="bar">
          {label && <span className="lbl">{label}</span>}
          {action && <span style={{ marginLeft: "auto" }}>{action}</span>}
        </div>
      )}
      {children}
    </div>
  );
}

export interface Metric {
  label: string;
  value: string;
  note?: string;
  highlight?: boolean;
}

/** The k4/k3 metric strip at the top of the dashboard and every report. */
export function MetricTiles({ metrics, columns = 4 }: { metrics: readonly Metric[]; columns?: 3 | 4 }) {
  return (
    <div className={columns === 3 ? "k3" : "k4"}>
      {metrics.map((m) => (
        <div className="tile" key={m.label} style={m.highlight ? { borderColor: "var(--color-accent)" } : undefined}>
          <span className="lbl">{m.label}</span>
          <span className="big" style={m.highlight ? { color: "var(--color-accent-700)" } : undefined}>
            {m.value}
          </span>
          {m.note && <span className="fnote">{m.note}</span>}
        </div>
      ))}
    </div>
  );
}

export function KeyValue({ label, children, rule = false }: { label: ReactNode; children: ReactNode; rule?: boolean }) {
  return (
    <div className={rule ? "kv kv-rule" : "kv"}>
      <span>{label}</span>
      {typeof children === "string" || typeof children === "number" ? <b>{children}</b> : children}
    </div>
  );
}

export function Meter({ percent, height = 10 }: { percent: number; height?: number }) {
  return (
    <div className="meter" style={{ height }} role="img" aria-label={`${percent}%`}>
      <div style={{ width: `${percent}%` }} />
    </div>
  );
}

/** Vertical bar chart. `peakIndex` gets the solid accent fill the reference uses for the peak. */
export function Bars({
  values,
  peakIndex,
  labels,
}: {
  values: readonly number[];
  peakIndex?: number;
  labels?: readonly string[];
}) {
  return (
    <>
      <div className="bars">
        {values.map((v, i) => (
          <div key={i} className={i === peakIndex ? "hi" : undefined} style={{ height: `${v}%` }} />
        ))}
      </div>
      {labels && (
        <div className="bars-axis">
          {labels.map((l) => (
            <span key={l}>{l}</span>
          ))}
        </div>
      )}
    </>
  );
}

export interface TimelineStep {
  title: string;
  detail?: string;
  /** done = filled dot, current = outlined dot, waiting = greyed dot. */
  state: "done" | "current" | "waiting";
}

export function Timeline({ steps }: { steps: readonly TimelineStep[] }) {
  return (
    <div className="tl">
      {steps.map((s, i) => (
        <div key={s.title} style={{ display: "contents" }}>
          <div>
            <div className={`dot${s.state === "done" ? " fill" : s.state === "waiting" ? " wait" : ""}`} />
            <div className={s.state === "waiting" ? "text-muted" : undefined}>
              <b>{s.title}</b>
              {s.detail && <div className="fnote">{s.detail}</div>}
            </div>
          </div>
          {i < steps.length - 1 && <div className="stem" />}
        </div>
      ))}
    </div>
  );
}

/* ── toolbar / pagination ─────────────────────────────────────────────────── */

/** The reference's filter row: search box, a few selects, sometimes a right-aligned action. */
export function Toolbar({ children }: { children: ReactNode }) {
  return <div className="bar">{children}</div>;
}

export function Select({
  label,
  options,
  value,
  onChange,
  width = 170,
}: {
  label: string;
  options: readonly string[];
  value?: string;
  onChange?: (value: string) => void;
  width?: number;
}) {
  return (
    <select
      className="input"
      style={{ maxWidth: width }}
      aria-label={label}
      value={value}
      onChange={(e) => onChange?.(e.target.value)}
    >
      {options.map((o) => (
        <option key={o} value={o}>
          {`${label}: ${o}`}
        </option>
      ))}
    </select>
  );
}

export function Pagination({
  showing,
  onPrevious,
  onNext,
  disablePrevious,
  disableNext,
}: {
  showing: string;
  onPrevious?: () => void;
  onNext?: () => void;
  disablePrevious?: boolean;
  disableNext?: boolean;
}) {
  return (
    <div className="pg">
      <span>{showing}</span>
      <span style={{ display: "flex", gap: 8 }}>
        <button type="button" className="btn btn-secondary" onClick={onPrevious} disabled={disablePrevious}>
          Previous
        </button>
        <button type="button" className="btn btn-secondary" onClick={onNext} disabled={disableNext}>
          Next
        </button>
      </span>
    </div>
  );
}

/* ── dialog ───────────────────────────────────────────────────────────────── */

/** Modal dialog — Escape closes, focus is trapped by the backdrop click target. */
export function Dialog({
  title,
  body,
  onClose,
  children,
  actions,
  width,
}: {
  title: string;
  body?: string;
  onClose: () => void;
  children?: ReactNode;
  actions?: ReactNode;
  width?: number;
}) {
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="dialog-backdrop" onClick={onClose}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-label={title}
        style={width ? { width } : undefined}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="dialog-title">{title}</div>
        {body && <div className="dialog-body">{body}</div>}
        {children}
        <div className="dialog-actions">
          {actions ?? (
            <button type="button" className="btn btn-secondary" onClick={onClose}>
              Close
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

/* ── field helpers ────────────────────────────────────────────────────────── */

export function Field({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="field">
      <label>{label}</label>
      {children}
    </div>
  );
}

/** Text field with its own label — used by the two form-heavy screens (W-17, W-26). */
export function TextField({
  label,
  defaultValue,
  placeholder,
  disabled,
  textarea,
}: {
  label: string;
  defaultValue?: string;
  placeholder?: string;
  disabled?: boolean;
  textarea?: boolean;
}) {
  return (
    <Field label={label}>
      {textarea ? (
        <textarea className="input" defaultValue={defaultValue} placeholder={placeholder} aria-label={label} />
      ) : (
        <input
          className="input"
          defaultValue={defaultValue}
          placeholder={placeholder}
          disabled={disabled}
          aria-label={label}
        />
      )}
    </Field>
  );
}

export function CheckRow({ label, defaultChecked }: { label: string; defaultChecked?: boolean }) {
  return (
    <label className="radio">
      <input type="checkbox" defaultChecked={defaultChecked} />
      <span className="dot" />
      {label}
    </label>
  );
}

/* ── hatched placeholder ──────────────────────────────────────────────────── */

export function Placeholder({ label, width, height }: { label?: string; width?: number | string; height?: number | string }) {
  return (
    <div className="ph" style={{ width, height }}>
      <span>{label}</span>
    </div>
  );
}

/* ── states ───────────────────────────────────────────────────────────────── */

/**
 * Shown at the top of every screen whose content comes from `src/dev/fixtures.ts`. It is
 * deliberately conspicuous: seeded reference content must never be mistaken for live data.
 */
export function FixtureNotice({ owner, what }: { owner: "S2" | "S3" | "S4" | "Admin"; what: string }) {
  return (
    <div className="devbar" role="status">
      <Icon name="alert-triangle" size={14} />
      <span>
        <b>Development preview.</b> {what} is seeded from the UI reference — {owner} has not built this endpoint yet.
      </span>
    </div>
  );
}

/** The honest state a production build shows for a screen whose backend does not exist yet. */
export function NotBuiltYet({ owner, what }: { owner: string; what: string }) {
  return (
    <div className="state-view state-view--unavailable" role="status">
      <strong>Not available yet.</strong>
      <p>
        {what} needs the {owner} API, which has not been built. Nothing is shown here rather than
        showing data that is not real.
      </p>
    </div>
  );
}
