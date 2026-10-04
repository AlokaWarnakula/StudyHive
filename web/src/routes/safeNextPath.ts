/** Only an in-app path may be a sign-in destination: "/rooms" yes, "//evil.example" or "https://…" never. */
export function safeNextPath(next: string | null): string {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.includes("\\")) return "/";
  return next;
}
