using DocumentLifecycle.Domain.Common;
using DocumentLifecycle.Domain.Documents;

namespace DocumentLifecycle.UnitTests.Documents;

public sealed class ManagedDocumentTests
{
    private static readonly Guid WorkspaceId = Guid.Parse("f95c86ef-00d1-4e16-930e-6989bcf86e8e");
    private static readonly DateTime UtcNow = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(UtcNow);

    [Fact]
    public void DraftCannotActivateWithoutARevision()
    {
        var document = CreateDraft();

        var exception = Assert.Throws<DomainRuleException>(() => document.Activate("manager", UtcNow));

        Assert.Contains("revision", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LifecycleState.Draft, document.State);
    }

    [Fact]
    public void ValidLifecycleTransitionsPreserveRevisionHistory()
    {
        var document = CreateDraft();
        document.AddRevision(
            "Initial version",
            "fictional.pdf",
            "stored-1.pdf",
            "application/pdf",
            128,
            new string('A', 64),
            "manager",
            UtcNow);

        document.Activate("manager", UtcNow.AddMinutes(1));
        document.AddRevision(
            "Approved update",
            "fictional-v2.pdf",
            "stored-2.pdf",
            "application/pdf",
            256,
            new string('B', 64),
            "manager",
            UtcNow.AddMinutes(2));
        document.Archive("Superseded", "manager", UtcNow.AddMinutes(3));

        Assert.Equal("Superseded", document.ArchiveReason);
        Assert.Equal("manager", document.ArchivedBy);

        document.Restore("manager", UtcNow.AddMinutes(4));

        Assert.Equal(LifecycleState.Active, document.State);
        Assert.Equal([1, 2], document.Revisions.Select(revision => revision.RevisionNumber));
        Assert.Null(document.ArchiveReason);
        Assert.Null(document.ArchivedBy);
        Assert.Null(document.ArchivedAtUtc);
    }

    [Fact]
    public void InvalidTransitionsAndArchivedRevisionAreRejected()
    {
        var document = CreateDraft();
        AddRevision(document);

        Assert.Throws<DomainRuleException>(() => document.Archive("Not active", "manager", UtcNow));
        Assert.Throws<DomainRuleException>(() => document.Restore("Not archived", UtcNow));

        document.Activate("manager", UtcNow);
        Assert.Throws<DomainRuleException>(() => document.Activate("manager", UtcNow));
        document.Archive("Retired", "manager", UtcNow);

        Assert.Throws<DomainRuleException>(() => AddRevision(document));
        Assert.Throws<DomainRuleException>(() => document.Archive("Again", "manager", UtcNow));
    }

    [Theory]
    [InlineData(-1, DocumentDisplayStatus.Expired)]
    [InlineData(0, DocumentDisplayStatus.ExpiringSoon)]
    [InlineData(30, DocumentDisplayStatus.ExpiringSoon)]
    [InlineData(31, DocumentDisplayStatus.Active)]
    public void ActiveExpiryStatusHonorsInclusiveThirtyDayBoundary(
        int daysFromToday,
        DocumentDisplayStatus expected)
    {
        var document = CreateDraft(Today.AddDays(daysFromToday));
        AddRevision(document);
        document.Activate("manager", UtcNow);

        Assert.Equal(expected, document.GetDisplayStatus(Today));
    }

    [Fact]
    public void RevisionsAreNumberedSequentially()
    {
        var document = CreateDraft();

        AddRevision(document);
        AddRevision(document);
        AddRevision(document);

        Assert.Equal([1, 2, 3], document.Revisions.Select(revision => revision.RevisionNumber));
    }

    private static ManagedDocument CreateDraft(DateOnly? expiryDate = null) =>
        ManagedDocument.CreateDraft(
            WorkspaceId,
            "DOC-TEST",
            "Fictional document",
            "Synthetic test data",
            categoryId: 1,
            ownerId: 1,
            Today.AddDays(-10),
            expiryDate,
            "manager",
            UtcNow);

    private static void AddRevision(ManagedDocument document) =>
        document.AddRevision(
            "Test revision",
            "fictional.pdf",
            $"{Guid.NewGuid():N}.pdf",
            "application/pdf",
            128,
            new string('C', 64),
            "manager",
            UtcNow);
}
