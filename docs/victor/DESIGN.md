# FerramentaEMT DESIGN.md

## Purpose

FerramentaEMT is a Revit add-in for structural modeling, documentation, fabrication, QA, and precast workflows.
Its UI must feel like a professional engineering tool used inside a production BIM environment.

This is not a marketing product, consumer app, or playful dashboard.
It should feel:

- technical
- clear
- compact
- dependable
- fast to scan
- calm under long work sessions

The UI should help engineers complete tasks with confidence and minimal noise.

## Product Personality

- Quiet, practical, and trustworthy
- More like a CAD/BIM control surface than a website
- Dense where needed, but never cluttered
- Friendly without becoming decorative
- Strong visual hierarchy, minimal ornament

## Core Principles

1. Optimize for execution, not spectacle.
2. Every window should help the user finish a modeling or documentation task quickly.
3. Use stable, repeatable patterns across all dialogs.
4. Prefer consistency over novelty.
5. Show important states clearly: warning, confirmation, next action, current selection, preview.

## Visual Direction

### Overall Feel

- Desktop utility aesthetic
- Neutral surfaces
- Clear borders
- Soft corner radius
- High legibility
- No gradients, glass, blur, or decorative hero styling

### Layout

- Use compact dialog layouts with predictable spacing
- Default dialog content uses a single main column with grouped sections
- Use `GroupBox`, bordered summary cards, and action bars for structure
- Keep primary actions at the bottom-right
- Keep destructive or cancel actions visually secondary

### Density

- Moderate density
- Enough spacing to avoid fatigue
- Tight enough to avoid wasted space in Revit workflows

Target feel:

- more Linear/desktop utility than consumer SaaS
- more technical panel than editorial composition

## Color System

FerramentaEMT already supports light and dark themes through resource dictionaries.
Any new UI work should extend those theme resources instead of hardcoding colors.

### Existing Window Theme Direction

Light theme:

- background: cool light gray
- surfaces: white / near-white
- borders: soft neutral gray
- text: dark slate
- warning: warm amber
- accent cards: muted blue

Dark theme:

- background: deep charcoal
- surfaces: layered dark gray
- borders: muted blue-gray
- text: near-white
- warning: warm amber
- accent cards: muted steel-blue

### Accent Rules

There are two accent roles in the product:

1. Dialog/action accent
   - Keep the current warm amber accent for primary call-to-action buttons, warnings, and operational emphasis in WPF dialogs.
   - This works well for confirm/execute flows inside forms.

2. Ribbon/icon accent
   - Ribbon PNG icons should use a single cool blue accent.
   - Current standard: `#6BB7FF`
   - All ribbon icons should be monochrome tinted with this same blue while preserving transparency.

Do not mix multiple icon colors in the ribbon.
Do not use multicolor icons.

## Iconography

### Ribbon Icons

- Format: PNG with transparent background
- Sizes:
  - large: `32x32`
  - small: `16x16`
- Style:
  - simple
  - readable at small sizes
  - single-color tint
  - no shadows
  - no bevel
  - no photorealism

Reference implementation:

- source family: Lucide
- source repo: `lucide-icons/lucide`
- rasterized into local PNG assets for Revit ribbon usage
- canonical mapping file: `Resources/icon-manifest.json`

### Icon Standard

- Use one visual family across all tools
- Prefer clear silhouette over detail
- Avoid mixing outline-heavy and filled-heavy icons unless they still read as one family
- Keep stroke weight visually similar across icons
- Reuse an icon only when the tool meaning is truly shared

### Semantic Guidance

- modeling tools: structural/object icons
- view tools: isolate, visibility, grouping, preview icons
- documentation tools: tag, dimension, sheet, export, identify icons
- fabrication/CNC tools: part, cut, mark, export, connection icons
- QA tools: inspect, validate, checklist icons
- PF tools: keep concrete/rebar metaphors distinct from steel modeling tools

Current semantic baseline for FerramentaEMT ribbon:

- steel beam / steel profile tools: W-profile
- export and verification workflows: spreadsheet metaphor
- cut / split / section operations: scissors
- rebar tools: grid metaphor
- pillar tools: vertical rectangle
- concrete beam tools: horizontal rectangle
- pipe rack: tubes / pipe bundle
- truss tools: truss silhouette
- bracing tools: X-brace

## Typography

- Use `Segoe UI` as the default UI font
- Keep typography functional and native to Windows desktop tooling
- Dialog title: prominent but not oversized
- Labels: compact and clear
- Secondary text: muted, readable, concise
- Use `Consolas` only for machine IDs, file paths, codes, or generated identifiers

Do not use expressive display typography.
Do not use tight or decorative letter spacing.

## Shape and Surface Rules

- Buttons: 8px corner radius
- Cards and bordered panels: 10px corner radius
- GroupBox body: 12px corner radius
- Borders should be subtle but always present enough to separate controls from surface

Avoid:

- oversized rounding
- floating card stacks inside card stacks
- ornamental pills
- shadow-heavy depth effects

## Components

### Buttons

- Primary action: warm amber fill, high emphasis
- Secondary action: neutral raised surface
- Cancel buttons remain secondary
- Button labels should be verb-led and direct

Examples:

- `Gerar`
- `Aplicar`
- `Exportar`
- `Validar`
- `Cancelar`

### Forms

- Inputs should align cleanly in grids
- Labels should be short and practical
- Related options belong inside one section
- Use checkboxes for optional behaviors
- Use combo boxes for type/family/parameter selection
- Numeric input fields should stay compact

### Cards

Use bordered cards for:

- warnings
- previews
- result summaries
- highlighted next-step information

Accent cards are allowed for “important but not dangerous” information.
Warning cards are for validation risks, missing configuration, or execution caveats.

### Action Bars

- Place at the bottom of dialogs
- Separate from content with a top border
- Keep action order predictable
- Primary action should be the visual endpoint

## Window Behavior

- Windows should open with the minimum size necessary for the task
- Avoid oversized blank areas
- Long instructions should be split into title, short subtitle, and grouped controls
- Use progressive disclosure when a command has advanced options
- Prefer clarity over squeezing too much into one window

## Content Style

- Use direct engineering language
- Prefer precise nouns over promotional language
- Keep descriptions short and operational
- Tell the user what the command does, what it needs, and what it outputs

Good tone:

- objective
- concise
- instructive
- respectful

Avoid:

- hype
- playful filler
- vague “smart” or “powerful” claims

## Theme Implementation Rules

- Always use dynamic resources from the theme dictionaries
- Extend:
  - `AppTheme.Base.xaml`
  - `AppTheme.Light.xaml`
  - `AppTheme.Dark.xaml`
- Do not hardcode colors in new XAML unless there is a strong technical reason
- Reuse existing styles before creating new ones

Preferred existing style primitives:

- `PrimaryActionButton`
- `SecondaryActionButton`
- `SubtleCardBorder`
- `WarningCardBorder`
- `AccentCardBorder`
- `ActionBarBorder`
- `DialogTitleText`
- `DialogSubtitleText`
- `SectionBodyText`

## Consistency Rules For New Work

When creating a new command window:

1. Use the shared theme resources
2. Start with:
   - title
   - short subtitle
   - grouped form sections
   - optional status/warning card
   - bottom action bar
3. Keep spacing aligned with existing dialogs
4. Match button styles and control heights already used in the project
5. Use the ribbon icon family and tint standard

## Anti-Patterns

Do not introduce:

- gradient-heavy UI
- bright multicolor accents
- glassmorphism
- marketing-style cards
- giant headers
- decorative illustrations
- deeply nested framed containers
- inconsistent icon sets
- random hardcoded colors

## Design Intent Summary

FerramentaEMT should look like a serious structural engineering extension for Revit:

- clean
- precise
- theme-aware
- blue-standardized in ribbon icons
- amber-accented in dialog actions
- consistent across modeling, documentation, fabrication, QA, and PF modules

If a new screen or icon feels flashy, consumer-like, or visually louder than the work itself, it is off-pattern.
