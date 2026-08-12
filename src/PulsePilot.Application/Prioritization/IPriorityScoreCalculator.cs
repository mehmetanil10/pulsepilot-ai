namespace PulsePilot.Application.Prioritization;

public interface IPriorityScoreCalculator
{
    PriorityScoringResult Calculate(
        IReadOnlyCollection<PriorityScoringMember> members,
        DateTimeOffset calculatedAt);
}
