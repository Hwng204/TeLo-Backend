using System.Text;
using Application.Common;
using Domain.Entities.QuestionBank;
using Xunit;

namespace Application.Tests;

public sealed class TaskJournalCodecTests
{
    [Fact]
    public void LargeUnicodeSnapshotRoundTripsWithinTextColumnLimit()
    {
        var snapshot = string.Concat(Enumerable.Range(0, 10000).Select(index => $"Câu hỏi {index} tiếng Việt 🌻 {index * 7919}"));
        var chunks = TaskJournalCodec.Encode("question", 1, "SUBMITTED", snapshot);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(Encoding.UTF8.GetByteCount(chunk) < 65535));
        var rows = Rows(chunks);
        rows.Add(new() { Content = "Nhận xét ma trận cũ" });
        Assert.Equal(snapshot, Assert.Single(TaskJournalCodec.Decode(rows, "question")).Read<string>());
        Assert.Empty(TaskJournalCodec.Decode(rows, "exam:5"));
    }

    [Fact]
    public void MissingChunkIsRejectedInsteadOfReturningPartialSnapshot()
    {
        var random = new byte[50000]; new Random(42).NextBytes(random);
        var rows = Rows(TaskJournalCodec.Encode("question", 1, "SUBMITTED", Convert.ToBase64String(random)));
        rows.RemoveAt(1);
        Assert.Equal("JOURNAL_INVALID", Assert.Throws<QuestionException>(() => TaskJournalCodec.Decode(rows, "question")).Code);
    }

    [Fact]
    public void MissingOrDuplicateRevisionCannotMasqueradeAsCurrentState()
    {
        var rows = Rows(TaskJournalCodec.Encode("question", 2, "SAVED", "payload"));
        Assert.Throws<QuestionException>(() => TaskJournalCodec.Decode(rows, "question"));
        rows = Rows(TaskJournalCodec.Encode("question", 1, "SAVED", "one"));
        rows.AddRange(Rows(TaskJournalCodec.Encode("question", 1, "SAVED", "two")));
        Assert.Throws<QuestionException>(() => TaskJournalCodec.Decode(rows, "question"));
    }

    [Fact]
    public void InvalidPayloadReturnsDomainConflictInsteadOfServerError()
    {
        var entry = new TaskJournalCodec.Entry(1, "question", 1, "SUBMITTED", 2, DateTime.UtcNow, "{broken");
        Assert.Equal("JOURNAL_INVALID", Assert.Throws<QuestionException>(() => entry.Read<string>()).Code);
    }

    [Fact]
    public void ChecksumCorruptionIsRejected()
    {
        var rows = Rows(TaskJournalCodec.Encode("question", 1, "SUBMITTED", "original"));
        rows[0].Content = rows[0].Content.Replace("\"hash\":\"", "\"hash\":\"BAD");
        Assert.Equal("JOURNAL_INVALID", Assert.Throws<QuestionException>(() => TaskJournalCodec.Decode(rows, "question")).Code);
    }

    [Fact]
    public void EncodedSubmissionIsUnaffectedByLaterEdits()
    {
        var source = new List<string> { "Bản nộp" };
        var rows = Rows(TaskJournalCodec.Encode("question", 1, "SUBMITTED", source));
        source[0] = "Nội dung đã sửa";
        rows.AddRange(Rows(TaskJournalCodec.Encode("question", 2, "SAVED", source)));
        var history = TaskJournalCodec.Decode(rows, "question");
        Assert.Equal("Bản nộp", history[0].Read<string[]>()[0]);
        Assert.Equal("Nội dung đã sửa", TaskJournalCodec.Decode(rows, "question", latestOnly: true)[1].Read<string[]>()[0]);
    }

    private static List<TaskHistory> Rows(IReadOnlyList<string> chunks) => chunks.Select((chunk, index) =>
        new TaskHistory { Id = (ulong)index + 1, TaskId = 1, CreateBy = 2, CreateAt = DateTime.UtcNow, Content = chunk }).ToList();
}
