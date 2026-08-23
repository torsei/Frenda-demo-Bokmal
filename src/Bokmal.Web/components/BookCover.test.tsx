import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { BookCover } from './BookCover';

describe('BookCover', () => {
  it('is announced by title and author rather than as decoration', () => {
    render(<BookCover title="Dune" author="Frank Herbert" genre="Science Fiction & Fantasy" slug="dune" />);

    expect(screen.getByRole('img', { name: 'Dune by Frank Herbert' })).toBeInTheDocument();
  });

  it('breaks a long title across lines instead of running it off the cover', () => {
    // SVG has no text wrapping, so the component does it. Without this a long title is a
    // single line that leaves the artwork entirely.
    const { container } = render(
      <BookCover
        title="The Left Hand of Darkness"
        author="Ursula K. Le Guin"
        genre="Science Fiction & Fantasy"
        slug="the-left-hand-of-darkness"
      />,
    );

    const lines = [...container.querySelectorAll('text')].map((t) => t.textContent);

    expect(lines.length).toBeGreaterThan(2);
    expect(lines.join(' ')).toContain('THE LEFT HAND');
  });

  it('gives the same book the same cover every time', () => {
    // A cover that changed between renders would be worse than no cover at all.
    const first = render(<BookCover title="Dune" author="F. H." genre="Crime" slug="dune" />);
    const firstFill = first.container.querySelector('rect')?.getAttribute('fill');
    first.unmount();

    const second = render(<BookCover title="Dune" author="F. H." genre="Crime" slug="dune" />);
    const secondFill = second.container.querySelector('rect')?.getAttribute('fill');

    expect(firstFill).toBe(secondFill);
  });

  it('gives books of different genres different palettes', () => {
    const crime = render(<BookCover title="X" author="Y" genre="Crime" slug="x" />);
    const crimeFill = crime.container.querySelector('rect')?.getAttribute('fill');
    crime.unmount();

    const classics = render(<BookCover title="X" author="Y" genre="Classics" slug="x" />);
    const classicsFill = classics.container.querySelector('rect')?.getAttribute('fill');

    expect(crimeFill).not.toBe(classicsFill);
  });
});
