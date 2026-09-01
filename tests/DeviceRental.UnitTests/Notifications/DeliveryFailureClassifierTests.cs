using DeviceRental.Domain.Notifications;
using Xunit;

namespace DeviceRental.UnitTests.Notifications;

public sealed class DeliveryFailureClassifierTests
{
    [Theory]
    [InlineData(NotificationSendOutcome.Accepted, SmtpAcceptanceEvidence.Accepted, DeliveryFailureDisposition.None)]
    [InlineData(NotificationSendOutcome.TransientNotAccepted, SmtpAcceptanceEvidence.NotAccepted, DeliveryFailureDisposition.Retry)]
    [InlineData(NotificationSendOutcome.PermanentRejected, SmtpAcceptanceEvidence.NotAccepted, DeliveryFailureDisposition.DeadLetter)]
    [InlineData(NotificationSendOutcome.AcceptanceUnknown, SmtpAcceptanceEvidence.Unknown, DeliveryFailureDisposition.ManualReview)]
    [Trait("Requirement", "REQ-NOTIFY-004")]
    public void Classify_UsesExplicitOutcomeAndAcceptanceEvidence(
        NotificationSendOutcome outcome,
        SmtpAcceptanceEvidence evidence,
        DeliveryFailureDisposition expected)
    {
        Assert.Equal(expected, DeliveryFailureClassifier.Classify(outcome, evidence));
    }

    [Fact]
    public void Classify_RejectsContradictoryAcceptanceEvidence()
    {
        Assert.Throws<ArgumentException>(() => DeliveryFailureClassifier.Classify(
            NotificationSendOutcome.AcceptanceUnknown,
            SmtpAcceptanceEvidence.NotAccepted));
        Assert.Throws<ArgumentException>(() => DeliveryFailureClassifier.Classify(
            NotificationSendOutcome.TransientNotAccepted,
            SmtpAcceptanceEvidence.Unknown));
    }
}
