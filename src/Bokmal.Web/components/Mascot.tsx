import Image from 'next/image';

/**
 * The bookmal himself. A bokmal is a bookworm, and he is the only illustration in the app --
 * used sparingly, where a page would otherwise be empty or purely functional.
 */
export function Mascot({
  size,
  className,
  priority,
}: {
  size: number;
  className?: string;
  priority?: boolean;
}) {
  return (
    <Image
      src="/bokmal.png"
      alt=""
      width={size}
      height={size}
      className={className}
      priority={priority}
      // Decorative: the surrounding text already says everything he does, and announcing
      // "illustration of an old man reading" to a screen reader adds nothing but noise.
      aria-hidden
    />
  );
}
