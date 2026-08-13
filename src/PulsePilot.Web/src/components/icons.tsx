type IconProps = { name: "dashboard" | "feedback" | "actions" | "backlog" | "copilot" | "spark" | "arrow" };

export function Icon({ name }: IconProps) {
  const paths = {
    dashboard: <><rect x="3" y="3" width="7" height="7" rx="2" /><rect x="14" y="3" width="7" height="4" rx="2" /><rect x="14" y="11" width="7" height="10" rx="2" /><rect x="3" y="14" width="7" height="7" rx="2" /></>,
    feedback: <><path d="M20 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h9a4 4 0 0 1 4 4Z" /><path d="M8 9h7M8 13h4" /></>,
    actions: <><path d="M5 3h14v18H5z" /><path d="m8 9 2 2 5-5M8 16h8" /></>,
    backlog: <><path d="M4 7h16M4 12h16M4 17h10" /><circle cx="20" cy="17" r="2" /></>,
    copilot: <><path d="M12 3 9.5 8.5 4 11l5.5 2.5L12 19l2.5-5.5L20 11l-5.5-2.5Z" /><path d="m19 3-.8 1.8L16.5 6l1.7.8L19 8.5l.8-1.7L21.5 6l-1.7-1.2Z" /></>,
    spark: <><path d="m12 2 2.4 6.6L21 11l-6.6 2.4L12 20l-2.4-6.6L3 11l6.6-2.4Z" /></>,
    arrow: <><path d="M5 12h14M14 7l5 5-5 5" /></>,
  };

  return <svg className="ui-icon" viewBox="0 0 24 24" aria-hidden="true">{paths[name]}</svg>;
}
