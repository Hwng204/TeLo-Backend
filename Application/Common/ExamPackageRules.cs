using System.Security.Cryptography;
using Domain.Entities.QuestionBank;

namespace Application.Common;

public static class ExamPackageRules
{
    public static bool Matches(Question q, ExamMatrix matrix, MatrixDetail cell) =>
        q.Status == "APPROVED" && q.BankType == "exam" && q.Lesson.Status == "ACTIVE" && q.Lesson.Chapter.Status == "ACTIVE" &&
        q.Lesson.Chapter.SchoolBranchId == matrix.BranchId && q.Lesson.Chapter.SubjectId == matrix.SubjectId &&
        q.Lesson.Chapter.GradeLevelId == matrix.GradeLevelId && q.LessonId == cell.LessonId && q.QuestionTaskDetail.QuestionTask.LessonId == cell.LessonId &&
        q.QuestionTaskDetail.CognitiveLevel == cell.CognitiveLevel && q.QuestionTaskDetail.QuestionType == cell.QuestionType &&
        !string.IsNullOrWhiteSpace(q.Content) && q.Options.Count == 4 && q.Options.Count(x => x.IsCorrect) == 1 &&
        q.Options.Select(x => x.OptionKey).Order().SequenceEqual(new[] { "A", "B", "C", "D" }) &&
        q.Options.All(x => !string.IsNullOrWhiteSpace(x.Content));

    public static void Matrix(ExamMatrix matrix)
    {
        if (matrix.Status is not ("APPROVED" or "ARCHIVED") || matrix.TotalScore <= 0 || matrix.Details.Count == 0 ||
            matrix.Details.Sum(x => x.Percentage) != 100 || matrix.Details.Any(x => x.QuestionCount == 0 || x.Percentage <= 0 ||
                !MatrixCognitiveLevels.IsKnown(x.CognitiveLevel) || x.QuestionType != "MULTIPLE_CHOICE") ||
            matrix.Details.Sum(x => (long)x.QuestionCount) > 1000 ||
            matrix.Details.Select(x => (x.LessonId, x.CognitiveLevel, x.QuestionType)).Distinct().Count() != matrix.Details.Count)
            throw new QuestionException("EXAM_MATRIX_INVALID", "Ma trận phải đã duyệt, có cấu trúc hợp lệ, tối đa 1.000 câu và tổng tỷ lệ điểm bằng 100%.");
    }

    public static IReadOnlyList<(ulong DetailId, Question Question)> Select(ExamMatrix matrix, IReadOnlyList<Question> candidates)
    {
        Matrix(matrix);
        if (matrix.Status != "APPROVED") throw new QuestionException("EXAM_MATRIX_INVALID", "Chỉ tạo đề mới từ ma trận đã duyệt.");
        var result = new List<(ulong DetailId, Question Question)>();
        var used = new HashSet<ulong>();
        var shortages = new Dictionary<string, string[]>();
        foreach (var cell in matrix.Details.OrderBy(x => x.Id))
        {
            var pool = candidates.Where(q => !used.Contains(q.Id) && Matches(q, matrix, cell)).DistinctBy(q => q.Id).ToArray();
            if (pool.Length < cell.QuestionCount)
            {
                shortages[$"cell_{cell.Id}"] = [$"{cell.Lesson?.Title ?? $"Bài {cell.LessonId}"} · {cell.CognitiveLevel}: cần {cell.QuestionCount}, có {pool.Length} câu hợp lệ."];
                continue;
            }
            for (var i = 0; i < cell.QuestionCount; i++)
            {
                var selected = RandomNumberGenerator.GetInt32(i, pool.Length);
                (pool[i], pool[selected]) = (pool[selected], pool[i]);
                used.Add(pool[i].Id); result.Add((cell.Id, pool[i]));
            }
        }
        if (shortages.Count > 0) throw new QuestionException("EXAM_QUESTIONS_SHORTAGE", "Ngân hàng chưa đủ câu hỏi đúng ma trận. Vui lòng bổ sung các ô còn thiếu.", 422, shortages);
        return result;
    }

    public static void Validate(ExamMatrix matrix, IReadOnlyList<(ulong DetailId, Question Question)> rows)
    {
        Matrix(matrix);
        if (rows.Select(x => x.Question.Id).Distinct().Count() != rows.Count ||
            rows.Any(x => !matrix.Details.Any(cell => cell.Id == x.DetailId && Matches(x.Question, matrix, cell))) ||
            matrix.Details.Any(cell => rows.Count(x => x.DetailId == cell.Id) != cell.QuestionCount))
            throw new QuestionException("EXAM_STRUCTURE_INVALID", "Đề phải có đủ câu hợp lệ ở từng ô ma trận, không trùng câu và chỉ lấy từ ngân hàng thi.", 409);
    }
}
