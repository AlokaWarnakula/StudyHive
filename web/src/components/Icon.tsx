/**
 * The reference document draws its icons with `data-lucide="…"`. Rather than pull in a runtime
 * icon package, the 23 glyphs it actually uses are inlined here as Lucide-style paths (24×24,
 * stroke 1.5, currentColor) so the console renders them with nothing fetched at runtime.
 */

export type IconName =
  | "library"
  | "layout-dashboard"
  | "inbox"
  | "file-text"
  | "door-open"
  | "projector"
  | "wrench"
  | "package"
  | "clipboard-list"
  | "truck"
  | "bar-chart-3"
  | "scroll-text"
  | "users"
  | "settings"
  | "graduation-cap"
  | "workflow"
  | "arrow-left"
  | "alert-triangle"
  | "plus"
  | "x"
  | "download"
  | "chevron-left"
  | "chevron-right";

const PATHS: Record<IconName, string> = {
  library: "M16 6 4 8v12l12-2zM8 6V4l12 2v12l-4-.7M4 8l12-2",
  "layout-dashboard": "M3 3h7v9H3zM14 3h7v5h-7zM14 12h7v9h-7zM3 16h7v5H3z",
  inbox: "M22 12h-6l-2 3h-4l-2-3H2M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z",
  "file-text": "M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7zM14 2v6h6M16 13H8M16 17H8M10 9H8",
  "door-open": "M13 4h3a2 2 0 0 1 2 2v14M2 20h3M20 20h2M11 4.7v14.6a1 1 0 0 1-1.2 1l-6-1.3a1 1 0 0 1-.8-1V6a1 1 0 0 1 .8-1l6-1.3a1 1 0 0 1 1.2 1zM10 12v.01",
  projector: "M5 7v10M2 12h1M20 12h2M9 5h6M9 19h6M6 7h12a3 3 0 0 1 3 3v4a3 3 0 0 1-3 3H6a3 3 0 0 1-3-3v-4a3 3 0 0 1 3-3zM15 12h.01",
  wrench: "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94z",
  package: "M16.5 9.4 7.5 4.21M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16zM3.3 7 12 12l8.7-5M12 22V12",
  "clipboard-list": "M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2M9 2h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1zM12 11h4M12 16h4M8 11h.01M8 16h.01",
  truck: "M10 17h4V5H2v12h3M20 17h2v-3.34a4 4 0 0 0-1.17-2.83L19 9h-5v8h1M14 17h1M7.5 17a2.5 2.5 0 1 0 5 0 2.5 2.5 0 1 0-5 0M15.5 17a2.5 2.5 0 1 0 5 0 2.5 2.5 0 1 0-5 0",
  "bar-chart-3": "M3 3v18h18M18 17V9M13 17V5M8 17v-3",
  "scroll-text": "M15 12h-5M15 8h-5M19 17V5a2 2 0 0 0-2-2H4M8 21h12a2 2 0 0 0 2-2v-1a1 1 0 0 0-1-1H5a1 1 0 0 0-1 1v1a2 2 0 0 1-2 2h6z",
  users: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75",
  settings:
    "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.6a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9v0a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z",
  "graduation-cap": "M22 10 12 5 2 10l10 5zM6 12v5c3 3 9 3 12 0v-5",
  workflow: "M3 3h6v6H3zM15 15h6v6h-6zM9 6h6a2 2 0 0 1 2 2v7",
  "arrow-left": "M19 12H5M12 19l-7-7 7-7",
  "alert-triangle":
    "M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0zM12 9v4M12 17h.01",
  plus: "M12 5v14M5 12h14",
  x: "M18 6 6 18M6 6l12 12",
  download: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3",
  "chevron-left": "M15 18l-6-6 6-6",
  "chevron-right": "M9 18l6-6-6-6",
};

interface IconProps {
  name: IconName;
  size?: number;
  className?: string;
}

export function Icon({ name, size = 18, className }: IconProps) {
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.5}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <path d={PATHS[name]} />
    </svg>
  );
}
