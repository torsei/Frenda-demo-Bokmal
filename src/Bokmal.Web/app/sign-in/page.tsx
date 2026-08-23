import { api } from '@/lib/api';
import { SignInForm } from './SignInForm';
import { Mascot } from '@/components/Mascot';
import { MemberList } from './MemberList';

export default async function SignInPage() {
  const client = await api();
  const { data } = await client.getApiBorrowers();

  const members = data ?? [];

  return (
    <div className="mx-auto max-w-md">
      <Mascot size={200} className="mx-auto h-44 w-44 object-contain" priority />

      <h1 className="mt-4 text-2xl font-semibold tracking-tight">Sign in</h1>
      <p className="mt-2 text-muted">
        Enter the address you are a member under. There is no password — this stands in for a
        real login so the app knows whose loans to show.
      </p>

      <SignInForm />

      {members.length > 0 ? (
        <div className="mt-10">
          <MemberList members={members.slice(0, 8)} />
          {members.length > 8 ? (
            <p className="mt-2 px-2 text-xs text-muted">
              &hellip;and {members.length - 8} more.
            </p>
          ) : null}
        </div>
      ) : null}

    </div>
  );
}
