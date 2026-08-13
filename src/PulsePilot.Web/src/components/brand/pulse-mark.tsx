type PulseMarkProps = {
  compact?: boolean;
};

export function PulseMark({ compact = false }: PulseMarkProps) {
  return (
    <span className="brand-mark" aria-label="PulsePilot">
      <span className="brand-symbol" aria-hidden="true">
        <svg viewBox="0 0 32 32" role="img">
          <path d="M3 17h5l3-8 5 15 4-11 3 4h6" />
        </svg>
      </span>
      {!compact && (
        <span className="brand-wordmark">
          Pulse<span>Pilot</span>
        </span>
      )}
    </span>
  );
}
