/**
 * Delta brand triangle mark, rendered as an inline SVG using CSS variables
 * for consistent theming. Replaces the old CSS border-triangle hack so the mark
 * is pixel-consistent and crisp at any zoom level, wherever it's used.
 *
 * The rectangle uses --color-primary (#2563EB in light, #60A5FA in dark) and
 * the triangle uses --color-on-primary (white in light, #0F172A in dark) for
 * optimal contrast and legibility in both themes.
 */
function LogoMark({ size = 36 }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 64 64"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      aria-label="Delta"
    >
      <rect width="64" height="64" rx="16" fill="var(--color-primary)" />
      <path d="M32 16 L48 46 L16 46 Z" fill="var(--color-on-primary)" />
    </svg>
  );
}

export default LogoMark;
