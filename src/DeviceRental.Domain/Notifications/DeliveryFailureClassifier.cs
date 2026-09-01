using DeviceRental.Domain.Common;

namespace DeviceRental.Domain.Notifications;

public enum DeliveryFailureDisposition
{
    None,
    Retry,
    DeadLetter,
    ManualReview,
}

public static class DeliveryFailureClassifier
{
    public static DeliveryFailureDisposition Classify(
        NotificationSendOutcome outcome,
        SmtpAcceptanceEvidence acceptanceEvidence)
    {
        DomainGuard.DefinedEnum(outcome, nameof(outcome));
        DomainGuard.DefinedEnum(acceptanceEvidence, nameof(acceptanceEvidence));

        return (outcome, acceptanceEvidence) switch
        {
            (NotificationSendOutcome.Accepted, SmtpAcceptanceEvidence.Accepted) =>
                DeliveryFailureDisposition.None,
            (NotificationSendOutcome.TransientNotAccepted, SmtpAcceptanceEvidence.NotAccepted) =>
                DeliveryFailureDisposition.Retry,
            (NotificationSendOutcome.PermanentRejected, SmtpAcceptanceEvidence.NotAccepted) =>
                DeliveryFailureDisposition.DeadLetter,
            (NotificationSendOutcome.AcceptanceUnknown, SmtpAcceptanceEvidence.Unknown) =>
                DeliveryFailureDisposition.ManualReview,
            _ => throw new ArgumentException("Notification outcome contradicts SMTP acceptance evidence."),
        };
    }
}
