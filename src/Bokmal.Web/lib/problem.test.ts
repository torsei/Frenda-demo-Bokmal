import { describe, expect, it } from 'vitest';
import { problemMessage } from './problem';

describe('problemMessage', () => {
  it('passes the API\u2019s own sentence through', () => {
    const detail = 'Every copy of this book is currently on loan. Try again once one comes back.';

    expect(problemMessage({ title: 'All copies are out', detail }, 'fallback')).toBe(detail);
  });

  it.each([
    ['nothing at all', undefined],
    ['a null', null],
    ['an unrelated shape', { message: 'boom' }],
    ['an empty detail', { detail: '' }],
    ['a whitespace detail', { detail: '   ' }],
    ['a non-string detail', { detail: 404 }],
  ])('falls back when the error is %s', (_name, error) => {
    expect(problemMessage(error, 'The book could not be borrowed.')).toBe(
      'The book could not be borrowed.',
    );
  });
});
