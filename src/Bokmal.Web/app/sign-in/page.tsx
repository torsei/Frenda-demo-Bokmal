import { api } from '@/lib/api';
import { SignInForm } from './SignInForm';
import { Mascot } from '@/components/Mascot';
import { CopyEmailButton } from './CopyEmailButton';

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
          <h2 className="text-sm font-medium text-muted">Members you can sign in as</h2>
          <ul className="mt-3 grid gap-0.5 text-sm">
            {members.slice(0, 8).map((member) => (
              <li key={member.id} className="flex items-center justify-between gap-4">
                <span>{member.displayName}</span>
                <span className="flex items-center gap-1">
                  <span className="text-muted">{member.email}</span>
                  <CopyEmailButton email={member.email} />
                </span>
              </li>
            ))}
          </ul>
          {members.length > 8 ? (
            <p className="mt-2 text-xs text-muted">
              …and {members.length - 8} more.
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
