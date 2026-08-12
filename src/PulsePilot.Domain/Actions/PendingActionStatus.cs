namespace PulsePilot.Domain.Actions;

public enum PendingActionStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Executed = 4,
    Failed = 5,
}
