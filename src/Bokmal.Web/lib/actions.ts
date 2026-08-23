'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { api } from './api';
import { problemMessage } from './problem';
import { clearCurrentBorrower, setCurrentBorrowerEmail } from './session';

export type ActionResult = { error: string } | { ok: true };

export async function signIn(formData: FormData): Promise<ActionResult> {
  const email = String(formData.get('email') ?? '').trim().toLowerCase();

  if (!email) return { error: 'Enter the address you are a member under.' };

  const client = await api();
  const { data, error } = await client.postApiSession({ body: { email } });

  if (error || !data) {
    return { error: problemMessage(error, 'That address is not a library member.') };
  }

  await setCurrentBorrowerEmail(data.email);
  redirect('/');
}

export async function signOut(): Promise<void> {
  await clearCurrentBorrower();
  redirect('/sign-in');
}

export async function borrow(bookSlug: string): Promise<ActionResult> {
  const client = await api();
  const { error } = await client.postApiLoans({ body: { bookSlug } });

  if (error) {
    return { error: problemMessage(error, 'The book could not be borrowed.') };
  }

  // Availability changed, so anything showing a copy count is now stale: this book's page,
  // the catalogue, the top list and the borrower's own loans.
  revalidatePath('/', 'layout');

  return { ok: true };
}

export async function returnLoan(loanId: string): Promise<ActionResult> {
  const client = await api();
  const { error } = await client.postApiLoansByLoanIdReturn({ path: { loanId } });

  if (error) {
    return { error: problemMessage(error, 'The book could not be returned.') };
  }

  revalidatePath('/', 'layout');

  return { ok: true };
}
