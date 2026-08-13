import { PulseMark } from "@/components/brand/pulse-mark";

export default function AuthLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <main className="auth-shell">
      <section className="auth-story" aria-label="PulsePilot product overview">
        <PulseMark />
        <div className="auth-story-copy">
          <p className="eyebrow eyebrow-light">Product intelligence, in motion</p>
          <h1>Turn every customer signal into an engineering decision.</h1>
          <p>
            PulsePilot groups feedback, surfaces urgency, and keeps every action
            grounded in human approval.
          </p>
        </div>
        <div className="signal-preview" aria-hidden="true">
          <div className="signal-preview-head">
            <span>Signal health</span>
            <span className="live-badge"><i /> Live</span>
          </div>
          <div className="signal-bars">
            {[34, 52, 44, 71, 58, 82, 68, 92, 78, 96].map((height, index) => (
              <i key={index} style={{ height: `${height}%` }} />
            ))}
          </div>
          <div className="signal-preview-foot">
            <span>1,284 signals analyzed</span>
            <strong>+18.4%</strong>
          </div>
        </div>
        <p className="auth-story-foot">Built for product and engineering teams</p>
      </section>
      <section className="auth-panel">{children}</section>
    </main>
  );
}
