import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AvailabilityLine, ReadingTimeLine } from './BookCard';
import type { AvailabilityDto, ReadingTimeDto } from '@/generated/api/types.gen';

function availability(total: number, available: number): AvailabilityDto {
  return {
    totalCopies: total,
    availableCopies: available,
    onLoanCopies: total - available,
    isAvailable: available > 0,
  };
}

function readingTime(days: number, loans: number, fromHistory: boolean): ReadingTimeDto {
  return { typicalDays: days, basedOnLoans: loans, fromHistory };
}

describe('AvailabilityLine', () => {
  it('says how many copies are on the shelf and how many are out', () => {
    render(<AvailabilityLine availability={availability(4, 2)} />);

    expect(screen.getByText(/2 of 4 on the shelf/)).toBeInTheDocument();
    expect(screen.getByText(/2 out/)).toBeInTheDocument();
  });

  it('does not mention loans when the whole shelf is there', () => {
    render(<AvailabilityLine availability={availability(3, 3)} />);

    expect(screen.getByText('3 of 3 on the shelf')).toBeInTheDocument();
    expect(screen.queryByText(/out/)).not.toBeInTheDocument();
  });

  it('leads with the bad news when nothing is available', () => {
    // A borrower scanning the catalogue needs to see this without doing arithmetic on
    // "0 of 3", which reads as a number rather than as a closed door.
    render(<AvailabilityLine availability={availability(3, 0)} />);

    expect(screen.getByText('All 3 copies out')).toBeInTheDocument();
  });

  it('counts a single copy in the singular', () => {
    render(<AvailabilityLine availability={availability(1, 0)} />);

    expect(screen.getByText('All 1 copy out')).toBeInTheDocument();
  });
});

describe('ReadingTimeLine', () => {
  it('states the figure plainly when it rests on real borrowing', () => {
    render(<ReadingTimeLine readingTime={readingTime(12, 28, true)} />);

    expect(screen.getByText(/Usually out 12 days/)).toBeInTheDocument();
    expect(screen.getByText(/28 readers/)).toBeInTheDocument();
  });

  it('hedges, and says why, when it is a guess from the page count', () => {
    // The distinction is the whole point of the API sending basedOnHistory. Presenting a
    // guess in the same words as a median over thirty readers would be a small lie.
    render(<ReadingTimeLine readingTime={readingTime(9, 1, false)} />);

    expect(screen.getByText(/Roughly 9 days, going by length/)).toBeInTheDocument();
    expect(screen.queryByText(/readers/)).not.toBeInTheDocument();
  });

  it('counts a single day in the singular', () => {
    render(<ReadingTimeLine readingTime={readingTime(1, 40, true)} />);

    expect(screen.getByText(/Usually out 1 day\b/)).toBeInTheDocument();
  });
});
