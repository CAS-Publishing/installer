/* eslint-disable */
// SVG icons for the CAS Hub Installer mockup. Stroke-based, Unity Editor style.

const Icon = ({ children, size = 16, stroke = 'currentColor', fill = 'none', strokeWidth = 1.6, style }) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill={fill}
    stroke={stroke}
    strokeWidth={strokeWidth}
    strokeLinecap="round"
    strokeLinejoin="round"
    style={style}
  >
    {children}
  </svg>
);

const IconClose = (p) => (
  <Icon {...p}>
    <path d="M6 6L18 18M18 6L6 18" />
  </Icon>
);

const IconCheck = (p) => (
  <Icon {...p}>
    <path d="M4 12l5 5L20 6" />
  </Icon>
);

const IconCheckCircle = (p) => (
  <Icon {...p} fill="none">
    <circle cx="12" cy="12" r="9" />
    <path d="M8 12l3 3 5-6" />
  </Icon>
);

const IconWarning = (p) => (
  <Icon {...p}>
    <path d="M12 3L2 20h20L12 3z" />
    <path d="M12 10v5" />
    <circle cx="12" cy="18" r="0.6" fill="currentColor" stroke="none" />
  </Icon>
);

const IconChevronRight = (p) => (
  <Icon {...p}>
    <path d="M9 6l6 6-6 6" />
  </Icon>
);

const IconRefresh = (p) => (
  <Icon {...p}>
    <path d="M3 12a9 9 0 0115.5-6.3L21 8" />
    <path d="M21 3v5h-5" />
    <path d="M21 12a9 9 0 01-15.5 6.3L3 16" />
    <path d="M3 21v-5h5" />
  </Icon>
);

const IconRadio = ({ checked, size = 14 }) => (
  <svg width={size} height={size} viewBox="0 0 14 14">
    <circle cx="7" cy="7" r="6" fill="#2a2a2a" stroke="#5a5a5a" strokeWidth="1" />
    {checked && <circle cx="7" cy="7" r="3" fill="#4C7EFF" />}
  </svg>
);

const IconCheckbox = ({ checked, size = 14 }) => (
  <svg width={size} height={size} viewBox="0 0 14 14">
    <rect x="0.75" y="0.75" width="12.5" height="12.5" rx="1.5" fill={checked ? '#4C7EFF' : '#2a2a2a'} stroke={checked ? '#4C7EFF' : '#5a5a5a'} strokeWidth="1" />
    {checked && (
      <path d="M3.5 7.2l2.4 2.4L10.7 4.5" fill="none" stroke="#fff" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    )}
  </svg>
);

const IconSpinner = ({ size = 16 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" style={{ animation: 'cas-spin 1s linear infinite' }}>
    <circle cx="12" cy="12" r="9" fill="none" stroke="#3a3a3a" strokeWidth="2.5" />
    <path d="M21 12a9 9 0 00-9-9" fill="none" stroke="#4C7EFF" strokeWidth="2.5" strokeLinecap="round" />
  </svg>
);

const IconDownload = (p) => (
  <Icon {...p}>
    <path d="M12 4v12" />
    <path d="M7 11l5 5 5-5" />
    <path d="M5 20h14" />
  </Icon>
);

const IconSettings = (p) => (
  <Icon {...p}>
    <circle cx="12" cy="12" r="3" />
    <path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 11-4 0v-.09a1.65 1.65 0 00-1-1.51 1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 11-2.83-2.83l.06-.06A1.65 1.65 0 004.6 15a1.65 1.65 0 00-1.51-1H3a2 2 0 110-4h.09A1.65 1.65 0 004.6 9 1.65 1.65 0 004.27 7.18l-.06-.06a2 2 0 112.83-2.83l.06.06A1.65 1.65 0 009 4.6 1.65 1.65 0 0010 3.09V3a2 2 0 114 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 112.83 2.83l-.06.06a1.65 1.65 0 00-.33 1.82V9a1.65 1.65 0 001.51 1H21a2 2 0 110 4h-.09a1.65 1.65 0 00-1.51 1z" />
  </Icon>
);

const IconExternal = (p) => (
  <Icon {...p}>
    <path d="M15 3h6v6" />
    <path d="M10 14L21 3" />
    <path d="M20 13v7H4V4h7" />
  </Icon>
);

// "Robot" icon for "Make everything for me"
const IconRobot = ({ size = 36 }) => (
  <svg width={size} height={size} viewBox="0 0 48 48" fill="none">
    <rect x="11" y="16" width="26" height="22" rx="4" stroke="#fff" strokeWidth="1.8" />
    <path d="M24 10v6" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" />
    <circle cx="24" cy="9" r="2" fill="#fff" />
    <circle cx="19" cy="26" r="2.2" fill="#4ADE80" />
    <circle cx="29" cy="26" r="2.2" fill="#4ADE80" />
    <path d="M20 32h8" stroke="#fff" strokeWidth="1.6" strokeLinecap="round" />
    <path d="M11 24H7v8h4" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    <path d="M37 24h4v8h-4" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

// Big gear for "I will do it myself"
const IconGearBig = ({ size = 36 }) => (
  <svg width={size} height={size} viewBox="0 0 48 48" fill="none">
    <path
      d="M24 6.5l2 4.2 4.6-.6 1.6 4.4 4.3 1.7-.6 4.6 4.2 2-4.2 2 .6 4.6-4.3 1.7-1.6 4.4-4.6-.6-2 4.2-2-4.2-4.6.6-1.6-4.4L9.5 29l.6-4.6-4.2-2 4.2-2-.6-4.6 4.3-1.7 1.6-4.4 4.6.6L24 6.5z"
      stroke="#C7D0DA"
      strokeWidth="1.8"
      strokeLinejoin="round"
    />
    <circle cx="24" cy="24" r="5.5" stroke="#C7D0DA" strokeWidth="1.8" />
  </svg>
);

// "Wand + sparkles" for Hub Actions tile
const IconWand = ({ size = 28 }) => (
  <svg width={size} height={size} viewBox="0 0 32 32" fill="none">
    <path d="M5 27L20 12" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
    <path d="M19 11l3 3" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
    <path d="M24 4v4M22 6h4" stroke="#fff" strokeWidth="1.6" strokeLinecap="round" />
    <path d="M27 11v3M25.5 12.5h3" stroke="#fff" strokeWidth="1.6" strokeLinecap="round" />
    <path d="M7 5v3M5.5 6.5h3" stroke="#fff" strokeWidth="1.6" strokeLinecap="round" />
  </svg>
);

// CAS / Tenjin generic tile icon
const IconCube = ({ size = 18, color = '#9CC2FF' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    <path d="M12 3l8 4.5v9L12 21l-8-4.5v-9L12 3z" stroke={color} strokeWidth="1.6" strokeLinejoin="round" />
    <path d="M12 12l8-4.5M12 12v9M12 12L4 7.5" stroke={color} strokeWidth="1.6" />
  </svg>
);

// Component-specific tiny logos drawn as SVG glyphs (originals — not the vendor marks)
const LogoCAS = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    <rect x="2" y="2" width="20" height="20" rx="4" fill="#1f3a66" />
    <text x="12" y="15.5" textAnchor="middle" fontSize="7.5" fontWeight="700" fill="#fff" fontFamily="Inter, sans-serif">CAS</text>
  </svg>
);

const LogoTenjin = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    <rect x="2" y="2" width="20" height="20" rx="4" fill="#0f1b2e" />
    <path d="M7 8h10M12 8v9" stroke="#7AB7FF" strokeWidth="2" strokeLinecap="round" />
  </svg>
);

const LogoUnity = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    <rect x="2" y="2" width="20" height="20" rx="4" fill="#1a1a1a" />
    <path d="M12 6l5 3v6l-5 3-5-3V9l5-3z" stroke="#fff" strokeWidth="1.4" strokeLinejoin="round" />
    <path d="M12 6v6l5 3M12 12L7 15M12 12l5-3" stroke="#fff" strokeWidth="1.1" />
  </svg>
);

const LogoGoogle = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    <rect x="2" y="2" width="20" height="20" rx="4" fill="#fff" />
    <path d="M12 11v2.2h3.1c-.13.86-.94 2.5-3.1 2.5a3.7 3.7 0 110-7.4c1.05 0 1.76.45 2.16.84l1.48-1.42A5.7 5.7 0 0012 6.3a5.7 5.7 0 100 11.4c3.3 0 5.47-2.32 5.47-5.58 0-.37-.04-.66-.09-.92H12z" fill="#4285F4" />
  </svg>
);

const LogoMeta = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    <rect x="2" y="2" width="20" height="20" rx="4" fill="#0866FF" />
    <path d="M5 15c1.5-5 3.5-7 5.2-7 1.7 0 2.8 1.5 4.6 4.5 1.5 2.5 2.3 3 3 3 .7 0 1.2-.4 1.2-1.2" stroke="#fff" strokeWidth="1.6" fill="none" strokeLinecap="round" />
  </svg>
);

const LogoIron = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    <rect x="2" y="2" width="20" height="20" rx="4" fill="#1a1a1a" />
    <path d="M7 17V9l5 4 5-4v8" stroke="#7CE3A0" strokeWidth="1.6" strokeLinejoin="round" fill="none" />
  </svg>
);

const LogoIAP = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
    <rect x="2" y="2" width="20" height="20" rx="4" fill="#222a3a" />
    <path d="M7 9h10l-1 8H8L7 9z" stroke="#FFD479" strokeWidth="1.5" strokeLinejoin="round" fill="none" />
    <path d="M9 9V7a3 3 0 016 0v2" stroke="#FFD479" strokeWidth="1.5" fill="none" />
  </svg>
);

const IconDashboard = ({ size = 28 }) => (
  <svg width={size} height={size} viewBox="0 0 32 32" fill="none">
    <rect x="4" y="6" width="24" height="20" rx="2.5" stroke="#fff" strokeWidth="1.8" />
    <path d="M4 11h24" stroke="#fff" strokeWidth="1.8" />
    <circle cx="7" cy="8.5" r="0.8" fill="#fff" />
    <circle cx="10" cy="8.5" r="0.8" fill="#fff" />
    <path d="M8 21l4-4 3 3 5-6" stroke="#7AB7FF" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const IconReset = ({ size = 28 }) => (
  <svg width={size} height={size} viewBox="0 0 32 32" fill="none">
    <path d="M5 16a11 11 0 1019-7.5L26 6" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    <path d="M26 4v5h-5" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

const IconGearTile = ({ size = 28 }) => (
  <svg width={size} height={size} viewBox="0 0 32 32" fill="none">
    <circle cx="16" cy="16" r="4" stroke="#fff" strokeWidth="2" />
    <path d="M16 3v3M16 26v3M3 16h3M26 16h3M6.8 6.8l2.2 2.2M23 23l2.2 2.2M6.8 25.2L9 23M23 9l2.2-2.2" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
  </svg>
);

const IconDownloadBig = ({ size = 56 }) => (
  <svg width={size} height={size} viewBox="0 0 64 64" fill="none">
    <rect x="6" y="6" width="52" height="52" rx="10" fill="#3960C9" />
    <path d="M32 18v22" stroke="#fff" strokeWidth="3" strokeLinecap="round" />
    <path d="M22 32l10 10 10-10" stroke="#fff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
    <path d="M18 46h28" stroke="#fff" strokeWidth="3" strokeLinecap="round" />
  </svg>
);

const IconBigCheck = ({ size = 96 }) => (
  <svg width={size} height={size} viewBox="0 0 96 96" fill="none">
    <circle cx="48" cy="48" r="40" fill="#22A06B" />
    <path d="M30 49l13 13 23-26" stroke="#fff" strokeWidth="6" strokeLinecap="round" strokeLinejoin="round" fill="none" />
  </svg>
);

const IconFolder = (p) => (
  <Icon {...p}>
    <path d="M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V7z" />
  </Icon>
);

const IconPackage = (p) => (
  <Icon {...p}>
    <path d="M21 8L12 3 3 8v8l9 5 9-5V8z" />
    <path d="M3 8l9 5 9-5M12 13v8" />
  </Icon>
);

const IconBook = (p) => (
  <Icon {...p}>
    <path d="M4 5a2 2 0 012-2h12v16H6a2 2 0 00-2 2V5z" />
    <path d="M4 19a2 2 0 002 2h12" />
  </Icon>
);

const IconChat = (p) => (
  <Icon {...p}>
    <path d="M21 12a8 8 0 11-3.5-6.6L21 4l-1.4 3.5A8 8 0 0121 12z" />
    <circle cx="9" cy="12" r="0.8" fill="currentColor" stroke="none" />
    <circle cx="13" cy="12" r="0.8" fill="currentColor" stroke="none" />
    <circle cx="17" cy="12" r="0.8" fill="currentColor" stroke="none" />
  </Icon>
);

Object.assign(window, {
  Icon,
  IconClose, IconCheck, IconCheckCircle, IconWarning, IconChevronRight,
  IconRefresh, IconRadio, IconCheckbox, IconSpinner, IconDownload,
  IconSettings, IconExternal, IconRobot, IconGearBig, IconWand,
  IconCube, IconDashboard, IconReset, IconGearTile,
  IconDownloadBig, IconBigCheck, IconFolder, IconPackage, IconBook, IconChat,
  LogoCAS, LogoTenjin, LogoUnity, LogoGoogle, LogoMeta, LogoIron, LogoIAP,
});
