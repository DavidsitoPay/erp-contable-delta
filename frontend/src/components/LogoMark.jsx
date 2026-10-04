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
