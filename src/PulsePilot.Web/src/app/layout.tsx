import type { Metadata, Viewport } from "next";

import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "PulsePilot",
    template: "%s · PulsePilot",
  },
  description: "AI-driven product feedback and engineering copilot.",
};

export const viewport: Viewport = {
  colorScheme: "light",
  themeColor: "#f5f7f2",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
