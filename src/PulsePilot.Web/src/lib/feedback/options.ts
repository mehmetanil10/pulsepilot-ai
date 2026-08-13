export const feedbackSources = [
  ["manual", "Manual"],
  ["email", "Email"],
  ["support", "Support"],
  ["survey", "Survey"],
  ["api", "API"],
  ["appReview", "App review"],
] as const;

export const processingStatuses = [
  ["pending", "Pending"],
  ["processing", "Processing"],
  ["completed", "Completed"],
  ["failed", "Failed"],
] as const;

export const feedbackCategories = [
  ["bug", "Bug"],
  ["featureRequest", "Feature request"],
  ["complaint", "Complaint"],
  ["question", "Question"],
  ["praise", "Praise"],
  ["other", "Other"],
] as const;

export const feedbackComponents = [
  ["payments", "Payments"],
  ["authentication", "Authentication"],
  ["dashboard", "Dashboard"],
  ["reporting", "Reporting"],
  ["mobile", "Mobile"],
  ["api", "API"],
  ["performance", "Performance"],
  ["general", "General"],
] as const;

export const feedbackSentiments = [
  ["positive", "Positive"],
  ["neutral", "Neutral"],
  ["negative", "Negative"],
] as const;
