import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Testing Library only registers its own cleanup when Vitest runs with globals enabled, and
// this config does not. Doing it explicitly is the honest fix: without it, one test's markup
// is still in the document when the next one queries, so any assertion about what is *absent*
// is really reading the previous test's output.
afterEach(cleanup);
