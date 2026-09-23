using Domain.Entities.Academic;

namespace Domain.Entities.QuestionBank;

public sealed class ExamMatrix
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ulong? TaskId { get; set; }
    public ulong? SemesterId { get; set; }
    public ulong AcademicContextId { get; set; }
    public string? RejectComment { get; set; }
    public ulong? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }

    // Null for matrices that predate authorship tracking and had no task to infer it from.
    public ulong? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ulong? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public WorkTask? Task { get; set; }
    public Semester? Semester { get; set; }
    public AcademicContext AcademicContext { get; set; } = null!;
    public ICollection<MatrixDetail> Details { get; set; } = new List<MatrixDetail>();

    public uint TotalQuestions =>
        Details.Aggregate(0u, (total, detail) => checked(total + detail.QuestionCount));

    // Số nguyên dương do người lập tự đặt (không còn tính từ tổng chi tiết). Điểm mỗi ô là
    // TotalScore * Percentage / 100, suy ra khi hiển thị/xuất file, không lưu ở MatrixDetail.
    public int TotalScore { get; set; }

    // A matrix must add up to 100% of its declared TotalScore before it can be submitted, confirmed
    // or edited in review.
    public const decimal RequiredTotalPercentage = 100m;

    public bool CanEdit(MatrixActor actor)
    {
        return Status switch
        {
            MatrixStatusCodes.Draft => actor.Role == MatrixActorRole.Pht || IsAssignedTeamLead(actor),
            MatrixStatusCodes.Submitted => actor.Role == MatrixActorRole.Pht,
            _ => false
        };
    }

    public bool CanHardDelete(MatrixActor actor)
    {
        return Status == MatrixStatusCodes.Draft &&
            (actor.Role == MatrixActorRole.Pht || IsAssignedTeamLead(actor));
    }

    public void ReplaceDetails(IEnumerable<MatrixDetailValue> values, MatrixActor actor)
    {
        if (!CanEdit(actor))
        {
            throw new MatrixDomainException(
                "MatrixNotEditable",
                "Bạn không thể chỉnh sửa ma trận ở trạng thái hiện tại.");
        }

        var normalizedDetails = new List<MatrixDetail>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values ?? throw new ArgumentNullException(nameof(values)))
        {
            if (value.LessonId == 0 ||
                value.QuestionCount == 0 ||
                value.Percentage <= 0 ||
                value.Percentage > 100)
            {
                throw new MatrixDomainException(
                    "InvalidDetail",
                    "Mỗi dòng chi tiết phải có bài học, số câu lớn hơn 0 và tỷ lệ điểm trong khoảng (0, 100].");
            }

            var cognitiveLevel = Normalize(value.CognitiveLevel);
            var questionType = Normalize(value.QuestionType);
            if (questionType.Length == 0)
            {
                questionType = MatrixQuestionTypes.MultipleChoice;
            }

            if (!MatrixCognitiveLevels.IsKnown(cognitiveLevel) ||
                questionType != MatrixQuestionTypes.MultipleChoice)
            {
                throw new MatrixDomainException(
                    "InvalidDetail",
                    "Mỗi dòng chi tiết phải dùng mức nhận thức hợp lệ (Nhận biết, Thông hiểu, Vận dụng) và loại câu hỏi trắc nghiệm.");
            }

            var key = $"{value.LessonId}:{cognitiveLevel}:{questionType}";
            if (!keys.Add(key))
            {
                throw new MatrixDomainException(
                    "DuplicateDetail",
                    "Ma trận không được có hai dòng trùng bài học, mức nhận thức và loại câu hỏi.");
            }

            normalizedDetails.Add(new MatrixDetail
            {
                ExamMatrixId = Id,
                LessonId = value.LessonId,
                CognitiveLevel = cognitiveLevel,
                QuestionType = questionType,
                QuestionCount = value.QuestionCount,
                Percentage = value.Percentage,
                ExamMatrix = this
            });
        }

        // A Draft may be saved at any percentage total, even empty, so it can be worked on in several
        // sittings. A Submitted matrix is already in review, so editing it must keep it at exactly 100%.
        // Checked before Details.Clear() so a rejected save leaves the matrix untouched.
        if (Status == MatrixStatusCodes.Submitted)
        {
            EnsureRequiredTotalPercentage(normalizedDetails.Sum(detail => detail.Percentage));
        }

        Details.Clear();
        foreach (var detail in normalizedDetails)
        {
            Details.Add(detail);
        }
    }

    public void Submit(MatrixActor actor)
    {
        EnsureDraft();
        if (actor.Role != MatrixActorRole.Pht && !IsAssignedTeamLead(actor))
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Chỉ PHT hoặc Tổ trưởng được giao mới được nộp ma trận này.");
        }

        if (Details.Count == 0)
        {
            throw new MatrixDomainException(
                "EmptyMatrix",
                "Ma trận phải có ít nhất một dòng chi tiết trước khi nộp hoặc xác nhận.");
        }

        // Also covers ConfirmDirect, which submits before approving.
        EnsureRequiredTotalPercentage(Details.Sum(detail => detail.Percentage));

        Status = MatrixStatusCodes.Submitted;
        ClearRejection();
    }

    // The PHT sends a submitted matrix back to the Team Lead (Draft) with an optional comment.
    public void Reject(MatrixActor actor, string? comment, DateTime rejectedAtUtc)
    {
        if (actor.Role != MatrixActorRole.Pht)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Chỉ PHT mới được từ chối ma trận.");
        }

        if (Status != MatrixStatusCodes.Submitted)
        {
            throw InvalidTransition("Chỉ từ chối được ma trận đã nộp.");
        }

        Status = MatrixStatusCodes.Draft;
        RejectComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        RejectedByUserId = actor.UserId;
        RejectedAt = rejectedAtUtc;
    }

    public void Approve(MatrixActor actor, DateTime? approvedAtUtc = null)
    {
        if (actor.Role != MatrixActorRole.Pht)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Chỉ PHT mới được duyệt ma trận.");
        }

        if (Status != MatrixStatusCodes.Submitted)
        {
            throw InvalidTransition("Chỉ duyệt được ma trận đã nộp.");
        }

        Status = MatrixStatusCodes.Approved;
        ApprovedByUserId = actor.UserId;
        ApprovedAt = approvedAtUtc ?? DateTime.UtcNow;
    }

    public void ConfirmDirect(MatrixActor actor, DateTime? approvedAtUtc = null)
    {
        if (actor.Role != MatrixActorRole.Pht)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Chỉ PHT mới được xác nhận ma trận trực tiếp.");
        }

        if (TaskId is not null)
        {
            throw new MatrixDomainException(
                "DirectMatrixRequired",
                "Chỉ xác nhận trực tiếp được ma trận không thuộc nhiệm vụ nào.");
        }

        Submit(actor);
        Approve(actor, approvedAtUtc);
    }

    public void Archive(MatrixActor actor)
    {
        if (actor.Role != MatrixActorRole.Pht)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Chỉ PHT mới được lưu trữ ma trận.");
        }

        if (Status != MatrixStatusCodes.Approved)
        {
            throw InvalidTransition("Chỉ lưu trữ được ma trận đã duyệt.");
        }

        Status = MatrixStatusCodes.Archived;
    }

    public ExamMatrix CloneAsDraft(MatrixActor actor, DateTime? createdAtUtc = null)
    {
        if (actor.Role != MatrixActorRole.Pht)
        {
            throw new MatrixDomainException(
                "Forbidden",
                "Chỉ PHT mới được tạo bản sao ma trận.");
        }

        if (Status is not (MatrixStatusCodes.Approved or MatrixStatusCodes.Archived))
        {
            throw InvalidTransition("Chỉ sao chép được ma trận đã duyệt hoặc đã lưu trữ.");
        }

        var clone = new ExamMatrix
        {
            Id = 0,
            Name = Name,
            Status = MatrixStatusCodes.Draft,
            TaskId = null,
            SemesterId = SemesterId,
            AcademicContextId = AcademicContextId,
            TotalScore = TotalScore,
            // The copy is a new matrix authored by whoever made the copy, not by the original author.
            CreatedByUserId = actor.UserId,
            CreatedAt = createdAtUtc ?? DateTime.UtcNow
        };

        foreach (var detail in Details)
        {
            clone.Details.Add(new MatrixDetail
            {
                Id = 0,
                ExamMatrixId = 0,
                LessonId = detail.LessonId,
                CognitiveLevel = detail.CognitiveLevel,
                QuestionType = detail.QuestionType,
                QuestionCount = detail.QuestionCount,
                Percentage = detail.Percentage,
                ExamMatrix = clone,
                Lesson = detail.Lesson
            });
        }

        return clone;
    }

    // True when the matrix's detail rows add up to 100% of its declared TotalScore, i.e. it may be
    // submitted or confirmed.
    public bool HasRequiredTotalPercentage => Details.Sum(detail => detail.Percentage) == RequiredTotalPercentage;

    private static void EnsureRequiredTotalPercentage(decimal totalPercentage)
    {
        if (totalPercentage != RequiredTotalPercentage)
        {
            throw new MatrixDomainException(
                "InvalidTotalScore",
                $"Tổng tỷ lệ điểm của ma trận phải bằng {RequiredTotalPercentage:0}% (hiện là {totalPercentage:0.##}%).");
        }
    }

    private void ClearRejection()
    {
        RejectComment = null;
        RejectedByUserId = null;
        RejectedAt = null;
    }

    private bool IsAssignedTeamLead(MatrixActor actor)
    {
        return actor.Role == MatrixActorRole.TeamLead &&
            TaskId.HasValue &&
            Task is not null &&
            Task.AssignedToUserId == actor.UserId;
    }

    private void EnsureDraft()
    {
        if (Status != MatrixStatusCodes.Draft)
        {
            throw InvalidTransition("Chỉ ma trận ở trạng thái Nháp mới thực hiện được thao tác này.");
        }
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static MatrixDomainException InvalidTransition(string message)
    {
        return new MatrixDomainException("InvalidTransition", message);
    }
}
