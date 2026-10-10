import type { ReactNode, SVGProps } from "react"

type IconName = "activity" | "alert" | "arrow-left" | "arrow-right" | "banknote" | "building" | "calendar" | "car" | "chart" | "check" | "check-circle" | "chevron-down" | "clipboard" | "clock" | "credit-card" | "database" | "file" | "graduation-cap" | "grid" | "help" | "house" | "leaf" | "life-buoy" | "link" | "lock" | "log-out" | "mail" | "map" | "map-pin" | "menu" | "phone" | "globe" | "motorcycle" | "parking" | "search" | "settings" | "shield" | "sparkles" | "target" | "ticket" | "users" | "wallet" | "wrench" | "x" | "plus" | "youtube" | "zap" | "tag" | "flame" | "star" | "shopping-bag" | "percent" | "award"

const paths: Record<IconName, ReactNode> = {
  activity: (
    <>
      <path d="M22 12h-4l-3 9L9 3l-3 9H2" />
    </>
  ),

  alert: (
    <>
      <path d="M10.3 3.9 2.4 17.5A2 2 0 0 0 4.1 20h15.8a2 2 0 0 0 1.7-2.5L13.7 3.9a2 2 0 0 0-3.4 0Z" />
      <path d="M12 9v4m0 4h.01" />
    </>
  ),

  "arrow-left": (
    <>
      <path d="m12 19-7-7 7-7" />
      <path d="M19 12H5" />
    </>
  ),

  "arrow-right": (
    <>
      <path d="M5 12h14" />
      <path d="m12 5 7 7-7 7" />
    </>
  ),

  award: (
    <>
      <circle cx="12" cy="8" r="6" />
      <path d="M15.4 12.8 17 22l-5-3-5 3 1.6-9.2" />
    </>
  ),

  banknote: (
    <>
      <rect x="2" y="5" width="20" height="14" rx="2" />
      <path d="M6 9a2 2 0 0 1-2 2m14-2a2 2 0 0 0 2 2m-14 2a2 2 0 0 1-2 2m14-2a2 2 0 0 0 2 2M14 12a2 2 0 1 1-4 0 2 2 0 0 1 4 0Z" />
    </>
  ),

  building: (
    <>
      <rect x="4" y="2" width="16" height="20" rx="2" />
      <path d="M9 22v-4h6v4M8 6h.01M12 6h.01M16 6h.01M8 10h.01M12 10h.01M16 10h.01M8 14h.01M12 14h.01M16 14h.01" />
    </>
  ),

  calendar: (
    <>
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M16 3v4M8 3v4M3 11h18" />
    </>
  ),

  car: (
    <>
      <path d="m5 11 1.5-5h11L19 11" />
      <path d="M3 11h18v7H3zM6 18v2m12-2v2M6 14h.01M18 14h.01" />
    </>
  ),

  chart: (
    <>
      <path d="M3 3v18h18" />
      <path d="m19 9-5 5-4-4-5 5" />
    </>
  ),

  check: (
    <>
      <path d="m5 12 4 4L19 6" />
    </>
  ),

  "check-circle": (
    <>
      <circle cx="12" cy="12" r="10" />
      <path d="m8 12 3 3 5-6" />
    </>
  ),

  "chevron-down": (
    <>
      <path d="m6 9 6 6 6-6" />
    </>
  ),

  clipboard: (
    <>
      <rect x="5" y="4" width="14" height="18" rx="2" />
      <path d="M9 4.5h6a2 2 0 0 0-2-2h-2a2 2 0 0 0-2 2ZM9 12h6m-6 4h6" />
    </>
  ),

  clock: (
    <>
      <circle cx="12" cy="12" r="10" />
      <path d="M12 6v6l4 2" />
    </>
  ),

  "credit-card": (
    <>
      <rect x="2" y="5" width="20" height="14" rx="2" />
      <path d="M2 10h20M6 15h3" />
    </>
  ),

  database: (
    <>
      <ellipse cx="12" cy="5" rx="9" ry="3" />
      <path d="M3 5v14c0 1.7 4 3 9 3s9-1.3 9-3V5M3 12c0 1.7 4 3 9 3s9-1.3 9-3" />
    </>
  ),

  file: (
    <>
      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
      <path d="M14 2v6h6M8 13h8m-8 4h8" />
    </>
  ),

  flame: (
    <>
      <path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.1-2.1-.2-4.1 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.2.4-2.3 1-3a2.5 2.5 0 0 0 2.5 2.5z" />
    </>
  ),

  globe: (
    <>
      <circle cx="12" cy="12" r="10" />
      <path d="M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10Z" />
    </>
  ),

  "graduation-cap": (
    <>
      <path d="m2 10 10-5 10 5-10 5-10-5Z" />
      <path d="M6 12v5c3.5 3 8.5 3 12 0v-5m4-2v6" />
    </>
  ),

  grid: (
    <>
      <rect x="3" y="3" width="8" height="8" rx="1" />
      <rect x="13" y="3" width="8" height="8" rx="1" />
      <rect x="3" y="13" width="8" height="8" rx="1" />
      <rect x="13" y="13" width="8" height="8" rx="1" />
    </>
  ),

  help: (
    <>
      <circle cx="12" cy="12" r="10" />
      <path d="M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3m.1 4h.01" />
    </>
  ),

  house: (
    <>
      <path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-6v-7h-4v7H4a1 1 0 0 1-1-1z" />
    </>
  ),

  leaf: (
    <>
      <path d="M20 4c-9 0-15 4-15 11a5 5 0 0 0 5 5c7 0 11-7 10-16Z" />
      <path d="M4 21c2-5 6-8 11-11" />
    </>
  ),

  "life-buoy": (
    <>
      <circle cx="12" cy="12" r="10" />
      <circle cx="12" cy="12" r="4" />
      <path d="m4.9 4.9 4.3 4.3m5.6 5.6 4.3 4.3m0-14.2-4.3 4.3m-5.6 5.6-4.3 4.3" />
    </>
  ),

  link: (
    <>
      <path d="M10 13a5 5 0 0 0 7.1 0l3-3A5 5 0 0 0 13 2.9l-1.7 1.7" />
      <path d="M14 11a5 5 0 0 0-7.1 0l-3 3A5 5 0 0 0 11 21.1l1.7-1.7" />
    </>
  ),

  lock: (
    <>
      <rect x="4" y="10" width="16" height="12" rx="2" />
      <path d="M8 10V7a4 4 0 1 1 8 0v3m-4 5v2" />
    </>
  ),

  "log-out": (
    <>
      <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
      <path d="m16 17 5-5-5-5m5 5H9" />
    </>
  ),

  mail: (
    <>
      <rect x="2" y="4" width="20" height="16" rx="2" />
      <path d="m22 7-9 6a2 2 0 0 1-2 0L2 7" />
    </>
  ),

  map: (
    <>
      <path d="m3 6 6-3 6 3 6-3v15l-6 3-6-3-6 3z" />
      <path d="M9 3v15m6-12v15" />
    </>
  ),

  "map-pin": (
    <>
      <path d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z" />
      <circle cx="12" cy="10" r="2.5" />
    </>
  ),

  menu: (
    <>
      <path d="M4 6h16M4 12h16M4 18h16" />
    </>
  ),

  motorcycle: (
    <>
      <circle cx="5.5" cy="17.5" r="3.5" />
      <circle cx="18.5" cy="17.5" r="3.5" />
      <path d="M9 17.5h4l-3-7h4l3 7m-9-7H5l-2 4m11-7h3l2 3h2" />
    </>
  ),

  percent: (
    <>
      <line x1="19" y1="5" x2="5" y2="19" />
      <circle cx="6.5" cy="6.5" r="2.5" />
      <circle cx="17.5" cy="17.5" r="2.5" />
    </>
  ),

  phone: (
    <>
      <path d="M22 16.9v3a2 2 0 0 1-2.2 2A19.8 19.8 0 0 1 3.1 5.2 2 2 0 0 1 5.1 3h3a2 2 0 0 1 2 1.7l.5 2.8a2 2 0 0 1-.6 1.7L8.2 11a16 16 0 0 0 4.8 4.8l1.8-1.8a2 2 0 0 1 1.7-.6l2.8.5a2 2 0 0 1 1.7 2Z" />
    </>
  ),

  plus: (
    <>
      <path d="M12 5v14M5 12h14" />
    </>
  ),

  parking: (
    <>
      <path d="M7 21V3h6a6 6 0 0 1 0 12H7" />
    </>
  ),

  search: (
    <>
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-4-4" />
    </>
  ),

  settings: (
    <>
      <path d="M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z" />
      <path d="m19.4 15 .1.1 1.4 1.1-1.4 2.4-1.8-.6a8 8 0 0 1-1.7 1l-.3 1.9h-2.8l-.3-1.9a8 8 0 0 1-1.7-1l-1.8.6-1.4-2.4 1.4-1.1a8 8 0 0 1 0-2l-1.4-1.1 1.4-2.4 1.8.6a8 8 0 0 1 1.7-1l.3-1.9h2.8l.3 1.9a8 8 0 0 1 1.7 1l1.8-.6 1.4 2.4-1.4 1.1a8 8 0 0 1-.1 1.9Z" />
    </>
  ),

  shield: (
    <>
      <path d="M12 22s8-4 8-11V5l-8-3-8 3v6c0 7 8 11 8 11Z" />
      <path d="m9 12 2 2 4-4" />
    </>
  ),

  "shopping-bag": (
    <>
      <path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z" />
      <path d="M3 6h18" />
      <path d="M16 10a4 4 0 0 1-8 0" />
    </>
  ),

  sparkles: (
    <>
      <path d="m12 3 1.9 5.8L20 11l-6.1 2.2L12 19l-2-5.8L4 11l6-2.2L12 3Z" />
      <path d="m19 14 1 3 3 1-3 1-1 3-1-3-3-1 3-1 1-3ZM5 2l.8 2.2L8 5l-2.2.8L5 8l-.8-2.2L2 5l2.2-.8L5 2Z" />
    </>
  ),

  star: (
    <>
      <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />
    </>
  ),

  tag: (
    <>
      <path d="M20.59 13.41 13.42 20.58a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82Z" />
      <line x1="7" y1="7" x2="7.01" y2="7" />
    </>
  ),

  target: (
    <>
      <circle cx="12" cy="12" r="10" />
      <circle cx="12" cy="12" r="6" />
      <circle cx="12" cy="12" r="2" />
    </>
  ),

  ticket: (
    <>
      <path d="M2 9a3 3 0 0 0 0 6v4h20v-4a3 3 0 0 1 0-6V5H2z" />
      <path d="M13 5v2m0 4v2m0 4v2" />
    </>
  ),

  users: (
    <>
      <path d="M16 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
      <circle cx="10" cy="7" r="4" />
      <path d="M20 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8" />
    </>
  ),

  wallet: (
    <>
      <rect x="3" y="5" width="18" height="15" rx="2" />
      <path d="M3 8h15a3 3 0 0 1 3 3v2h-5a2 2 0 0 1 0-4h5M17 12h.01" />
    </>
  ),

  wrench: (
    <>
      <path d="M14.7 6.3a5 5 0 0 0-6.4 6.4L3 18l3 3 5.3-5.3a5 5 0 0 0 6.4-6.4L15 12l-3-3 2.7-2.7Z" />
    </>
  ),

  x: (
    <>
      <path d="m18 6-12 12M6 6l12 12" />
    </>
  ),

  youtube: (
    <>
      <rect x="2" y="5" width="20" height="14" rx="4" />
      <path d="m10 9 5 3-5 3z" />
    </>
  ),

  zap: (
    <>
      <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
    </>
  ),
}

const aliases: Record<string, IconName> = {
  "🔍": "search",
  "🅿️": "parking",
  "📋": "clipboard",
  "💳": "credit-card",
  "✅": "check-circle",

  "🚗": "car",
  "⚡": "zap",
  "🔒": "lock",
  "📈": "chart",
  "🤖": "sparkles",
  "🗺️": "map",

  "🎫": "ticket",
  "🎟️": "ticket",
  "🛟": "life-buoy",
  "🎯": "target",
  "🌱": "leaf",
  "🌳": "leaf",
  "🏥": "activity",

  "🎓": "graduation-cap",
  "🏠": "house",
  "🌏": "map",
  "🔗": "link",
  "👥": "users",
  "💰": "banknote",

  "📞": "phone",
  "✉️": "mail",
  "📊": "chart",
  "📝": "file",
  "🚘": "car",
  "⚙️": "settings",
  "🔧": "wrench",

  "📱": "phone",
  "👷": "users",
  "🚀": "sparkles",
  "💬": "mail",
  "🔐": "shield",
  "📤": "file",
  "📖": "file",
  "🌐": "globe",

  "⏳": "clock",
  "🏢": "building",
  "🏗️": "building",
  "🏍️": "motorcycle",
  "♙": "users",
  "☷": "file",
  "⚖": "file",
  "⚙": "settings",

  "⌂": "house",
  "◫": "grid",
  "⌖": "map-pin",
  "▦": "grid",
  "▣": "ticket",
  "▤": "clipboard",
  "◷": "clock",

  "◉": "users",
  "↔": "arrow-right",
  "✓": "check",
  "✉": "mail",
  "!": "alert",
  "₫": "banknote",

  "?": "help",
  f: "users",
  in: "users",
  "▶": "youtube",

  "🔥": "flame",
  "🏷️": "tag",
  "🏪": "shopping-bag",
  "🎉": "award",
  "⭐": "star",
  "📍": "map-pin",
  "➔": "arrow-right",
  "✕": "x",
  "✨": "sparkles",
}

export function UntitledIcon({
  name,
  size = 20,
  style,
  ...props
}: { name: string; size?: number } & SVGProps<SVGSVGElement>) {
  const iconName = (
    name in paths ? name : aliases[name]
  ) as IconName | undefined

  const icon = iconName ? paths[iconName] : paths.activity

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      style={{
        display: "inline-block",
        flexShrink: 0,
        verticalAlign: "middle",
        ...style,
      }}
      {...props}
    >
      {icon}
    </svg>
  )
}
