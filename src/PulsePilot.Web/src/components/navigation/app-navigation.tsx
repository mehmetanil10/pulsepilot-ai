"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

import { Icon } from "@/components/icons";

const items = [
  { label: "Dashboard", href: "/dashboard", icon: "dashboard" as const, ready: true },
  { label: "Feedback", href: "/feedback", icon: "feedback" as const, ready: true },
  { label: "Actions", href: "/actions", icon: "actions" as const, ready: true },
  { label: "Backlog", href: "/backlog", icon: "backlog" as const, ready: false },
  { label: "Copilot", href: "/copilot", icon: "copilot" as const, ready: false },
];

export function AppNavigation() {
  const pathname = usePathname();

  return (
    <nav className="app-navigation" aria-label="Workspace navigation">
      <p>Workspace</p>
      {items.map((item) => {
        const contents = (
          <>
            <Icon name={item.icon} />
            <span>{item.label}</span>
            {!item.ready && <small>Soon</small>}
          </>
        );

        return item.ready ? (
          <Link
            className={pathname.startsWith(item.href) ? "active" : undefined}
            href={item.href}
            key={item.href}
          >
            {contents}
          </Link>
        ) : (
          <span className="nav-disabled" aria-disabled="true" key={item.href}>
            {contents}
          </span>
        );
      })}
    </nav>
  );
}
