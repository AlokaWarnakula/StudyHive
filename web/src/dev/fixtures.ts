/**
 * Development-only reference data.
 *
 * S2, S3 and S4 own real APIs that do not exist yet. Rather than show empty tables (which reads as
 * "broken") or invent endpoints (which reads as "done"), the screens those owners will inherit read
 * from this module while it is enabled, and every one of them renders the `<FixtureNotice/>` banner
 * so nobody mistakes seeded content for production data.
 *
 * Every value below is copied from the reference document embedded in
 * UI/StudyHive Web UI (offline).html, so the screens show exactly what the design shows.
 *
 * When the owning student wires up a real endpoint they delete the `useFixture(...)` call on their
 * screen and fetch instead — nothing else on the screen has to change, because the fixture types
 * here are shaped like the responses their API tables describe.
 *
 * See `useFixture.ts` for the gate: outside a development build this data is never returned.
 */

/* ── shared ───────────────────────────────────────────────────────────────── */

export type TagTone = "accent" | "outline" | "neutral";

export interface StatusTag {
  label: string;
  tone: TagTone;
}

/* ── S4 · approvals, costing, workflow, audit, reports ────────────────────── */

export interface ApprovalRow {
  id: string;
  purpose: string;
  student: string;
  studentNote?: string;
  room: string;
  slot: string;
  items: string;
  total: string;
  budget: StatusTag;
  aiChecks: StatusTag;
  waiting: string;
}

export interface QuotationLine {
  no: number;
  description: string;
  source: string;
  qty: string;
  unit: string;
  amount: string;
}

export interface WorkflowStep {
  title: string;
  detail: string;
  state: "done" | "current" | "waiting";
}

export interface WorkflowRun {
  id: string;
  request: string;
  started: string;
  duration: string;
  lastStep: string;
  status: StatusTag;
  result: string;
  action: "open" | "retry";
}

export interface AuditRow {
  when: string;
  user: string;
  role: string;
  action: StatusTag;
  entity: string;
  change: string;
  ip: string;
}

/* ── S2 · rooms, equipment, maintenance ───────────────────────────────────── */

export interface RoomRow {
  code: string;
  location: string;
  seats: number;
  equipment: string;
  rate: string;
  today: string;
  status: StatusTag;
}

export interface EquipmentRow {
  item: string;
  type: string;
  serial: string;
  room: string;
  condition: StatusTag;
  lastChecked: string;
  action: string;
}

export interface MaintenanceRow {
  room: string;
  reason: string;
  from: string;
  to: string;
  bookingsHit: string;
  status: StatusTag;
  action: string;
}

export interface CalendarEntry {
  slot: string;
  day: number; // 0-4, Monday to Friday
  label: string;
  kind: "approved" | "pending" | "maintenance";
}

/* ── S3 · consumables, reservations, suppliers ────────────────────────────── */

export interface ConsumableRow {
  name: string;
  code: string;
  unitPrice: string;
  inStock: string;
  reserved: string;
  free: string;
  reorderAt: string;
  status: StatusTag;
}

export interface LedgerRow {
  when: string;
  type: StatusTag;
  qty: string;
  balance: string;
  reference: string;
  by: string;
}

export interface LowStockRow {
  name: string;
  code: string;
  inStock: string;
  reorderAt: string;
  shortfall: string;
  suggested: string;
  supplier: string;
  leadTime: string;
  urgent: boolean;
}

export interface ReservationRow {
  id: string;
  request: string;
  student: string;
  item: string;
  qty: number;
  heldUntil: string;
  status: StatusTag;
  action: "view" | "issue";
}

export interface SupplierRow {
  name: string;
  contact: string;
  phone: string;
  items: number;
  leadTime: string;
  status: StatusTag;
}

/* ── admin ────────────────────────────────────────────────────────────────── */

export interface UserRow {
  name: string;
  email: string;
  role: StatusTag;
  lastSignIn: string;
  created: string;
  status: StatusTag;
}

/* ── the data ─────────────────────────────────────────────────────────────── */

const accent = (label: string): StatusTag => ({ label, tone: "accent" });
const outline = (label: string): StatusTag => ({ label, tone: "outline" });
const neutral = (label: string): StatusTag => ({ label, tone: "neutral" });

export const fixtures = {
  /** W-02 Dashboard. */
  dashboard: {
    metrics: [
      { label: "Waiting for you", value: "6", note: "Oldest 42 min ago", highlight: true },
      { label: "Bookings today", value: "18", note: "4 checked in", highlight: false },
      { label: "Rooms in use", value: "7 / 12", note: "1 under maintenance", highlight: false },
      { label: "Low stock items", value: "3", note: "HDMI cable at zero", highlight: false },
    ],
    queue: [
      { student: "N. Perera", purpose: "Group project meeting", slot: "B-204 · 2–4 PM", total: "Rs. 720", waiting: "42 min" },
      { student: "A. Silva", purpose: "Presentation practice", slot: "C-301 · 9–11 AM", total: "Rs. 1,250", waiting: "31 min" },
      { student: "K. Jayasuriya", purpose: "Thesis writing", slot: "B-118 · 1–3 PM", total: "Rs. 300", waiting: "19 min" },
      { student: "M. Rathnayake", purpose: "Study group, 8 people", slot: "C-301 · 4–6 PM", total: "Rs. 980", waiting: "12 min" },
      { student: "T. Weeraman", purpose: "Lab report", slot: "B-204 · 6–8 PM", total: "Rs. 450", waiting: "5 min" },
    ],
    attention: [
      { text: "HDMI cable out of stock", tag: accent("Order") },
      { text: "C-301 air conditioning repair", tag: neutral("Today") },
      { text: "2 workflows failed overnight", tag: outline("Review") },
    ],
    budgetUsedPercent: 68,
  },

  /** W-03 Approval queue. */
  approvals: [
    {
      id: "REQ-1042",
      purpose: "Group project meeting",
      student: "N. Perera",
      studentNote: "Year 2 · Computing",
      room: "B-204",
      slot: "Today 2–4 PM",
      items: "2 items",
      total: "Rs. 720",
      budget: accent("Within"),
      aiChecks: accent("All passed"),
      waiting: "42 min",
    },
    {
      id: "REQ-1041",
      purpose: "Presentation practice",
      student: "A. Silva",
      room: "C-301",
      slot: "Tue 9–11 AM",
      items: "4 items",
      total: "Rs. 1,250",
      budget: outline("Over by 250"),
      aiChecks: outline("1 warning"),
      waiting: "31 min",
    },
    {
      id: "REQ-1040",
      purpose: "Thesis writing",
      student: "K. Jayasuriya",
      room: "B-118",
      slot: "Today 1–3 PM",
      items: "None",
      total: "Rs. 300",
      budget: accent("Within"),
      aiChecks: accent("All passed"),
      waiting: "19 min",
    },
    {
      id: "REQ-1039",
      purpose: "Study group, 8 people",
      student: "M. Rathnayake",
      room: "C-301",
      slot: "Today 4–6 PM",
      items: "1 item",
      total: "Rs. 980",
      budget: accent("Within"),
      aiChecks: accent("All passed"),
      waiting: "12 min",
    },
    {
      id: "REQ-1038",
      purpose: "Lab report",
      student: "T. Weeraman",
      room: "B-204",
      slot: "Today 6–8 PM",
      items: "3 items",
      total: "Rs. 450",
      budget: accent("Within"),
      aiChecks: accent("All passed"),
      waiting: "5 min",
    },
    {
      id: "REQ-1037",
      purpose: "Club meeting",
      student: "D. Anuradha",
      room: "B-118",
      slot: "Wed 10–12",
      items: "2 items",
      total: "Rs. 540",
      budget: outline("Over by 40"),
      aiChecks: outline("1 warning"),
      waiting: "2 min",
    },
  ] satisfies ApprovalRow[],

  /** W-04 Review proposal. */
  proposal: {
    requestId: "REQ-1042",
    workflowId: "WF-2291",
    purpose: "Group project meeting",
    quote: "Group project meeting for 4 people, need a whiteboard and a projector",
    student: "N. Perera",
    studentNote: "Year 2 · Computing",
    people: "4",
    when: "Today 2–4 PM",
    budget: "Rs. 1,000",
    room: { code: "B-204", note: "6 seats · whiteboard · projector" },
    clashes: { value: "Confirmed", note: "Checked 12 bookings + maintenance" },
    reserved: { value: "2 of 2", note: "Held for 30 min" },
    lines: [
      { no: 1, description: "B-204, 2 hours", source: "Room", qty: "2 h", unit: "Rs. 150", amount: "Rs. 300" },
      { no: 2, description: "Projector", source: "Equipment", qty: "1", unit: "Rs. 200", amount: "Rs. 200" },
      { no: 3, description: "Whiteboard markers", source: "Consumable", qty: "2", unit: "Rs. 60", amount: "Rs. 120" },
      { no: 4, description: "A4 printouts", source: "Consumable", qty: "20", unit: "Rs. 5", amount: "Rs. 100" },
    ] satisfies QuotationLine[],
    total: "Rs. 720",
    budgetVerdict: "Within budget by Rs. 280",
    checks: [
      { label: "Room free at that time", tag: accent("Pass") },
      { label: "Capacity fits 4 people", tag: accent("Pass") },
      { label: "Stock available", tag: accent("Pass") },
      { label: "Within student budget", tag: accent("Pass") },
      { label: "Weekly booking limit", tag: accent("1 of 3 used") },
      { label: "No outstanding penalties", tag: accent("Pass") },
    ],
  },

  /** W-05 Quotation detail. */
  quotation: {
    id: "QT-0308",
    request: "REQ-1042",
    version: "version 2 of 2",
    student: "N. Perera",
    issued: "24 Aug, 9:13 AM",
    validUntil: "24 Aug, 1:00 PM",
    status: outline("Pending"),
    lines: [
      { no: 1, description: "Room B-204 hire", source: "Rooms", qty: "2 h", unit: "Rs. 150", amount: "Rs. 300" },
      { no: 2, description: "Projector use", source: "Equipment", qty: "1", unit: "Rs. 200", amount: "Rs. 200" },
      { no: 3, description: "Whiteboard markers", source: "Store", qty: "2", unit: "Rs. 60", amount: "Rs. 120" },
      { no: 4, description: "A4 printouts", source: "Store", qty: "20", unit: "Rs. 5", amount: "Rs. 100" },
    ] satisfies QuotationLine[],
    subtotal: "Rs. 720",
    discount: "Rs. 0",
    total: "Rs. 720",
    studentBudget: "Rs. 1,000",
    studentBudgetPercent: 72,
    studentBudgetNote: "72% used · Rs. 280 left",
    departmentBudget: "Rs. 120,000",
    departmentBudgetPercent: 68,
    departmentBudgetNote: "Rs. 81,600 committed",
    versions: [
      { label: "v2", total: "Rs. 720", tag: outline("Current"), note: "Projector added after student comment" },
      { label: "v1", total: "Rs. 520", tag: neutral("Superseded"), note: "9:13 AM · generated by the costing agent" },
    ],
  },

  /** W-06 Workflow execution viewer. */
  workflow: {
    id: "WF-2291",
    request: "REQ-1042",
    finishedIn: "finished in 41 s",
    status: accent("Completed"),
    steps: [
      { title: "1 · Planner agent", detail: "Eligibility passed, 4-step plan created · 3.2 s", state: "done" },
      { title: "2 · Scheduling agent", detail: "3 candidate rooms, B-204 chosen · 11.4 s", state: "done" },
      { title: "3 · Resource agent", detail: "2 items reserved, no oversell · 6.8 s", state: "done" },
      { title: "4 · Validation agent", detail: "6 checks passed, quotation QT-0308 · 19.6 s", state: "done" },
      { title: "5 · Handoff to librarian", detail: "Pending approval since 9:13 AM", state: "current" },
    ] satisfies WorkflowStep[],
    tokensUsed: "4,180",
    toolCalls: "9",
    retries: "0",
    selected: {
      title: "Step 2 · Scheduling agent",
      status: accent("Success"),
      tools: "search_rooms, check_conflicts",
      duration: "11.4 s",
      model: "llama-3.3-70b",
      input: '{ "group_size": 4, "date": "2026-08-24",\n  "time": ["14:00","16:00"],\n  "equipment": ["whiteboard","projector"] }',
      output:
        '{ "candidates": ["B-204","B-118","C-301"],\n  "chosen": "B-204", "conflicts": 0,\n  "reason": "capacity 6, both equipment present, no maintenance" }',
    },
  },

  /** W-07 Execution history. */
  executions: {
    metrics: [
      { label: "Runs", value: "142", highlight: false },
      { label: "Completed", value: "136", highlight: false },
      { label: "Failed", value: "6", highlight: true },
      { label: "Average time", value: "38 s", highlight: false },
    ],
    total: "142",
    // Failed runs are listed first on purpose: this screen exists to surface them.
    runs: [
      {
        id: "WF-2289",
        request: "REQ-1040",
        started: "Today 8:44 AM",
        duration: "18 s",
        lastStep: "Resource",
        status: outline("Failed"),
        result: "Stock service timeout",
        action: "retry",
      },
      {
        id: "WF-2287",
        request: "REQ-1038",
        started: "Yesterday 6:12 PM",
        duration: "29 s",
        lastStep: "Scheduling",
        status: outline("Failed"),
        result: "No room fits 12 people",
        action: "open",
      },
      {
        id: "WF-2291",
        request: "REQ-1042",
        started: "Today 9:12 AM",
        duration: "41 s",
        lastStep: "Validation",
        status: accent("Completed"),
        result: "QT-0308 · Rs. 720",
        action: "open",
      },
      {
        id: "WF-2290",
        request: "REQ-1041",
        started: "Today 9:01 AM",
        duration: "52 s",
        lastStep: "Validation",
        status: accent("Completed"),
        result: "QT-0307 · Rs. 1,250",
        action: "open",
      },
      {
        id: "WF-2288",
        request: "REQ-1039",
        started: "Today 8:20 AM",
        duration: "36 s",
        lastStep: "Validation",
        status: accent("Completed"),
        result: "QT-0306 · Rs. 980",
        action: "open",
      },
      {
        id: "WF-2286",
        request: "REQ-1037",
        started: "Yesterday 4:03 PM",
        duration: "44 s",
        lastStep: "Validation",
        status: accent("Completed"),
        result: "QT-0305 · Rs. 540",
        action: "open",
      },
    ] satisfies WorkflowRun[],
  },

  /** W-08 Audit log. */
  audit: {
    total: "3,914",
    rows: [
      {
        when: "9:41:02 AM",
        user: "S. Fernando",
        role: "Librarian",
        action: accent("Approve"),
        entity: "booking_request REQ-1042",
        change: "PendingApproval → Approved",
        ip: "10.2.4.18",
      },
      {
        when: "9:41:02 AM",
        user: "system",
        role: "Service",
        action: neutral("Reserve"),
        entity: "stock_reservation SR-881",
        change: "Held → Confirmed",
        ip: "internal",
      },
      {
        when: "9:41:02 AM",
        user: "system",
        role: "Service",
        action: neutral("Lock"),
        entity: "room_booking RB-1190",
        change: "— → Confirmed",
        ip: "internal",
      },
      {
        when: "9:13:44 AM",
        user: "validation-agent",
        role: "Agent",
        action: neutral("Create"),
        entity: "quotation QT-0308",
        change: "— → Rs. 720",
        ip: "internal",
      },
      {
        when: "9:12:10 AM",
        user: "N. Perera",
        role: "Student",
        action: neutral("Submit"),
        entity: "booking_request REQ-1042",
        change: "Draft → Submitted",
        ip: "103.21.7.9",
      },
      {
        when: "8:58:31 AM",
        user: "R. Costa",
        role: "Store officer",
        action: neutral("Stock in"),
        entity: "consumable CN-04",
        change: "18 → 42",
        ip: "10.2.4.31",
      },
      {
        when: "8:44:02 AM",
        user: "admin",
        role: "Admin",
        action: outline("Update role"),
        entity: "user U-0032",
        change: "Student → Store officer",
        ip: "10.2.4.2",
      },
    ] satisfies AuditRow[],
  },

  /** W-09 Reports. */
  reports: {
    weeklyBars: [35, 52, 88, 64, 41, 73, 58, 30],
    weeklyPeakIndex: 2,
    byStatus: [
      { status: "Approved", count: "96", share: "68%", change: "+12%" },
      { status: "Pending", count: "6", share: "4%", change: "−3%" },
      { status: "Rejected", count: "14", share: "10%", change: "+1%" },
      { status: "Completed", count: "82", share: "58%", change: "+9%" },
      { status: "Failed", count: "6", share: "4%", change: "+2%" },
    ],
    busiestRoom: { value: "B-204", note: "74% utilised · 61 bookings" },
    mostUsedItem: { value: "A4 printouts", note: "1,840 sheets · Rs. 9,200" },
    committed: "Rs. 81,600",
    budget: "Rs. 120,000",
    left: "Rs. 38,400",
    budgetPercent: 68,
  },

  /** W-13 Rooms. */
  rooms: [
    {
      code: "B-204",
      location: "Main library · 2",
      seats: 6,
      equipment: "Whiteboard, projector",
      rate: "Rs. 150",
      today: "4 bookings",
      status: accent("Available"),
    },
    {
      code: "B-118",
      location: "Main library · 1",
      seats: 4,
      equipment: "Whiteboard",
      rate: "Rs. 100",
      today: "3 bookings",
      status: accent("Available"),
    },
    {
      code: "C-301",
      location: "New wing · 3",
      seats: 10,
      equipment: "Projector, TV",
      rate: "Rs. 250",
      today: "2 bookings",
      status: outline("Maintenance 4–6 PM"),
    },
    {
      code: "C-105",
      location: "New wing · 1",
      seats: 2,
      equipment: "—",
      rate: "Rs. 80",
      today: "6 bookings",
      status: accent("Available"),
    },
    {
      code: "B-020",
      location: "Main library · G",
      seats: 16,
      equipment: "Projector, sound",
      rate: "Rs. 400",
      today: "1 booking",
      status: neutral("Closed"),
    },
  ] satisfies RoomRow[],

  /** W-14 Room detail. */
  roomDetail: {
    code: "B-204",
    facts: [
      { label: "Building · floor", value: "Main library · 2" },
      { label: "Seats", value: "6" },
      { label: "Rate", value: "Rs. 150 / hour" },
      { label: "Opening hours", value: "8 AM – 8 PM" },
      { label: "Utilisation this month", value: "74%" },
    ],
    status: accent("Available"),
    equipment: [
      { item: "Whiteboard", serial: "WB-2041", condition: accent("Working"), lastChecked: "12 Aug" },
      { item: "Projector", serial: "PJ-0087", condition: accent("Working"), lastChecked: "18 Aug" },
      { item: "Air conditioner", serial: "AC-1120", condition: outline("Under repair"), lastChecked: "24 Aug" },
    ],
    upcoming: [
      { when: "Today 2–4 PM", student: "N. Perera", people: 4, status: outline("Pending") },
      { when: "Today 6–8 PM", student: "T. Weeraman", people: 3, status: accent("Approved") },
      { when: "Tue 10–12", student: "A. Silva", people: 5, status: accent("Approved") },
      { when: "Wed 1–3 PM", student: "P. Kumara", people: 2, status: accent("Approved") },
    ],
  },

  /** W-15 Room calendar. */
  calendar: {
    range: "24 – 28 August 2026",
    days: ["Mon 24", "Tue 25", "Wed 26", "Thu 27", "Fri 28"],
    slots: ["8 – 10 AM", "10 – 12 PM", "12 – 2 PM", "2 – 4 PM", "4 – 6 PM", "6 – 8 PM"],
    entries: [
      { slot: "8 – 10 AM", day: 1, label: "C-301 · A. Silva", kind: "approved" },
      { slot: "8 – 10 AM", day: 3, label: "B-118 · S. Dias", kind: "approved" },
      { slot: "10 – 12 PM", day: 0, label: "B-204 · K. Silva", kind: "approved" },
      { slot: "10 – 12 PM", day: 1, label: "B-204 · pending", kind: "pending" },
      { slot: "10 – 12 PM", day: 2, label: "C-105 · P. Kumara", kind: "approved" },
      { slot: "12 – 2 PM", day: 2, label: "B-118 · D. Anuradha", kind: "approved" },
      { slot: "2 – 4 PM", day: 0, label: "B-204 · N. Perera · pending", kind: "pending" },
      { slot: "2 – 4 PM", day: 3, label: "C-301 · M. Rathnayake", kind: "approved" },
      { slot: "4 – 6 PM", day: 0, label: "C-301 · AC repair", kind: "maintenance" },
      { slot: "4 – 6 PM", day: 2, label: "B-020 · club", kind: "approved" },
      { slot: "6 – 8 PM", day: 0, label: "B-204 · T. Weeraman", kind: "approved" },
    ] satisfies CalendarEntry[],
  },

  /** W-16 Equipment. */
  equipment: {
    metrics: [
      { label: "Total items", value: "38", highlight: false },
      { label: "Working", value: "33", highlight: false },
      { label: "Under repair", value: "3", highlight: false },
      { label: "Retired", value: "2", highlight: false },
    ],
    total: "38",
    rows: [
      { item: "Projector", type: "Projector", serial: "PJ-0087", room: "B-204", condition: accent("Working"), lastChecked: "18 Aug", action: "Edit" },
      { item: "Air conditioner", type: "Comfort", serial: "AC-1120", room: "B-204", condition: outline("Under repair"), lastChecked: "24 Aug", action: "Edit" },
      { item: 'Smart TV 55"', type: "Display", serial: "TV-0033", room: "C-301", condition: accent("Working"), lastChecked: "10 Aug", action: "Edit" },
      { item: "Whiteboard", type: "Writing", serial: "WB-2041", room: "B-204", condition: accent("Working"), lastChecked: "12 Aug", action: "Edit" },
      { item: "Sound system", type: "Audio", serial: "SS-0009", room: "B-020", condition: neutral("Retired"), lastChecked: "2 Jul", action: "Edit" },
      { item: "Projector", type: "Projector", serial: "PJ-0091", room: "Unassigned", condition: accent("Working"), lastChecked: "21 Aug", action: "Assign" },
    ] satisfies EquipmentRow[],
  },

  /** W-17 Maintenance windows. */
  maintenance: {
    total: "5",
    rows: [
      { room: "C-301", reason: "Air conditioning repair", from: "Today 4:00 PM", to: "Today 6:00 PM", bookingsHit: "1 moved", status: accent("Active"), action: "Edit" },
      { room: "B-020", reason: "Projector replacement", from: "25 Aug 8:00 AM", to: "25 Aug 12:00 PM", bookingsHit: "0", status: outline("Planned"), action: "Edit" },
      { room: "B-118", reason: "Repainting", from: "28 Aug 8:00 AM", to: "29 Aug 6:00 PM", bookingsHit: "2 to move", status: outline("Planned"), action: "Edit" },
      { room: "C-105", reason: "Furniture swap", from: "18 Aug", to: "18 Aug", bookingsHit: "0", status: neutral("Finished"), action: "View" },
    ] satisfies MaintenanceRow[],
    clashWarning: "1 approved booking falls inside this window. Saving will notify the student and mark the booking for rescheduling.",
  },

  /** W-18 Room utilisation report. */
  roomUsage: {
    metrics: [
      { label: "Average utilisation", value: "61%", note: "+7% on July", highlight: false },
      { label: "Busiest hour", value: "2 – 4 PM", note: "83% of rooms full", highlight: false },
      { label: "Hours booked", value: "742", note: "", highlight: false },
      { label: "No-shows", value: "11", note: "no QR check-in", highlight: false },
    ],
    byRoom: [
      { room: "B-204", percent: 74 },
      { room: "C-105", percent: 69 },
      { room: "B-118", percent: 58 },
      { room: "C-301", percent: 47 },
      { room: "B-020", percent: 22 },
    ],
    byHour: [18, 32, 54, 47, 92, 71, 44, 26],
    byHourPeakIndex: 4,
    hourLabels: ["8", "10", "12", "2", "4", "6", "8 PM"],
  },

  /** W-19 Consumables. */
  consumables: [
    { name: "HDMI cable", code: "CN-11", unitPrice: "Rs. 350", inStock: "0", reserved: "0", free: "0", reorderAt: "5", status: outline("Out of stock") },
    { name: "Whiteboard eraser", code: "CN-07", unitPrice: "Rs. 120", inStock: "4", reserved: "2", free: "2", reorderAt: "10", status: outline("Low") },
    { name: "Flip chart paper", code: "CN-09", unitPrice: "Rs. 90", inStock: "8", reserved: "0", free: "8", reorderAt: "15", status: outline("Low") },
    { name: "Whiteboard markers", code: "CN-04", unitPrice: "Rs. 60", inStock: "42", reserved: "6", free: "36", reorderAt: "20", status: accent("Healthy") },
    { name: "A4 printouts", code: "CN-01", unitPrice: "Rs. 5", inStock: "1,200", reserved: "120", free: "1,080", reorderAt: "300", status: accent("Healthy") },
    { name: "Sticky notes", code: "CN-15", unitPrice: "Rs. 200", inStock: "26", reserved: "4", free: "22", reorderAt: "10", status: accent("Healthy") },
  ] satisfies ConsumableRow[],

  /** W-20 Consumable detail + ledger. */
  consumableDetail: {
    name: "Whiteboard markers",
    code: "CN-04",
    facts: [
      { label: "Code", value: "CN-04" },
      { label: "Unit price", value: "Rs. 60" },
      { label: "In stock", value: "42" },
      { label: "Reserved", value: "6" },
      { label: "Free to reserve", value: "36" },
      { label: "Reorder level", value: "20" },
      { label: "Supplier", value: "Lanka Stationers" },
    ],
    status: accent("Healthy"),
    stockPercent: 70,
    stockNote: "42 of 60 shelf capacity · reorder at 20",
    ledgerTotal: "88",
    ledger: [
      { when: "Today 9:13 AM", type: outline("Reserved"), qty: "−2", balance: "36 free", reference: "REQ-1042", by: "resource-agent" },
      { when: "Today 8:58 AM", type: accent("Stock in"), qty: "+24", balance: "42", reference: "PO-2211", by: "R. Costa" },
      { when: "Yesterday 5:20 PM", type: neutral("Issued"), qty: "−4", balance: "18", reference: "REQ-1031", by: "R. Costa" },
      { when: "Yesterday 2:02 PM", type: outline("Reserved"), qty: "−4", balance: "22 free", reference: "REQ-1031", by: "resource-agent" },
      { when: "22 Aug", type: neutral("Released"), qty: "+2", balance: "26", reference: "REQ-1029 rejected", by: "system" },
      { when: "21 Aug", type: neutral("Issued"), qty: "−6", balance: "24", reference: "REQ-1025", by: "R. Costa" },
    ] satisfies LedgerRow[],
  },

  /** W-21 Low stock. */
  lowStock: {
    metrics: [
      { label: "Out of stock", value: "1", note: "HDMI cable · 2 requests waiting", highlight: true },
      { label: "Below reorder level", value: "2", note: "Eraser, flip chart paper", highlight: false },
      { label: "Value to reorder", value: "Rs. 6,150", note: "Across 2 suppliers", highlight: false },
    ],
    rows: [
      { name: "HDMI cable", code: "CN-11", inStock: "0", reorderAt: "5", shortfall: "5", suggested: "10 units · Rs. 3,500", supplier: "TechLine Colombo", leadTime: "3 days", urgent: true },
      { name: "Whiteboard eraser", code: "CN-07", inStock: "4", reorderAt: "10", shortfall: "6", suggested: "12 units · Rs. 1,440", supplier: "Lanka Stationers", leadTime: "1 day", urgent: false },
      { name: "Flip chart paper", code: "CN-09", inStock: "8", reorderAt: "15", shortfall: "7", suggested: "15 units · Rs. 1,350", supplier: "Lanka Stationers", leadTime: "1 day", urgent: false },
    ] satisfies LowStockRow[],
    blocked: [
      "REQ-1043 · P. Kumara · needs 1 HDMI cable",
      "REQ-1044 · S. Dias · needs 2 HDMI cables",
    ],
  },

  /** W-22 Stock reservations. */
  reservations: {
    open: "14",
    counts: [accent("Held 4"), outline("Confirmed 6"), outline("Issued 3"), outline("Released 1")],
    rows: [
      { id: "SR-0881", request: "REQ-1042", student: "N. Perera", item: "Whiteboard markers", qty: 2, heldUntil: "Today 9:43 AM", status: accent("Held"), action: "view" },
      { id: "SR-0882", request: "REQ-1042", student: "N. Perera", item: "A4 printouts", qty: 20, heldUntil: "Today 9:43 AM", status: accent("Held"), action: "view" },
      { id: "SR-0879", request: "REQ-1039", student: "M. Rathnayake", item: "Sticky notes", qty: 4, heldUntil: "—", status: outline("Confirmed"), action: "issue" },
      { id: "SR-0877", request: "REQ-1038", student: "T. Weeraman", item: "A4 printouts", qty: 100, heldUntil: "—", status: outline("Confirmed"), action: "issue" },
      { id: "SR-0870", request: "REQ-1031", student: "A. Silva", item: "Whiteboard markers", qty: 4, heldUntil: "—", status: neutral("Issued"), action: "view" },
      { id: "SR-0866", request: "REQ-1029", student: "D. Anuradha", item: "Whiteboard markers", qty: 2, heldUntil: "—", status: neutral("Released"), action: "view" },
    ] satisfies ReservationRow[],
  },

  /** W-23 Suppliers. */
  suppliers: {
    total: "5",
    rows: [
      { name: "Lanka Stationers", contact: "K. Bandara", phone: "011 234 5678", items: 12, leadTime: "1 day", status: accent("Active") },
      { name: "TechLine Colombo", contact: "S. Nawaz", phone: "011 987 6543", items: 6, leadTime: "3 days", status: accent("Active") },
      { name: "Paper World", contact: "M. Gunasekara", phone: "011 445 1122", items: 3, leadTime: "2 days", status: accent("Active") },
      { name: "Office Plus", contact: "R. Fonseka", phone: "011 332 8899", items: 2, leadTime: "5 days", status: neutral("Inactive") },
    ] satisfies SupplierRow[],
    selectedItems: [
      { name: "Whiteboard markers", price: "Rs. 58" },
      { name: "Whiteboard eraser", price: "Rs. 120" },
      { name: "Flip chart paper", price: "Rs. 90" },
      { name: "Sticky notes", price: "Rs. 200" },
    ],
    selectedEmail: "orders@lankastationers.lk",
  },

  /** W-24 Consumable usage report. */
  consumableUsage: {
    metrics: [
      { label: "Items issued", value: "2,410", note: "", highlight: false },
      { label: "Cost of items", value: "Rs. 24,180", note: "", highlight: false },
      { label: "Released unused", value: "86", note: "rejected or cancelled", highlight: false },
      { label: "Times out of stock", value: "4", note: "", highlight: false },
    ],
    byItem: [
      { item: "A4 printouts", issued: "1,840", cost: "Rs. 9,200", reservedNow: "120", outOfStock: "0 times" },
      { item: "Whiteboard markers", issued: "288", cost: "Rs. 17,280", reservedNow: "6", outOfStock: "1 time" },
      { item: "Sticky notes", issued: "112", cost: "Rs. 22,400", reservedNow: "4", outOfStock: "0 times" },
      { item: "Flip chart paper", issued: "96", cost: "Rs. 8,640", reservedNow: "0", outOfStock: "2 times" },
      { item: "HDMI cable", issued: "44", cost: "Rs. 15,400", reservedNow: "0", outOfStock: "4 times" },
    ],
    perWeek: [44, 62, 88, 57],
    perWeekPeakIndex: 2,
    busiestDay: "Wednesday",
    averagePerBooking: "17 items",
  },

  /** W-25 Users & roles. */
  users: {
    metrics: [
      { label: "Students", value: "318", highlight: false },
      { label: "Librarians", value: "3", highlight: false },
      { label: "Store officers", value: "2", highlight: false },
      { label: "Admins", value: "1", highlight: false },
    ],
    total: "324",
    rows: [
      { name: "S. Fernando", email: "s.fernando@sliit.lk", role: accent("Librarian"), lastSignIn: "Today 8:40 AM", created: "10 Jan 2025", status: accent("Active") },
      { name: "R. Costa", email: "r.costa@sliit.lk", role: accent("Store officer"), lastSignIn: "Today 8:12 AM", created: "10 Jan 2025", status: accent("Active") },
      { name: "N. Perera", email: "it21234@my.sliit.lk", role: neutral("Student"), lastSignIn: "Today 9:08 AM", created: "12 Jan 2025", status: accent("Active") },
      { name: "D. Anuradha", email: "it19555@my.sliit.lk", role: neutral("Student"), lastSignIn: "21 Aug", created: "3 Mar 2024", status: outline("Suspended") },
      { name: "admin", email: "admin@sliit.lk", role: outline("Admin"), lastSignIn: "Yesterday", created: "1 Jan 2025", status: accent("Active") },
    ] satisfies UserRow[],
    roleChoices: [
      { value: "Student", label: "Student — phone app only" },
      { value: "Librarian", label: "Librarian — approvals, rooms, reports" },
      { value: "StoreOfficer", label: "Store officer — consumables, stock, suppliers" },
      { value: "Admin", label: "Admin — everything, including users" },
    ],
  },

  /** W-26 Settings. */
  settings: {
    booking: {
      perWeek: "3",
      longestHours: "4",
      aheadDays: "14",
      holdMinutes: "30",
      requireQr: true,
      cancelNoShow: true,
    },
    email: {
      senderName: "StudyHive Library",
      senderAddress: "no-reply@studyhive.lk",
      onDecision: true,
      onLowStock: true,
      dailySummary: false,
      connection: accent("Working · tested 8:00 AM"),
    },
    pricing: {
      roomRate: "150",
      projector: "200",
      soundSystem: "300",
      monthlyBudget: "120000",
    },
    agent: {
      url: "https://studyhive-agents.internal/api",
      model: "llama-3.3-70b (Groq)",
      timeoutSeconds: "45",
      health: [
        { label: "Planner agent", tag: accent("Healthy") },
        { label: "Scheduling agent", tag: accent("Healthy") },
        { label: "Resource agent", tag: outline("1 timeout today") },
        { label: "Validation agent", tag: accent("Healthy") },
      ],
    },
    hours: {
      weekdayOpen: "8:00 AM",
      weekdayClose: "8:00 PM",
      saturdayOpen: "9:00 AM",
      saturdayClose: "4:00 PM",
      closedSundays: true,
    },
  },

  /**
   * Toolbar option lists and prefilled form values the reference draws.
   *
   * They live here rather than inside the screens so that no seeded name, room code or purchase
   * order exists outside this development-only module.
   */
  filters: {
    rooms: ["All", "B-204", "B-118", "C-301", "C-105", "B-020"],
    buildings: ["New wing", "Main library"],
    equipmentTypes: ["All", "Projector", "Display", "Writing", "Audio", "Comfort"],
    equipmentRooms: ["All", "B-204", "B-118", "C-301", "Unassigned"],
    suppliers: ["All", "Lanka Stationers", "TechLine Colombo", "Paper World"],
    consumableItems: ["All", "Whiteboard markers", "A4 printouts", "Sticky notes"],
    auditUsers: ["All", "S. Fernando", "R. Costa", "system"],
    auditEntities: ["All", "booking_request", "quotation", "stock_reservation", "user"],
    auditRange: "18 Aug – 24 Aug",
    reportMonth: "August 2026",
  },

  forms: {
    newRoom: { code: "C-402", seats: "8", floor: "4", rate: "200" },
    stockIn: {
      quantity: "24",
      unitCost: "58",
      purchaseOrder: "PO-2211",
      suppliers: ["Lanka Stationers", "Paper World"],
      /** Balance before this stock-in, so the dialog can show a live "new balance". */
      balanceBefore: 18,
    },
    maintenanceWindow: {
      rooms: ["C-301 — New wing, floor 3", "B-204 — Main library, floor 2"],
      reason: "Air conditioning repair",
      from: "24 Aug, 4:00 PM",
      to: "24 Aug, 6:00 PM",
      notes: "Technician arriving at 3:45 PM.",
    },
  },
} as const;

export type Fixtures = typeof fixtures;
