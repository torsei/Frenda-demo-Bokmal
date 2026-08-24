/**
 * A generated cover.
 *
 * Real cover art is copyrighted, and pulling it from a cover API would add a network
 * dependency that breaks the app when it is offline or rate-limited. Drawing one instead
 * costs nothing, always renders, and looks deliberate rather than borrowed.
 *
 * Deterministic: the same book always gets the same cover, because everything varying is
 * derived from the slug and the genre rather than from chance. A cover that changed on every
 * render would be worse than no cover at all.
 */

type Palette = {
  background: string;
  ink: string;
  accent: string;
};

/**
 * The colour a genre gets, derived from its name.
 *
 * This used to be a lookup table keyed on the genre strings, which meant the frontend had to
 * know every genre the backend does. That is a duplication with the worst possible failure
 * mode: rename a genre server-side and nothing breaks, the covers just quietly go grey and
 * nobody notices for six months.
 *
 * Deriving the hue instead removes the coupling entirely. A genre the frontend has never
 * heard of still gets a stable, distinct colour, and adding one to the library needs no
 * frontend change at all.
 *
 * The honest version of this is a genre table, with the colour on the row and an admin
 * screen to set it -- a colour is an attribute of a genre, and a librarian is the right
 * person to choose it. That is a table, a foreign key and a screen more than this exercise
 * calls for, so the colour is computed here instead and the catalogue keeps genre as a plain
 * label. Nothing about this design blocks that change later.
 *
 * Saturation and lightness are fixed so every cover belongs to the same family however many
 * genres appear. Two genres can land on neighbouring hues; the genre is written on the card
 * beside the cover, so the colour is decoration rather than the only signal.
 */
function paletteFor(genre: string): Palette {
  const hue = hash(genre) % 360;

  return {
    background: `hsl(${hue} 24% 21%)`,
    ink: `hsl(${hue} 16% 93%)`,
    accent: `hsl(${hue} 44% 68%)`,
  };
}

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
  const palette = paletteFor(genre);
  const titleLines = wrap(title.toUpperCase(), 12, 4);

  // Nudged per book so a shelf of covers does not look like one template repeated.
  const bandOffset = 58 + (hash(slug) % 5) * 7;

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
