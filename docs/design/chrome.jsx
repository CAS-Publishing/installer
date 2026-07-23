/* eslint-disable */
// Shared chrome and primitives for CAS Hub Installer windows.

const COLORS = {
  windowBg: '#383838',
  panel: '#3C3C3C',
  panelDeep: '#2D2D2D',
  border: '#1F1F1F',
  borderSoft: '#2E2E2E',
  text: '#D3D3D3',
  textStrong: '#FFFFFF',
  textMuted: '#8E8E8E',
  accent: '#4C7EFF',
  accentDeep: '#3B6AE0',
  green: '#22A06B',
  greenSoft: '#1f6e4d',
  yellow: '#E0A030',
  red: '#D8534F',
  warningBg: '#3a3320',
  warningBorder: '#7a6420',
};

// 30x30 grey square = generic Unity grid icon used as fallback
const FontStack = `'Inter', -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif`;

const WindowChrome = ({ number, width = 480, height = 560, children }) => (
  <div
    style={{
      width,
      height,
      background: COLORS.windowBg,
      border: `1px solid ${COLORS.border}`,
      borderRadius: 6,
      fontFamily: FontStack,
      color: COLORS.text,
      fontSize: 12,
      display: 'flex',
      flexDirection: 'column',
      overflow: 'hidden',
      boxShadow: '0 12px 32px rgba(0,0,0,0.35)',
      position: 'relative',
    }}
  >
    {/* Title bar */}
    <div
      style={{
        height: 28,
        background: '#4D4D4D',
        borderBottom: `1px solid ${COLORS.border}`,
        display: 'flex',
        alignItems: 'center',
        padding: '0 10px',
        justifyContent: 'space-between',
        userSelect: 'none',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <div
          style={{
            width: 14,
            height: 14,
            borderRadius: 3,
            background: '#1f3a66',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 7,
            fontWeight: 700,
            color: '#fff',
            letterSpacing: -0.2,
          }}
        >
          CAS
        </div>
        <span style={{ fontSize: 12, color: '#E0E0E0' }}>CleverAdsSolutions Hub Installer</span>
      </div>
      <div style={{ color: '#A8A8A8', cursor: 'default' }}>
        <IconClose size={14} strokeWidth={1.8} />
      </div>
    </div>

    {/* Step number badge */}
    {number != null && (
      <div
        style={{
          position: 'absolute',
          top: -10,
          left: -10,
          width: 28,
          height: 28,
          borderRadius: '50%',
          background: '#4C7EFF',
          color: '#fff',
          fontWeight: 700,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: 13,
          boxShadow: '0 4px 10px rgba(0,0,0,0.45)',
          border: '2px solid #1a1a1a',
        }}
      >
        {number}
      </div>
    )}

    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 }}>{children}</div>
  </div>
);

const Button = ({ variant = 'default', children, style, ...rest }) => {
  const variants = {
    default: {
      background: 'linear-gradient(180deg, #585858, #4A4A4A)',
      border: '1px solid #1f1f1f',
      color: '#E5E5E5',
    },
    primary: {
      background: 'linear-gradient(180deg, #5A8BFF, #3B6AE0)',
      border: '1px solid #1d3e9a',
      color: '#fff',
    },
    ghost: {
      background: 'transparent',
      border: '1px solid #2a2a2a',
      color: '#D0D0D0',
    },
    soft: {
      background: '#3a3a3a',
      border: '1px solid #2a2a2a',
      color: '#E0E0E0',
    },
  };
  return (
    <button
      style={{
        ...variants[variant],
        padding: '6px 14px',
        borderRadius: 3,
        fontFamily: FontStack,
        fontSize: 12,
        cursor: 'pointer',
        fontWeight: 500,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        gap: 6,
        ...style,
      }}
      {...rest}
    >
      {children}
    </button>
  );
};

const Pill = ({ tone = 'green', children }) => {
  const tones = {
    green: { bg: 'rgba(74, 222, 128, 0.12)', fg: '#4ADE80', dot: '#4ADE80' },
    yellow: { bg: 'rgba(224, 160, 48, 0.16)', fg: '#E8B463', dot: '#E0A030' },
    red: { bg: 'rgba(216, 83, 79, 0.14)', fg: '#E58783', dot: '#D8534F' },
  };
  const t = tones[tone];
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
        background: t.bg,
        color: t.fg,
        padding: '2px 8px 2px 6px',
        borderRadius: 10,
        fontSize: 11,
        fontWeight: 500,
      }}
    >
      <span style={{ width: 6, height: 6, borderRadius: '50%', background: t.dot }} />
      {children}
    </span>
  );
};

const FootRule = () => (
  <div style={{ height: 1, background: COLORS.border, margin: '0' }} />
);

Object.assign(window, { COLORS, WindowChrome, Button, Pill, FootRule, FontStack });
