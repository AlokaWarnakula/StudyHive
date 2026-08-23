/** Booking-request status → the reference's three tag tones and its spaced labels. */

export function statusTone(status: string): "accent" | "outline" | "neutral" {
  if (status === "Approved" || status === "Completed") return "accent";
  if (status === "PendingApproval" || status === "Failed" || status === "RevisionRequested") return "outline";
  return "neutral";
}

/** "PendingApproval" reads as "Pending approval" in the reference. */
export function statusLabel(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, "$1 $2");
}
