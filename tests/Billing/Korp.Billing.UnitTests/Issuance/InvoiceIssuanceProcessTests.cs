using Korp.Billing.Domain;
using Korp.Billing.Domain.Issuance;

namespace Korp.Billing.UnitTests.Issuance;

public sealed class InvoiceIssuanceProcessTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TstData013CreateProducesPendingProcessWithoutOutcome()
    {
        var process = CreateProcess();

        Assert.Equal(InvoiceIssuanceProcessStatus.Pending, process.Status);
        Assert.Null(process.OutcomeCode);
        Assert.Null(process.OutcomeDescription);
        Assert.Null(process.FinishedAtUtc);
    }

    [Fact]
    public void TstData013ProcessCanAwaitAndComplete()
    {
        var process = CreateProcess();

        process.MarkAwaitingStock(Now.AddMinutes(1));
        process.Complete(Now.AddMinutes(2));

        Assert.Equal(InvoiceIssuanceProcessStatus.Completed, process.Status);
        Assert.Equal(Now.AddMinutes(2), process.FinishedAtUtc);
        Assert.Null(process.OutcomeCode);
    }

    [Theory]
    [InlineData(true, InvoiceIssuanceProcessStatus.Rejected)]
    [InlineData(false, InvoiceIssuanceProcessStatus.ManualIntervention)]
    public void TstData013ProcessRecordsSanitizedTerminalOutcome(bool reject, InvoiceIssuanceProcessStatus expected)
    {
        var process = CreateProcess();

        if (reject)
        {
            process.Reject(" insufficient_stock ", " Safe description ", Now.AddMinutes(1));
        }
        else
        {
            process.RequireManualIntervention(" processing_failed ", null, Now.AddMinutes(1));
        }

        Assert.Equal(expected, process.Status);
        Assert.NotNull(process.OutcomeCode);
        Assert.Equal(process.OutcomeCode!.Trim(), process.OutcomeCode);
        Assert.Equal(Now.AddMinutes(1), process.FinishedAtUtc);
    }

    [Fact]
    public void TstData013ProcessRejectsInvalidCreationOutcomeAndTransitions()
    {
        Assert.Throws<DomainRuleException>(() => InvoiceIssuanceProcess.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now));
        Assert.Throws<DomainRuleException>(() => InvoiceIssuanceProcess.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default));

        var process = CreateProcess();
        Assert.Throws<DomainRuleException>(() => process.Reject(" ", null, Now.AddMinutes(1)));
        Assert.Throws<DomainRuleException>(() => process.MarkAwaitingStock(Now.AddSeconds(-1)));

        process.Complete(Now.AddMinutes(1));
        Assert.Throws<DomainRuleException>(() => process.Complete(Now.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(() => process.MarkAwaitingStock(Now.AddMinutes(2)));
    }

    [Fact]
    public void TstData013ProcessRejectsOversizedOutcomeFields()
    {
        var process = CreateProcess();
        Assert.Throws<DomainRuleException>(() => process.Reject(new string('A', 101), null, Now.AddMinutes(1)));

        process = CreateProcess();
        Assert.Throws<DomainRuleException>(() => process.Reject("code", new string('A', 501), Now.AddMinutes(1)));
    }

    private static InvoiceIssuanceProcess CreateProcess() =>
        InvoiceIssuanceProcess.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
}
