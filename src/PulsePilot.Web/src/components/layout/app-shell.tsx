import type { BackendCurrentUserResponse } from "@/types/auth";
import { PulseMark } from "@/components/brand/pulse-mark";
import { AppNavigation } from "@/components/navigation/app-navigation";
import { LogoutButton } from "@/components/auth/logout-button";

type AppShellProps = {
  user: BackendCurrentUserResponse;
  children: React.ReactNode;
};

export function AppShell({ user, children }: AppShellProps) {
  const initials = user.displayName
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();

  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <PulseMark />
        <AppNavigation />
        <div className="sidebar-foot">
          <div className="workspace-pill">
            <span>{initials || "PP"}</span>
            <div>
              <strong>{user.displayName}</strong>
              <small>{user.role}</small>
            </div>
          </div>
          <LogoutButton />
        </div>
      </aside>
      <div className="app-stage">
        <header className="mobile-header">
          <PulseMark />
          <span className="mobile-avatar">{initials || "PP"}</span>
        </header>
        {children}
      </div>
    </div>
  );
}
