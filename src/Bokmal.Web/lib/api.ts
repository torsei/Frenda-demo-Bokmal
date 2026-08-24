import 'server-only';

import { createClient, createConfig } from '@/generated/api/client';
import { Sdk } from '@/generated/api/sdk.gen';
import { currentBorrowerEmail } from './session';

const API_BASE_URL = process.env.BOKMAL_API_URL ?? 'http://localhost:5080';

/**
 * The header the API identifies the borrower from.
 *
 * Half of a contract whose other half is HeaderCurrentBorrower.HeaderName in the C# project.
 * Nothing can enforce agreement across the language boundary -- headers are not part of the
 * OpenAPI schema we generate from -- so this is the one string in the frontend that has to be
 * kept in step by hand. Named rather than inlined so it is findable when that day comes.
 */
const BORROWER_HEADER = 'X-Borrower-Email';

/**
 * The backend, typed.
 *
 * Everything under generated/ comes from the API's OpenAPI document, so renaming a field on
 * a C# DTO breaks the build here instead of quietly arriving as `undefined` in a component.
 * Regenerate with `npm run generate-api`.
 *
 * Marked server-only, and that is the whole architecture in one line: every call happens on
 * the Next server, never from the browser. Two things follow. The API needs no CORS
 * configuration, because no browser ever talks to it directly. And the borrower's identity
 * travels from an httpOnly cookie straight into a request header without passing through
 * any code the user could tamper with.
 */
export async function api(): Promise<Sdk> {
  const email = await currentBorrowerEmail();

  const client = createClient(
    createConfig({
      baseUrl: API_BASE_URL,
      headers: email ? { [BORROWER_HEADER]: email } : undefined,
      // Loan state changes under the user's feet -- another borrower can take the last copy
      // between a page render and a click. Nothing here is cacheable.
      cache: 'no-store',
    }),
  );

  return new Sdk({ client });
}
