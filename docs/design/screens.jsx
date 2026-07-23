/* eslint-disable */
// 8 screens of the CAS Hub Installer (CleverAdsSolutions Hub Installer).
// All windows share width/height with WindowChrome so they read as one wizard.

const W = 480;
const H = 560;

// ────────────────────────────────────────────────────────────────────────────
// 1. Welcome
// ────────────────────────────────────────────────────────────────────────────
const Screen1Welcome = () => {
  const [method, setMethod] = React.useState('git');
  return (
    <WindowChrome number={1} width={W} height={H}>
      <div style={{ padding: 24, paddingBottom: 8, flex: 1, display: 'flex', flexDirection: 'column' }}>
        {/* Logo lockup */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 14, justifyContent: 'center', marginTop: 4 }}>
          <div
            style={{
              width: 56,
              height: 56,
              borderRadius: 10,
              background: '#1f3a66',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: '#fff',
              fontWeight: 700,
              fontSize: 18,
              letterSpacing: 0.5,
            }}
          >
            CAS
          </div>
          <div style={{ width: 1, height: 44, background: '#555' }} />
          <div>
            <div style={{ color: '#fff', fontWeight: 700, fontSize: 22, letterSpacing: 1 }}>CAS</div>
            <div style={{ color: '#9AA0A6', fontSize: 11, letterSpacing: 0.4 }}>CleverAdsSolutions</div>
          </div>
        </div>

        <h1 style={{ color: '#fff', fontSize: 19, fontWeight: 600, textAlign: 'center', margin: '22px 0 6px' }}>
          Welcome to CAS Hub Installer
        </h1>
        <p style={{ color: '#9AA0A6', fontSize: 12, textAlign: 'center', margin: 0, lineHeight: 1.5 }}>
          This tool will help you install and configure all
          <br />
          required components for CleverAdsSolutions Hub.
        </p>

        <div style={{ marginTop: 20 }}>
          <div style={{ color: '#C7C7C7', fontWeight: 600, fontSize: 12, marginBottom: 10 }}>Installation method</div>

          {[
            { id: 'git', title: 'Git URL (Recommended)', desc: 'Install latest version via Git repository URL' },
            { id: 'upm', title: 'UPM (Unity Package Manager)', desc: 'Install via scoped registry' },
            { id: 'pkg', title: 'Unity Package (unitypackage)', desc: 'Install from local package' },
          ].map((opt) => (
            <label
              key={opt.id}
              onClick={() => setMethod(opt.id)}
              style={{ display: 'flex', alignItems: 'flex-start', gap: 10, padding: '6px 0', cursor: 'pointer' }}
            >
              <span style={{ marginTop: 2 }}>
                <IconRadio checked={method === opt.id} />
              </span>
              <div>
                <div style={{ color: '#E5E5E5', fontSize: 12, fontWeight: 500 }}>{opt.title}</div>
                <div style={{ color: '#8E8E8E', fontSize: 11, marginTop: 1 }}>{opt.desc}</div>
              </div>
            </label>
          ))}
        </div>

        <div style={{ marginTop: 16 }}>
          <div style={{ color: '#C7C7C7', fontSize: 12, marginBottom: 6 }}>Git URL</div>
          <input
            defaultValue="https://github.com/CleverAdsSolutions/cas-hub.git"
            style={{
              width: '100%',
              background: '#2a2a2a',
              border: '1px solid #1d1d1d',
              borderRadius: 3,
              color: '#D5D5D5',
              padding: '7px 9px',
              fontFamily: FontStack,
              fontSize: 12,
              outline: 'none',
              boxSizing: 'border-box',
            }}
          />
          <button
            style={{
              marginTop: 8,
              width: '100%',
              background: '#4A4A4A',
              border: '1px solid #1f1f1f',
              borderRadius: 3,
              color: '#E5E5E5',
              padding: '7px 0',
              cursor: 'pointer',
              fontFamily: FontStack,
              fontSize: 12,
            }}
          >
            Load Information
          </button>
        </div>
      </div>

      <FootRule />
      <div style={{ padding: '10px 18px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span style={{ color: '#6E6E6E', fontSize: 11 }}>v1.2.3</span>
        <Button variant="primary" style={{ minWidth: 100 }}>
          Next
        </Button>
      </div>
    </WindowChrome>
  );
};

// ────────────────────────────────────────────────────────────────────────────
// 2. Integration mode
// ────────────────────────────────────────────────────────────────────────────
const Screen2Integration = () => {
  const [pick, setPick] = React.useState('auto');
  return (
    <WindowChrome number={2} width={W} height={H}>
      <div style={{ padding: 22, flex: 1, display: 'flex', flexDirection: 'column' }}>
        <h2 style={{ color: '#fff', fontSize: 17, fontWeight: 600, margin: 0 }}>Integration mode</h2>
        <p style={{ color: '#8E8E8E', fontSize: 12, margin: '4px 0 16px' }}>
          Choose how you want to integrate CAS Hub into your project.
        </p>

        {/* Auto card */}
        <div
          onClick={() => setPick('auto')}
          style={{
            cursor: 'pointer',
            background: pick === 'auto' ? 'rgba(34, 160, 107, 0.14)' : '#333',
            border: `1.5px solid ${pick === 'auto' ? '#22A06B' : '#2a2a2a'}`,
            borderRadius: 6,
            padding: 14,
            display: 'flex',
            gap: 14,
            alignItems: 'center',
          }}
        >
          <div
            style={{
              width: 54,
              height: 54,
              background: '#22A06B',
              borderRadius: 8,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <IconRobot size={34} />
          </div>
          <div style={{ flex: 1 }}>
            <div style={{ color: '#fff', fontWeight: 600, fontSize: 14, marginBottom: 4 }}>Make everything for me</div>
            <div style={{ color: '#A8A8A8', fontSize: 11.5, lineHeight: 1.45 }}>
              We will automatically install and configure
              <br />
              all required components and settings.
            </div>
            <div
              style={{
                display: 'inline-block',
                marginTop: 8,
                background: '#22A06B',
                color: '#fff',
                fontSize: 10.5,
                fontWeight: 600,
                padding: '2px 8px',
                borderRadius: 3,
              }}
            >
              Recommended
            </div>
          </div>
        </div>

        {/* Manual card */}
        <div
          onClick={() => setPick('manual')}
          style={{
            cursor: 'pointer',
            marginTop: 12,
            background: pick === 'manual' ? 'rgba(76, 126, 255, 0.10)' : '#333',
            border: `1.5px solid ${pick === 'manual' ? '#4C7EFF' : '#2a2a2a'}`,
            borderRadius: 6,
            padding: 14,
            display: 'flex',
            gap: 14,
            alignItems: 'center',
          }}
        >
          <div
            style={{
              width: 54,
              height: 54,
              background: '#2a2a2a',
              border: '1px solid #3a3a3a',
              borderRadius: 8,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <IconGearBig size={32} />
          </div>
          <div style={{ flex: 1 }}>
            <div style={{ color: '#fff', fontWeight: 600, fontSize: 14, marginBottom: 4 }}>I will do it myself</div>
            <div style={{ color: '#A8A8A8', fontSize: 11.5, lineHeight: 1.45 }}>
              I want to configure everything manually.
              <br />
              (For advanced users only)
            </div>
          </div>
        </div>

        {/* Warning */}
        <div
          style={{
            marginTop: 14,
            background: '#3a3320',
            border: '1px solid #7a6420',
            borderRadius: 4,
            padding: 12,
            display: 'flex',
            gap: 10,
            alignItems: 'flex-start',
          }}
        >
          <span style={{ color: '#E0A030', flexShrink: 0, marginTop: 1 }}>
            <IconWarning size={18} />
          </span>
          <div style={{ fontSize: 11.5, color: '#D8C8A0', lineHeight: 1.5 }}>
            <strong style={{ color: '#E8C57A' }}>Warning!</strong> Manual integration may lead to incorrect
            configuration and unpredictable behavior.
            <br />
            Are you sure you know what you're doing?
          </div>
        </div>
      </div>

      <FootRule />
      <div style={{ padding: '10px 18px', display: 'flex', justifyContent: 'space-between' }}>
        <Button>Cancel</Button>
        <Button variant="primary">I understand, continue</Button>
      </div>
    </WindowChrome>
  );
};

// ────────────────────────────────────────────────────────────────────────────
// 3. Components Overview
// ────────────────────────────────────────────────────────────────────────────
const Screen3Components = () => {
  const rows = [
    { logo: <LogoCAS />, name: 'CAS SDK', sub: 'Core SDK', status: ['green', 'Installed'], action: 'Up to date', actVar: 'soft', init: true },
    { logo: <LogoTenjin />, name: 'Tenjin SDK', sub: 'Attribution', status: ['green', 'Installed'], action: 'Up to date', actVar: 'soft', init: true },
    { logo: <LogoUnity />, name: 'Unity Ads', sub: 'Ads Network', status: ['red', 'Not Installed'], action: 'Install', actVar: 'primary', init: true },
    { logo: <LogoGoogle />, name: 'Google Mobile Ads', sub: 'Ads Network', status: ['yellow', 'Installed'], action: 'Update', actVar: 'warn', init: true },
    { logo: <LogoMeta />, name: 'Facebook Audience Network', sub: 'Ads Network', status: ['red', 'Not Installed'], action: 'Install', actVar: 'primary', init: false },
    { logo: <LogoIron />, name: 'IronSource LevelPlay', sub: 'Offerwall', status: ['green', 'Installed'], action: 'Up to date', actVar: 'soft', init: true },
    { logo: <LogoIAP />, name: 'Unity IAP', sub: 'In-App Purchases', status: ['green', 'Installed'], action: 'Up to date', actVar: 'soft', init: true },
  ];

  return (
    <WindowChrome number={3} width={W} height={H}>
      <div style={{ padding: 22, flex: 1, display: 'flex', flexDirection: 'column' }}>
        <h2 style={{ color: '#fff', fontSize: 17, fontWeight: 600, margin: 0 }}>Components Overview</h2>
        <p style={{ color: '#8E8E8E', fontSize: 12, margin: '4px 0 14px' }}>
          Check the status of required components.
        </p>

        {/* Header row */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: '1.5fr 1fr 1fr 60px',
            padding: '6px 10px',
            background: '#2c2c2c',
            borderTop: '1px solid #1f1f1f',
            borderBottom: '1px solid #1f1f1f',
            fontSize: 11,
            color: '#9A9A9A',
            fontWeight: 500,
          }}
        >
          <span>Component</span>
          <span>Status</span>
          <span>Action</span>
          <span style={{ textAlign: 'center' }}>Auto Init</span>
        </div>

        <div style={{ background: '#333', border: '1px solid #1f1f1f', borderTop: 0 }}>
          {rows.map((r, i) => (
            <div
              key={r.name}
              style={{
                display: 'grid',
                gridTemplateColumns: '1.5fr 1fr 1fr 60px',
                alignItems: 'center',
                padding: '7px 10px',
                borderBottom: i < rows.length - 1 ? '1px solid #2a2a2a' : 'none',
                background: i % 2 === 0 ? '#343434' : '#303030',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <span style={{ width: 6, height: 6, borderRadius: 3, background: r.status[0] === 'red' ? '#D8534F' : r.status[0] === 'yellow' ? '#E0A030' : '#4ADE80' }} />
                {r.logo}
                <div>
                  <div style={{ color: '#E5E5E5', fontSize: 11.5, lineHeight: 1.2 }}>{r.name}</div>
                  <div style={{ color: '#7E7E7E', fontSize: 10 }}>{r.sub}</div>
                </div>
              </div>
              <span
                style={{
                  fontSize: 11,
                  color: r.status[0] === 'red' ? '#E58783' : r.status[0] === 'yellow' ? '#E8B463' : '#A8A8A8',
                }}
              >
                {r.status[1]}
              </span>
              <div>
                {r.actVar === 'primary' ? (
                  <Button variant="primary" style={{ padding: '3px 14px', fontSize: 11, minWidth: 70 }}>{r.action}</Button>
                ) : r.actVar === 'warn' ? (
                  <button
                    style={{
                      background: '#D8893F',
                      border: '1px solid #8C5A28',
                      color: '#fff',
                      padding: '3px 14px',
                      fontSize: 11,
                      borderRadius: 3,
                      cursor: 'pointer',
                      fontFamily: FontStack,
                      minWidth: 70,
                    }}
                  >
                    {r.action}
                  </button>
                ) : (
                  <button
                    style={{
                      background: '#454545',
                      border: '1px solid #1f1f1f',
                      color: '#C5C5C5',
                      padding: '3px 14px',
                      fontSize: 11,
                      borderRadius: 3,
                      cursor: 'default',
                      fontFamily: FontStack,
                      minWidth: 70,
                    }}
                  >
                    {r.action}
                  </button>
                )}
              </div>
              <div style={{ display: 'flex', justifyContent: 'center' }}>
                <IconCheckbox checked={r.init} />
              </div>
            </div>
          ))}
        </div>
      </div>

      <FootRule />
      <div style={{ padding: '10px 18px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 11 }}>
        <Button variant="soft" style={{ padding: '5px 12px' }}>
          <IconRefresh size={12} /> Refresh
        </Button>
        <div style={{ display: 'flex', gap: 14, color: '#9A9A9A' }}>
          <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#4ADE80' }} /> Installed
          </span>
          <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#E0A030' }} /> Update available
          </span>
          <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
            <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#D8534F' }} /> Not Installed
          </span>
        </div>
      </div>
    </WindowChrome>
  );
};

// ────────────────────────────────────────────────────────────────────────────
// 4. Hub Actions
// ────────────────────────────────────────────────────────────────────────────
const Screen4HubActions = () => {
  const tiles = [
    {
      title: 'Make everything for me',
      desc: 'Automatically install, configure and initialize all components.',
      icon: <IconWand size={26} />,
      bg: '#3D6AE0',
      iconBg: 'rgba(255,255,255,0.18)',
      selected: true,
    },
    {
      title: 'Reset All Settings',
      desc: 'This will remove all CAS Hub settings and configuration from the project.',
      icon: <IconReset size={26} />,
      iconBg: '#454545',
    },
    {
      title: 'Open CAS Settings',
      desc: 'Configure SDK, ad formats, networks and other settings.',
      icon: <IconGearTile size={26} />,
      iconBg: '#454545',
    },
    {
      title: 'Open Tenjin Dashboard',
      desc: 'Manage attribution & analytics in Tenjin dashboard.',
      icon: <IconDashboard size={26} />,
      iconBg: '#454545',
    },
  ];
  return (
    <WindowChrome number={4} width={W} height={H}>
      <div style={{ padding: 22, flex: 1, display: 'flex', flexDirection: 'column', gap: 10 }}>
        <h2 style={{ color: '#fff', fontSize: 17, fontWeight: 600, margin: 0 }}>Hub Actions</h2>

        {tiles.map((t) => (
          <div
            key={t.title}
            style={{
              background: t.bg || '#333',
              border: `1px solid ${t.selected ? '#1d3e9a' : '#2a2a2a'}`,
              borderRadius: 5,
              padding: '12px 14px',
              display: 'flex',
              alignItems: 'center',
              gap: 14,
              cursor: 'pointer',
            }}
          >
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: 6,
                background: t.iconBg,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#fff',
                flexShrink: 0,
              }}
            >
              {t.icon}
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ color: '#fff', fontWeight: 600, fontSize: 13 }}>{t.title}</div>
              <div style={{ color: t.bg ? 'rgba(255,255,255,0.85)' : '#9A9A9A', fontSize: 11.5, marginTop: 2 }}>
                {t.desc}
              </div>
            </div>
            <span style={{ color: t.bg ? '#fff' : '#7A7A7A' }}>
              <IconChevronRight size={16} />
            </span>
          </div>
        ))}
      </div>
      <FootRule />
      <div style={{ padding: '10px 18px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 11 }}>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="ghost" style={{ padding: '4px 10px', fontSize: 11 }}>
            <IconBook size={12} /> Documentation
          </Button>
          <Button variant="ghost" style={{ padding: '4px 10px', fontSize: 11 }}>
            <IconChat size={12} /> Support
          </Button>
        </div>
        <div style={{ textAlign: 'right', color: '#7E7E7E' }}>
          v1.2.3
          <br />
          <span style={{ color: '#4C7EFF', cursor: 'pointer' }}>Check for Updates</span>
        </div>
      </div>
    </WindowChrome>
  );
};

// ────────────────────────────────────────────────────────────────────────────
// 5. Installation Progress
// ────────────────────────────────────────────────────────────────────────────
const Screen5Progress = () => {
  const items = [
    { label: 'Checking system requirements', state: 'done' },
    { label: 'Resolving dependencies', state: 'done' },
    { label: 'Installing CAS SDK', state: 'done' },
    { label: 'Installing Tenjin SDK', state: 'done' },
    { label: 'Installing Unity Ads', state: 'active', rightLabel: 'Installing…' },
    { label: 'Installing Google Mobile Ads', state: 'wait' },
    { label: 'Installing Facebook Audience Network', state: 'wait' },
    { label: 'Configuring settings', state: 'wait' },
    { label: 'Initializing components', state: 'wait' },
  ];
  return (
    <WindowChrome number={5} width={W} height={H}>
      <div style={{ padding: 22, flex: 1, display: 'flex', flexDirection: 'column' }}>
        <h2 style={{ color: '#fff', fontSize: 17, fontWeight: 600, margin: 0 }}>Installation Progress</h2>

        <div style={{ marginTop: 16, display: 'flex', flexDirection: 'column', gap: 4 }}>
          {items.map((it) => (
            <div
              key={it.label}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 10,
                padding: '8px 4px',
                borderBottom: '1px solid #2c2c2c',
              }}
            >
              <span style={{ width: 18, display: 'flex', justifyContent: 'center' }}>
                {it.state === 'done' && (
                  <span style={{ color: '#4ADE80' }}>
                    <IconCheck size={16} strokeWidth={2.4} />
                  </span>
                )}
                {it.state === 'active' && <IconSpinner size={14} />}
                {it.state === 'wait' && (
                  <span style={{ width: 10, height: 10, borderRadius: '50%', border: '1.5px solid #555' }} />
                )}
              </span>
              <span style={{ flex: 1, color: it.state === 'wait' ? '#8A8A8A' : '#E0E0E0', fontSize: 12.5 }}>
                {it.label}
              </span>
              {it.state === 'active' && <span style={{ color: '#9CC2FF', fontSize: 11 }}>{it.rightLabel}</span>}
              {it.state === 'wait' && <span style={{ color: '#6E6E6E', fontSize: 11 }}>Waiting</span>}
            </div>
          ))}
        </div>

        <div style={{ marginTop: 'auto', paddingTop: 16 }}>
          <div
            style={{
              height: 6,
              background: '#2a2a2a',
              borderRadius: 3,
              overflow: 'hidden',
              position: 'relative',
              border: '1px solid #1f1f1f',
            }}
          >
            <div
              style={{
                width: '56%',
                height: '100%',
                background: 'linear-gradient(90deg, #4C7EFF, #6E96FF)',
              }}
            />
          </div>
          <div style={{ textAlign: 'right', color: '#9A9A9A', fontSize: 11, marginTop: 4 }}>56%</div>
        </div>
      </div>
      <FootRule />
      <div style={{ padding: '10px 18px', display: 'flex', justifyContent: 'center' }}>
        <Button style={{ minWidth: 100 }}>Cancel</Button>
      </div>
    </WindowChrome>
  );
};

// ────────────────────────────────────────────────────────────────────────────
// 6. All Done
// ────────────────────────────────────────────────────────────────────────────
const Screen6Done = () => (
  <WindowChrome number={6} width={W} height={H}>
    <div style={{ padding: 22, flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
      <div style={{ marginTop: 32 }}>
        <IconBigCheck size={104} />
      </div>
      <h2 style={{ color: '#fff', fontSize: 22, fontWeight: 600, margin: '22px 0 8px' }}>All Done!</h2>
      <p style={{ color: '#9A9A9A', fontSize: 12.5, textAlign: 'center', margin: 0, lineHeight: 1.5 }}>
        CAS Hub has been successfully installed
        <br />
        and configured in your project.
      </p>

      <div style={{ marginTop: 30, width: '100%', display: 'flex', flexDirection: 'column', gap: 10 }}>
        <Button variant="soft" style={{ padding: '10px 0', fontSize: 12.5 }}>
          Open CAS Settings
        </Button>
        <Button variant="soft" style={{ padding: '10px 0', fontSize: 12.5 }}>
          View Documentation
        </Button>
        <Button variant="primary" style={{ padding: '10px 0', fontSize: 12.5 }}>
          Close
        </Button>
      </div>
    </div>
  </WindowChrome>
);

// ────────────────────────────────────────────────────────────────────────────
// 7. Update Installer
// ────────────────────────────────────────────────────────────────────────────
const Screen7Update = () => (
  <WindowChrome number={7} width={W} height={H}>
    <div style={{ padding: 22, flex: 1, display: 'flex', flexDirection: 'column' }}>
      <h2 style={{ color: '#fff', fontSize: 17, fontWeight: 600, margin: 0 }}>Update Installer</h2>
      <p style={{ color: '#9A9A9A', fontSize: 12, margin: '4px 0 16px' }}>
        A new version of the installer is available!
      </p>

      <div style={{ display: 'flex', gap: 22, alignItems: 'flex-start' }}>
        <div style={{ flex: 1 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid #2a2a2a', fontSize: 12 }}>
            <span style={{ color: '#9A9A9A' }}>Current version:</span>
            <span style={{ color: '#E0E0E0' }}>1.2.3</span>
          </div>
          <div style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', fontSize: 12 }}>
            <span style={{ color: '#9A9A9A' }}>Latest version:</span>
            <span style={{ color: '#4ADE80', fontWeight: 600 }}>1.2.5</span>
          </div>
        </div>
        <IconDownloadBig size={78} />
      </div>

      <div style={{ marginTop: 18 }}>
        <div style={{ color: '#C7C7C7', fontWeight: 600, fontSize: 12.5, marginBottom: 8 }}>What's new:</div>
        <ul style={{ margin: 0, paddingLeft: 18, color: '#B5B5B5', fontSize: 12, lineHeight: 1.7 }}>
          <li>Updated component links</li>
          <li>Added new network support</li>
          <li>Bug fixes and improvements</li>
        </ul>
      </div>

      <div style={{ flex: 1 }} />

      <div
        style={{
          background: 'rgba(76, 126, 255, 0.08)',
          border: '1px solid rgba(76, 126, 255, 0.25)',
          borderRadius: 4,
          padding: '10px 12px',
          fontSize: 11.5,
          color: '#9CC2FF',
          display: 'flex',
          gap: 8,
          alignItems: 'flex-start',
        }}
      >
        <IconDownload size={14} />
        Updating preserves your project settings and configuration.
      </div>
    </div>
    <FootRule />
    <div style={{ padding: '10px 18px', display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
      <Button>Later</Button>
      <Button variant="primary">Update Now</Button>
    </div>
  </WindowChrome>
);

// ────────────────────────────────────────────────────────────────────────────
// 8. CAS Settings (Redirect)
// ────────────────────────────────────────────────────────────────────────────
const Screen8Settings = () => (
  <WindowChrome number={8} width={W} height={H}>
    <div style={{ padding: '6px 18px', borderBottom: '1px solid #2a2a2a', display: 'flex', alignItems: 'center', gap: 6, fontSize: 11, color: '#9A9A9A' }}>
      <span>CAS Settings</span>
      <IconChevronRight size={11} />
      <span style={{ color: '#C7C7C7' }}>Redirect</span>
    </div>
    <div style={{ padding: 22, flex: 1, display: 'flex', flexDirection: 'column' }}>
      <h2 style={{ color: '#fff', fontSize: 19, fontWeight: 600, margin: 0 }}>CAS SDK Settings</h2>
      <p style={{ color: '#9A9A9A', fontSize: 12, margin: '4px 0 16px' }}>
        All core settings are managed by CAS SDK.
      </p>

      <div style={{ display: 'flex', gap: 18, alignItems: 'flex-start' }}>
        <div style={{ flex: 1 }}>
          <Button variant="soft" style={{ width: '100%', padding: '9px 12px', justifyContent: 'space-between' }}>
            <span style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <IconSettings size={14} />
              Open CAS Settings Window
            </span>
            <IconExternal size={12} />
          </Button>
        </div>
        <div
          style={{
            width: 86,
            height: 86,
            borderRadius: 10,
            background: '#fff',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            padding: 6,
            flexShrink: 0,
          }}
        >
          <div style={{ color: '#1f3a66', fontWeight: 700, fontSize: 20, letterSpacing: 0.5 }}>CAS</div>
          <div style={{ color: '#6E7B8E', fontSize: 7.5, fontWeight: 500, textAlign: 'center', marginTop: 2 }}>
            CleverAdsSolutions
          </div>
        </div>
      </div>

      <div style={{ marginTop: 18 }}>
        <div style={{ color: '#C7C7C7', fontWeight: 600, fontSize: 12.5, marginBottom: 8 }}>Here you can configure:</div>
        <ul style={{ margin: 0, paddingLeft: 18, color: '#B5B5B5', fontSize: 12, lineHeight: 1.7 }}>
          <li>CAS ID (if required)</li>
          <li>Ad formats</li>
          <li>Networks</li>
          <li>Mediation settings</li>
          <li>More…</li>
        </ul>
      </div>

      <div style={{ flex: 1 }} />

      <div
        style={{
          background: '#2a2a2a',
          border: '1px solid #1f1f1f',
          borderRadius: 4,
          padding: 12,
          fontSize: 11.5,
          color: '#9A9A9A',
          lineHeight: 1.55,
        }}
      >
        <span style={{ color: '#9CC2FF' }}>Tip:</span> All Hub installer changes here are applied immediately and saved
        to the project.
      </div>
    </div>
  </WindowChrome>
);

Object.assign(window, {
  Screen1Welcome,
  Screen2Integration,
  Screen3Components,
  Screen4HubActions,
  Screen5Progress,
  Screen6Done,
  Screen7Update,
  Screen8Settings,
});
