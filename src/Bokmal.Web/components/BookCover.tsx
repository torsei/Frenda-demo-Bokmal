/**
 * A generated cover.
 *
 * Real cover art is copyrighted, and pulling it from a cover API would add a network
 * dependency that breaks the app when it is offline or rate-limited. Drawing one instead
 * costs nothing, always renders, and looks deliberate rather than borrowed.
 *
 * Deterministic: the same book always gets the same cover, because everything varying is
 * derived from the slug rather than from chance. A cover that changed on every render would
 * be worse than no cover at all.
 */

type Palette = {
  background: string;
  band: string;
  ink: string;
  accent: string;
};

const PALETTES: Record<string, Palette[]> = {
  'Science Fiction & Fantasy': [
    { background: '#2a2a5e', band: '#3d3d82', ink: '#f0eefc', accent: '#8f8ce0' },
    { background: '#1f3b5c', band: '#2d5480', ink: '#eaf2fa', accent: '#78a9d8' },
  ],
  Crime: [
    { background: '#2c2321', band: '#43332f', ink: '#f5ece8', accent: '#c4685a' },
    { background: '#3a2020', band: '#552e2e', ink: '#f7ebe8', accent: '#d08072' },
  ],
  'Literary Fiction': [
    { background: '#25423c', band: '#345c53', ink: '#eef6f2', accent: '#7fb8a4' },
    { background: '#2f4340', band: '#425f5a', ink: '#f0f5f3', accent: '#95b9b0' },
  ],
  Classics: [
    { background: '#4a3520', band: '#63482c', ink: '#faf3e8', accent: '#c9a878' },
    { background: '#553d28', band: '#6f5236', ink: '#fbf4ea', accent: '#d4b183' },
  ],
  'Non-fiction': [
    { background: '#26333f', band: '#354756', ink: '#eef3f7', accent: '#88a8c0' },
    { background: '#2f3a33', band: '#425046', ink: '#f0f4f1', accent: '#9ab4a3' },
  ],
};

const FALLBACK: Palette[] = [
  { background: '#33302b', band: '#474239', ink: '#f4f1ec', accent: '#b9ac97' },
];

/** Small stable hash so a book's cover never changes between renders or machines. */
function hash(value: string): number {
  let result = 0;
  for (let i = 0; i < value.length; i++) {
    result = (result * 31 + value.charCodeAt(i)) >>> 0;
  }
  return result;
}

/** SVG has no text wrapping, so lines are worked out here. */
function wrap(text: string, maxChars: number, maxLines: number): string[] {
  const lines: string[] = [];
  let line = '';

  for (const word of text.split(' ')) {
    const candidate = line ? `${line} ${word}` : word;

    if (candidate.length <= maxChars) {
      line = candidate;
      continue;
    }

    if (line) lines.push(line);
    line = word;

    if (lines.length === maxLines - 1) break;
  }

  if (line && lines.length < maxLines) lines.push(line);

  return lines;
}

export function BookCover({
  title,
  author,
  genre,
  slug,
  className,
}: {
  title: string;
  author: string;
  genre: string;
  slug: string;
  className?: string;
}) {
  const seed = hash(slug);
  const options = PALETTES[genre] ?? FALLBACK;
  const palette = options[seed % options.length];

  const titleLines = wrap(title.toUpperCase(), 12, 4);

  // Nudged per book so a shelf of covers does not look like one template repeated.
  const bandOffset = 58 + (seed % 5) * 7;

  return (
    <svg
      viewBox="0 0 200 300"
      className={className}
      role="img"
      aria-label={`${title} by ${author}`}
      preserveAspectRatio="xMidYMid slice"
    >
      <rect width="200" height="300" fill={palette.background} />

      {/* A spine, so the shape reads as a book rather than a coloured rectangle. */}
      <rect width="10" height="300" fill="#000" opacity="0.28" />
      <rect x="10" width="2" height="300" fill={palette.accent} opacity="0.5" />

      {/* Two rules near the top and the title hung beneath them: the layout most cloth
          hardbacks settle on, and it fills the space better than centring everything. */}
      <rect x="26" y={bandOffset} width="148" height="3" fill={palette.accent} opacity="0.85" />
      <rect x="26" y={bandOffset + 7} width="148" height="1" fill={palette.accent} opacity="0.5" />

      <g fill={palette.ink} fontFamily="Georgia, 'Times New Roman', serif">
        {titleLines.map((line, index) => (
          <text
            key={line + index}
            x="26"
            y={bandOffset + 40 + index * 22}
            fontSize="18"
            letterSpacing="0.5"
          >
            {line}
          </text>
        ))}

        <text x="26" y="268" fontSize="12" opacity="0.8" letterSpacing="0.6">
          {author.length > 24 ? `${author.slice(0, 23)}…` : author}
        </text>
      </g>

      <rect x="26" y="248" width="40" height="1.5" fill={palette.accent} opacity="0.8" />
    </svg>
  );
}
