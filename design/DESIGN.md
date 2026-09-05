---
name: Solaris Audio Workstation
colors:
  surface: '#111317'
  surface-dim: '#111317'
  surface-bright: '#37393e'
  surface-container-lowest: '#0c0e12'
  surface-container-low: '#1a1c20'
  surface-container: '#1e2024'
  surface-container-high: '#282a2e'
  surface-container-highest: '#333539'
  on-surface: '#e2e2e8'
  on-surface-variant: '#e2bfb0'
  inverse-surface: '#e2e2e8'
  inverse-on-surface: '#2f3035'
  outline: '#a98a7d'
  outline-variant: '#5a4136'
  surface-tint: '#ffb693'
  primary: '#ffb693'
  on-primary: '#561f00'
  primary-container: '#ff6b00'
  on-primary-container: '#572000'
  inverse-primary: '#a04100'
  secondary: '#ffc640'
  on-secondary: '#402d00'
  secondary-container: '#e3aa00'
  on-secondary-container: '#5a4100'
  tertiary: '#ffb599'
  on-tertiary: '#5a1c00'
  tertiary-container: '#ff6a26'
  on-tertiary-container: '#5b1c00'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#ffdbcc'
  primary-fixed-dim: '#ffb693'
  on-primary-fixed: '#351000'
  on-primary-fixed-variant: '#7a3000'
  secondary-fixed: '#ffdf9f'
  secondary-fixed-dim: '#f9bd22'
  on-secondary-fixed: '#261a00'
  on-secondary-fixed-variant: '#5c4300'
  tertiary-fixed: '#ffdbce'
  tertiary-fixed-dim: '#ffb599'
  on-tertiary-fixed: '#370e00'
  on-tertiary-fixed-variant: '#7f2b00'
  background: '#111317'
  on-background: '#e2e2e8'
  surface-variant: '#333539'
  neon-orange: '#ff6b00'
  sunset-amber: '#fbbf24'
  deep-ember: '#ea580c'
  surface-base: '#0c0e12'
  surface-panel: '#14171f'
  surface-card: '#1e222d'
  surface-highlight: '#282d3c'
  peak-red: '#ef4444'
  signal-nominal: '#22c55e'
  meter-dim: '#334155'
typography:
  headline-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 36px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: -0.015em
  headline-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 22px
    letterSpacing: -0.01em
  headline-lg-mobile:
    fontFamily: Plus Jakarta Sans
    fontSize: 22px
    fontWeight: '700'
    lineHeight: 28px
    letterSpacing: -0.015em
  title-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 14px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: 0em
  title-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.01em
  body-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
    letterSpacing: 0em
  body-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 11px
    fontWeight: '400'
    lineHeight: 16px
    letterSpacing: 0.01em
  meter-val:
    fontFamily: JetBrains Mono
    fontSize: 11px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: -0.02em
  label-xs:
    fontFamily: JetBrains Mono
    fontSize: 9px
    fontWeight: '600'
    lineHeight: 12px
    letterSpacing: 0.05em
  clock-display:
    fontFamily: JetBrains Mono
    fontSize: 18px
    fontWeight: '700'
    lineHeight: 22px
    letterSpacing: 0.02em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  hairline: 1px
  track-gap: 2px
  gutter-xs: 4px
  gutter-sm: 8px
  gutter-md: 12px
  gutter-lg: 16px
  panel-padding: 12px
  toolbar-height: 42px
  fader-width: 36px
  meter-width: 10px
---

## Brand & Style

This design system blends Windows 11 Fluent UI architecture—Mica layering, Acrylic translucent sheet depth, and micro-specular light interaction—with the hyper-focused precision of next-generation audio workstations. The brand identity radiates warmth and kinetic power, swapping clinical digital sterility for luminous thermal energy.

Key characteristics:
- **Obsidian & Mica Substrates:** Multi-tier deep carbon surfaces that harness dark translucency, subtle structural blurs, and inner light strokes rather than flat black planes.
- **Solar & Neon Luminescence:** Fiery neon orange, radiant sunset amber, and warm ember accents act as focal vectors, waveform traces, active channel highlights, and parameter tracks.
- **Surgical Instrumentation Ergonomics:** Dense data packaging designed to eliminate visual fatigue across marathon studio sessions while preserving instant legibility of high-speed metering and parameter changes.
- **Fluent Precision Edge Glows:** Sub-pixel inner highlights and localized glow blooms designate channel focus, loop boundaries, and active DSP modules.

## Colors

The color architecture is optimized for prolonged focus in light-controlled studio environments, delivering crisp contrast for multi-channel waveforms and real-time DSP telemetry.

### Tonal Foundation (Fluent Mica/Carbon)
- **Canvas Base / Mica Substrate (`#0c0e12`):** Primary window backdrop and master frame canvas.
- **Panel Base (`#14171f`):** Arranger canvas, track lanes, mixer strip troughs, and docked drawers.
- **Card / Module Base (`#1e222d`):** VST plugin containers, floating inspectors, and device racks.
- **Active / Hover Tier (`#282d3c`):** Hover states on track headers, context popovers, and selected plugin slots.

### Signal & Chromatic Brand Tokens
- **Luminous Neon Orange (`#ff6b00`):** Primary transport active indicators, selected audio track headers, automation control nodes, and active modulation sweeps.
- **Sunset Amber (`#fbbf24`):** Warm golden secondary accent. Identifies solo channel states, pre-fader listen (PFL), transient anchors, and warm drive/saturation DSP stages.
- **Deep Ember (`#ea580c`):** Tertiary operational accent for group channel routing, loop range bounds, and inactive control fills.
- **Peak Alert Red (`#ef4444`):** True-peak limit alerts (0dBFS+), destructive operations, and active record-arming.
- **Signal Nominal Green (`#22c55e`):** Safe linear operating dynamic range (-inf to -6dBFS).
- **Meter Dim Slate (`#334155`):** Unlit LED segments and inactive parameter travel guides.

## Typography

The type system is divided strictly by cognitive function: structural brand hierarchy vs. high-velocity audio telemetry.

- **Plus Jakarta Sans:** Drives all human-facing navigation, window title bars, track designations, plugin categories, and dialog interactions. The geometric yet rounded contours soften technical density without loss of structural alignment.
- **JetBrains Mono:** Dedicated to dynamic numeric feedback. Real-time values—such as sample rate, buffer size, decibel levels (-inf to +6dB), millisecond delay lengths, Hz/kHz filter cutoffs, and SMPTE/bars-beats clocks—must render in tabular monospaced glyphs to prevent spatial jitter during continuous updates.

## Layout & Spacing

Layout geometry follows a dense workstation matrix built to support multi-window tiling and high-density screen real estate.

- **Spatial Rhythm:** A strict 4px/8px micro-grid governing controls, fader banks, and knob clusters.
- **Sub-Pixel Channel Separation:** Discrete mixer channels, arrangement lanes, and plugin insert slots are segregated via 1px `hairline` dividers rather than excessive negative space, conserving horizontal space for channel count.
- **Workspace Partitioning:**
  - **Master Command Bar:** Fixed 42px top bar holding global transport, master BPM/time signature, DSP load meter, and Windows 11 Fluent window controls.
  - **Arranger / Canvas:** Fluid center area with horizontal timeline scrolling and variable vertical lane zooming.
  - **Docked Inspector & Browser:** Collapsible 260px left/right lateral drawers.
  - **Bottom Dock:** Collapsible 220px to 360px lower drawer hosting mixer channels, multi-track drum pads, or automation lane envelopes.
- **Adaptive Breakpoints:**
  - **Desktop Multi-Monitor (1920px+):** Full detached multi-window layout with floating mixer and independent VST racks.
  - **Laptop / Standard Display (1024px – 1919px):** Integrated single-frame docking with tabbed drawer navigation.
  - **Field Companion / Mobile (<1024px):** Single-view focal layout (transport top bar, single channel or timeline stack) with collapsed icon-only tool ribbons.

## Elevation & Depth

Depth is established through Fluent UI translucent material stacks and directional top-light specular cues rather than heavy traditional shadows.

- **Acrylic / Mica Layering:** 
  - Substrate base panels: `background: rgba(20, 23, 31, 0.85); backdrop-filter: blur(20px) saturate(130%);`
  - Elevated cards & racks: `background: rgba(30, 34, 45, 0.9); backdrop-filter: blur(16px);`
- **Fluent Inset Light Borders:** Elevated modules use a 1px perimeter border with an intensified top specular edge to simulate directional light:
  - `border: 1px solid rgba(255, 255, 255, 0.07);`
  - `box-shadow: inset 0 1px 0 0 rgba(255, 255, 255, 0.14);`
- **Solar Glow Elevation:** Active channels, engaged solo buttons, and focused knobs project an ambient orange bloom:
  - `box-shadow: 0 0 14px rgba(255, 107, 0, 0.28), 0 0 1px 1px rgba(255, 107, 0, 0.6);`
- **Detached Windows & Floating VST Racks:**
  - `box-shadow: 0 16px 40px rgba(0, 0, 0, 0.7), 0 0 0 1px rgba(255, 255, 255, 0.12);`

## Shapes

The system implements a refined, soft-geometry approach (Roundedness Level 1) tuned to Windows 11 Fluent specifications, combining 4px radiuses on interactive units with 8px radiuses on container modules.

- **Master Panels & VST Windows:** 8px (`rounded-lg`) corner radius.
- **Mixer Fader Knobs, Inserts, & Action Buttons:** 4px (`rounded-sm`) for compact packaging without edge collisions.
- **Meters & Signal LEDs:** 1px to 2px minimal rounding to preserve sharp boundary distinctions on peak meters.
- **Pills & Toggles:** Full pill (`rounded-full`) reserved exclusively for transport time capsules and master loop states.

## Components

### Window Chrome & Master Transport
- **Title Bar:** Integrated with the application surface. Left-aligned project metadata and main menu; center-aligned transport module featuring a high-contrast monospaced SMPTE/bars-beats counter; right-aligned Windows 11 controls (Minimize, Maximize/Restore, Close).
- **Transport Buttons:** Circular and pill toggles. The Play state illuminates in luminous neon orange (`#ff6b00`) with an ambient outer glow; Record illuminates in solid peak red (`#ef4444`).

### Buttons & Channel Strips
- **Action Buttons:** Frosted Mica-styled panels (`#1e222d`) with 1px translucent borders. On hover, background shifts to `#282d3c` with a subtle white specular highlight.
- **Mute / Solo Micro-Buttons:** Compact 20px × 20px squares. Active Mute glows deep ember (`#ea580c`) with black text; active Solo glows radiant sunset amber (`#fbbf24`) with high-contrast obsidian typography.
- **Track Selection Header:** Active selection applies a left 3px indicator border in `#ff6b00` and a subtle horizontal gradient wash (`rgba(255, 107, 0, 0.08)`).

### Sliders, Rotary Knobs & Potentiometers
- **Rotary Dials:** Circular vector knobs with a 270° sweep. The inactive track uses `#334155`, while the active value arc features an energetic gradient from `#ea580c` to `#ff6b00`. Hover reveals numeric decibel/frequency readout centered over the dial in JetBrains Mono.
- **Console Faders:** Vertical channel faders feature an 8px recessed slot (`#0c0e12`) and a physical-feel thumb cap (`#282d3c`) with a central glowing neon orange center notch.

### Audio Level Meters
- **Meter Geometry:** 8px to 10px wide segmented LED tracks with 1px segment separation.
- **Dynamic Response Scale:**
  - Range -inf to -6dBFS: Nominal signal green (`#22c55e`).
  - Range -6dBFS to -0.1dBFS: Sunset amber (`#fbbf24`).
  - Range >= 0dBFS: Peak alert red (`#ef4444`) with 1.2-second peak-hold register.

### Input Fields & Parameter Readouts
- **Value Inputs:** Monospaced inputs styled in `#14171f` with 1px border (`rgba(255, 255, 255, 0.08)`). On focus, the border shifts to neon orange (`#ff6b00`) accompanied by a 2px outer ambient blur.
- **Preset Search & Dropdowns:** Filterable text fields with right-aligned chevron icons, utilizing backdrop blur on popover menus.

### Cards & VST Containers
- **Plugin Module Racks:** Acrylic card structures featuring an integrated header bar with module bypass toggle, preset recall dropdown, and input/output gain trimmers.