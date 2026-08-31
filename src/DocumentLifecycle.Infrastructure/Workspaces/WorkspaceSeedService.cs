using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Domain.Activity;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Files;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Infrastructure.Workspaces;

internal sealed class WorkspaceSeedService(
    ApplicationDbContext database,
    ICurrentWorkspace currentWorkspace,
    WorkspaceUploadPathResolver pathResolver,
    IClock clock) : IWorkspaceSeedService
{
    private const string SeedActor = "manager@documents.demo";

    public async Task SeedAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (currentWorkspace.WorkspaceId != workspaceId)
        {
            throw new InvalidOperationException("The seed workspace does not match the current workspace.");
        }

        if (await database.ManagedDocuments.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = new[]
        {
            DocumentCategory.Create(workspaceId, "Policy", "Fictional governance and workplace policies."),
            DocumentCategory.Create(workspaceId, "Procedure", "Fictional step-by-step operating procedures."),
            DocumentCategory.Create(workspaceId, "Certificate", "Fictional training and compliance certificates."),
            DocumentCategory.Create(workspaceId, "Contract", "Fictional agreements used only for this portfolio demo."),
        };
        var owners = new[]
        {
            DocumentOwner.Create(workspaceId, "Operations Team", "operations@documents.demo"),
            DocumentOwner.Create(workspaceId, "People Team", "people@documents.demo"),
            DocumentOwner.Create(workspaceId, "Finance Team", "finance@documents.demo"),
        };

        database.DocumentCategories.AddRange(categories);
        database.DocumentOwners.AddRange(owners);
        await database.SaveChangesAsync(cancellationToken);

        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var samples = CreateSamples(today);
        var uploadDirectory = pathResolver.GetWorkspaceDirectory(workspaceId);
        Directory.CreateDirectory(uploadDirectory);

        try
        {
            for (var index = 0; index < samples.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sample = samples[index];
                var category = categories[sample.CategoryIndex];
                var owner = owners[sample.OwnerIndex];
                var eventTime = now.AddMinutes(-(samples.Count - index) * 11);
                var document = ManagedDocument.CreateDraft(
                    workspaceId,
                    $"DOC-{index + 1:0000}",
                    sample.Title,
                    sample.Description,
                    category.Id,
                    owner.Id,
                    sample.EffectiveDate,
                    sample.ExpiryDate,
                    SeedActor,
                    eventTime);

                database.ManagedDocuments.Add(document);
                database.AuditEvents.Add(CreateAudit(document, "Created", eventTime));

                if (sample.HasRevision)
                {
                    var pdf = SyntheticPdf.Create(sample.Title);
                    var storedFilename = $"{Guid.NewGuid():N}.pdf";
                    var filePath = Path.Combine(uploadDirectory, storedFilename);
                    await File.WriteAllBytesAsync(filePath, pdf, cancellationToken);
                    document.AddRevision(
                        "Initial fictional demo revision",
                        $"{Slugify(sample.Title)}.pdf",
                        storedFilename,
                        "application/pdf",
                        pdf.LongLength,
                        Convert.ToHexString(SHA256.HashData(pdf)),
                        SeedActor,
                        eventTime.AddMinutes(1));
                    database.AuditEvents.Add(CreateAudit(document, "RevisionUploaded", eventTime.AddMinutes(1)));
                }

                if (sample.TargetState is LifecycleState.Active or LifecycleState.Archived)
                {
                    document.Activate(SeedActor, eventTime.AddMinutes(2));
                    database.AuditEvents.Add(CreateAudit(document, "Activated", eventTime.AddMinutes(2)));
                }

                if (sample.TargetState == LifecycleState.Archived)
                {
                    document.Archive("Superseded fictional demo record", SeedActor, eventTime.AddMinutes(3));
                    database.AuditEvents.Add(CreateAudit(document, "Archived", eventTime.AddMinutes(3)));
                }
            }

            await database.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (Directory.Exists(uploadDirectory))
            {
                Directory.Delete(uploadDirectory, recursive: true);
            }

            throw;
        }
    }

    private static List<SeedDocument> CreateSamples(DateOnly today) =>
    [
        new("Remote Work Guide", "Draft guidance for a fictional flexible-work program.", 0, 1, today, null, LifecycleState.Draft, false),
        new("Visitor Check-in Draft", "Draft visitor handling instructions for the demo office.", 1, 0, today.AddDays(-10), null, LifecycleState.Draft, true),
        new("Equipment Care Policy", "Fictional policy for caring for shared demo equipment.", 0, 0, today.AddDays(-180), null, LifecycleState.Active, true),
        new("Learning Completion Certificate", "Fictional certificate template for portfolio demonstrations.", 2, 1, today.AddDays(-90), today.AddDays(120), LifecycleState.Active, true),
        new("Budget Review Procedure", "Fictional quarterly budget review procedure.", 1, 2, today.AddDays(-120), today.AddDays(45), LifecycleState.Active, true),
        new("Emergency Contact Procedure", "Fictional procedure nearing its scheduled review.", 1, 0, today.AddDays(-200), today, LifecycleState.Active, true),
        new("Records Handling Policy", "Fictional records guidance nearing review.", 0, 0, today.AddDays(-300), today.AddDays(14), LifecycleState.Active, true),
        new("Training Provider Certificate", "Fictional certificate at the thirty-day boundary.", 2, 1, today.AddDays(-335), today.AddDays(30), LifecycleState.Active, true),
        new("Office Services Agreement", "Fictional agreement whose review date has passed.", 3, 2, today.AddDays(-365), today.AddDays(-1), LifecycleState.Active, true),
        new("Safety Induction Certificate", "Fictional expired safety induction certificate.", 2, 0, today.AddDays(-400), today.AddDays(-45), LifecycleState.Active, true),
        new("Legacy Travel Policy", "Fictional policy retained for historical context.", 0, 1, today.AddDays(-600), today.AddDays(-200), LifecycleState.Archived, true),
        new("Retired Supply Contract", "Fictional retired contract retained with its history.", 3, 2, today.AddDays(-500), null, LifecycleState.Archived, true),
    ];

    private static AuditEvent CreateAudit(
        ManagedDocument document,
        string action,
        DateTime occurredAtUtc) =>
        AuditEvent.Create(
            document.WorkspaceId,
            SeedActor,
            action,
            nameof(ManagedDocument),
            document.PublicId,
            occurredAtUtc,
            JsonSerializer.Serialize(new
            {
                document.Code,
                document.Title,
            }));

    private static string Slugify(string value)
    {
        var characters = value
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray();
        return string.Join('-', new string(characters).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record SeedDocument(
        string Title,
        string Description,
        int CategoryIndex,
        int OwnerIndex,
        DateOnly EffectiveDate,
        DateOnly? ExpiryDate,
        LifecycleState TargetState,
        bool HasRevision);

    private static class SyntheticPdf
    {
        public static byte[] Create(string title)
        {
            var safeTitle = title.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
            var content = $"BT /F1 18 Tf 72 720 Td ({safeTitle}) Tj 0 -30 Td /F1 11 Tf (Synthetic portfolio demo file - no real records.) Tj ET";
            var objects = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            };
            var builder = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int> { 0 };

            for (var index = 0; index < objects.Length; index++)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
                builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
            }

            var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.Append("xref\n0 ").Append(objects.Length + 1).Append("\n")
                .Append("0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1))
            {
                builder.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" 00000 n \n");
            }

            builder.Append("trailer\n<< /Size ").Append(objects.Length + 1)
                .Append(" /Root 1 0 R >>\nstartxref\n").Append(xrefOffset).Append("\n%%EOF\n");
            return Encoding.ASCII.GetBytes(builder.ToString());
        }
    }
}
