import { useId } from "react";

/**
 * Delta brand triangle mark, rendered as an inline SVG using the same
 * gradient rounded-rect + triangle path as the favicon in index.html.
 * Replaces the old CSS border-triangle hack so the mark is pixel-consistent
 * and crisp at any zoom level, wherever it's used.
 */
function LogoMark({ size = 36 }) {
  const gradientId = useId();

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 64 64"
      xmlns="http://www.w3.org/2000/svg"
      role="img"
      aria-label="Delta"
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#4f7fff" />
          <stop offset="100%" stopColor="#8b5cf6" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" rx="16" fill={`url(#${gradientId})`} />
      <path d="M32 16 L48 46 L16 46 Z" fill="#f8fafc" />
    </svg>
  );
}

export default LogoMark;
