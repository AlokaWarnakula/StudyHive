import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../../api/client";
import type { PagedResult, WorkflowStep } from "../../api/approvals";
import type { TagTone } from "../../dev/fixtures";
import { colomboDay, colomboTime } from "../../utils/colomboTime";

/**
 * Shared helpers for the live S4 screens (W-03 … W-09 and the dashboard): money and date
 * formatting, status tones, a small load hook, and a reader for the Validation step's logged
 * proposal (the slots an approval will book and the rule results).
 */

export function formatMoney(amount: number, currency = "LKR"): string {
  const value = amount.toFixed(2);
  return currency === "LKR" ? `Rs. ${value}` : `${currency} ${value}`;
}

/** "Mon 6 Oct, 14:05" in Colombo time (CW-11), whatever the browser's own zone. */
export function formatDateTime(value: string | null | undefined): string {
  return value ? `${colomboDay(value)}, ${colomboTime(value)}` : "—";
}

const ISO_INSTANT = /(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:\d{2}))/g;

/**
 * CW-11: quotation room lines arrive as "Quiet Study 101 2026-10-13T10:00:00+05:30"; show
 * "Quiet Study 101 · Mon 13 Oct, 10:00" instead. Names without a timestamp pass through.
 */
export function formatItemName(name: string): string {
  return name.replace(ISO_INSTANT, (iso) => {
    const parsed = new Date(iso);
    return Number.isNaN(parsed.getTime()) ? iso : `· ${formatDateTime(iso)}`;
  }).replace(/\s+·/g, " ·");
}

/** The quantity column: a Room line counts hours, a Consumable line counts items. */
export function formatQuantity(itemType: "Room" | "Consumable", quantity: number): string {
  return itemType === "Room" ? `${quantity} h` : String(quantity);
}

const GOOD = new Set(["Approved", "Completed", "Confirmed", "Pass"]);
const BAD = new Set(["Rejected", "Failed", "Fail", "RevisionRequested", "Superseded"]);

/** Accent for a good outcome, outline for a bad or waiting one, neutral otherwise. */
export function statusTone(status: string): TagTone {
  if (GOOD.has(status)) return "accent";
  if (BAD.has(status)) return "outline";
  return "neutral";
}

/** Human wording for enum values ("RevisionRequested" → "Revision requested"). */
export function humanize(value: string): string {
  const spaced = value.replace(/([a-z])([A-Z])/g, "$1 $2");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

/** The pagination footer's "Showing 21–40 of 57". */
export function showingRange(page: PagedResult<unknown>): string {
  if (page.totalItems === 0) return "Showing 0 of 0";
  const first = (page.page - 1) * page.pageSize + 1;
  return `Showing ${first}–${first + page.items.length - 1} of ${page.totalItems}`;
}

export function errorText(reason: unknown, fallback: string): string {
  return reason instanceof ApiError ? reason.message : fallback;
}

export interface Loaded<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  reload: () => void;
}

/**
 * Runs `load` whenever `key` changes (and on `reload`), ignoring answers that arrive after the
 * screen moved on. `load` returns null to skip, e.g. before a token exists.
 */
export function useLoad<T>(load: () => Promise<T> | null, key: string, fallbackError: string): Loaded<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  // `load` is a new closure every render; the ref hands the effect the latest one, and `key` is
  // what says the request actually changed.
  const loadRef = useRef(load);
  useEffect(() => {
    loadRef.current = load;
  });

  useEffect(() => {
    const pending = loadRef.current();
    if (!pending) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    pending
      .then((value) => !cancelled && setData(value))
      .catch((reason) => !cancelled && setError(errorText(reason, fallbackError)))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [key, attempt, fallbackError]);

  const reload = useCallback(() => setAttempt((n) => n + 1), []);
  return { data, loading, error, reload };
}

export interface ProposedSlot {
  roomId: string;
  roomName: string;
  startsAt: string;
  endsAt: string;
}

export interface RuleResult {
  rule: string;
  passed: boolean;
  detail: string;
}

export interface ValidationView {
  slots: ProposedSlot[];
  results: RuleResult[];
  valid: boolean | null;
  revisionNote: string | null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * The latest Validation attempt's logged input and output. Its input is the exact proposal the
 * agent priced, so its `proposedSlots` are the slots an approval books. Anything malformed is
 * dropped rather than trusted.
 */
export function readValidation(steps: readonly WorkflowStep[]): ValidationView | null {
  const step = [...steps]
    .filter((s) => s.agentName.toLowerCase() === "validation")
    .sort((a, b) => b.attempt - a.attempt)[0];
  if (!step) return null;

  const input = isRecord(step.input) ? step.input : {};
  const output = isRecord(step.output) ? step.output : {};

  const slots = (Array.isArray(input.proposedSlots) ? input.proposedSlots : [])
    .filter(isRecord)
    .filter((s) => typeof s.startsAt === "string" && typeof s.endsAt === "string")
    .map((s) => ({
      roomId: String(s.roomId ?? ""),
      roomName: typeof s.roomName === "string" ? s.roomName : "Room",
      startsAt: s.startsAt as string,
      endsAt: s.endsAt as string,
    }));

  const results = (Array.isArray(output.results) ? output.results : [])
    .filter(isRecord)
    .filter((r) => typeof r.rule === "string")
    .map((r) => ({ rule: r.rule as string, passed: r.passed === true, detail: typeof r.detail === "string" ? r.detail : "" }));

  return {
    slots,
    results,
    valid: typeof output.valid === "boolean" ? output.valid : null,
    revisionNote: typeof output.revisionNote === "string" ? output.revisionNote : null,
  };
}

/** Pretty JSON for a step payload; strings (non-JSON logs) are shown as they were stored. */
export function formatPayload(value: unknown): string {
  if (value === null || value === undefined) return "—";
  if (typeof value === "string") return value;
  return JSON.stringify(value, null, 2);
}
