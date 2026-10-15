using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Implement;
using Domain.Entities.Academic;
using Domain.Entities.Examination;
using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Infrastructure.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

if (!args.Contains("--seed")) { Console.WriteLine("Run with --seed to add local question demo data. Existing demo tasks are preserved."); return; }
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connection))
{
    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", "telo-school-management-webapi-local", "secrets.json");
    using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(path)); connection = secrets.RootElement.GetProperty("ConnectionStrings:DefaultConnection").GetString();
}
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Local MySQL connection is not configured.");
var builder = new MySqlConnectionStringBuilder(connection);
if (builder.Server is not ("localhost" or "127.0.0.1" or "::1")) throw new InvalidOperationException("Demo seed only supports local MySQL.");
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(builder.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options);
await db.Database.MigrateAsync();
var branch = await db.SchoolBranches.Include(x => x.School).Where(x => x.Status == "ACTIVE").OrderBy(x => x.Id).FirstAsync();
var password = Environment.GetEnvironmentVariable("TELO_QUESTION_DEMO_PASSWORD") ?? "Question-demo-123!";
async Task<User> Account(string suffix, string name, string roleCode)
{
    var username = "question_demo_" + suffix;
    var existing = await db.Users.SingleOrDefaultAsync(x => x.Username == username); if (existing is not null) return existing;
    var role = await db.Roles.SingleOrDefaultAsync(x => x.Code == roleCode) ?? new Role { Code = roleCode, Name = roleCode, IsSystem = true };
    if (role.Status != "ACTIVE") throw new InvalidOperationException($"Demo role {roleCode} is inactive.");
    var user = new User { Username = username, FullName = name, Email = username + "@example.invalid", SchoolBranchId = branch.Id, Status = "ACTIVE" };
    user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password); user.UserRoles.Add(new() { Role = role }); db.Users.Add(user); await db.SaveChangesAsync(); return user;
}
var teacher = await Account("gv", "Nguyễn Minh Anh", "GIAO_VIEN");
var lead = await Account("tl", "Trần Thu Hà", "TEAM_LEAD");
var pht = await Account("pht", "Lê Hoàng Nam", "PHT");
await Account("ht", "Phạm Thanh Bình", "PRINCIPAL");
var subject = await db.Subjects.FirstOrDefaultAsync(x => x.Name == "Toán") ?? new Subject { Name = "Toán" };
var grade = await db.GradeLevels.FirstOrDefaultAsync(x => x.Name.Contains("5")) ?? new GradeLevel { Name = "Khối 5" };
var field = await db.SubjectFields.FirstOrDefaultAsync(x => x.SubjectId == subject.Id && x.Name == "Số và phép tính") ?? new SubjectField { Subject = subject, Name = "Số và phép tính" };
var chapter = await db.Chapters.Include(x => x.Lessons).SingleOrDefaultAsync(x => x.SchoolBranchId == branch.Id && x.Code == "QM_DEMO")
    ?? new Chapter { SchoolBranchId = branch.Id, Field = field, GradeLevel = grade, Code = "QM_DEMO", Title = "Số và phép tính — dữ liệu thử", SortOrder = 99 };
if (chapter.Id == 0) { chapter.Lessons.Add(new() { Code = "1", Title = "Phân số", SortOrder = 1 }); chapter.Lessons.Add(new() { Code = "2", Title = "Số thập phân", SortOrder = 2 }); db.Chapters.Add(chapter); await db.SaveChangesAsync(); }
var lessons = chapter.Lessons.OrderBy(x => x.SortOrder).ToArray();
var today = TaskRules.Today(TimeProvider.System);
var service = new QuestionService(db, TimeProvider.System);
var teacherActor = new QuestionActor(teacher.Id, true, false, false, false); var leadActor = new QuestionActor(lead.Id, false, true, false, false);
var catalog = new (string Content, string[] Options, string Correct, string Explanation)[] {
    ("Phân số nào bằng 1/2?", ["2/4", "1/3", "3/4", "2/3"], "A", "Nhân tử số và mẫu số của 1/2 với 2 được 2/4."),
    ("Tử số của phân số 3/5 là số nào?", ["3", "5", "8", "2"], "A", "Tử số là số viết phía trên dấu gạch ngang."),
    ("Phân số 2/3 biểu thị điều gì?", ["Chia thành 2 phần bằng nhau, lấy 3 phần", "Chia thành 3 phần bằng nhau, lấy 2 phần", "Chia thành 3 phần, lấy cả 3 phần", "Chia thành 2 phần, lấy 1 phần"], "B", "Mẫu số cho biết số phần bằng nhau; tử số cho biết số phần được lấy."),
    ("An ăn 1/4 chiếc bánh, Bình ăn 2/4 chiếc bánh. Hai bạn đã ăn bao nhiêu phần chiếc bánh?", ["1/4", "3/4", "2/4", "4/4"], "B", "Hai phân số cùng mẫu: cộng tử số và giữ nguyên mẫu số."),
    ("Chữ số 5 trong số 12,5 thuộc hàng nào?", ["Hàng chục", "Hàng đơn vị", "Hàng phần mười", "Hàng phần trăm"], "C", "Chữ số đầu tiên sau dấu phẩy ở hàng phần mười."),
    ("Số thập phân 0,5 bằng phân số nào?", ["1/2", "1/5", "5/1", "1/10"], "A", "0,5 = 5/10 = 1/2."),
    ("Một đoạn dây dài 2,5 m, cắt đi 0,8 m. Đoạn dây còn lại dài bao nhiêu?", ["1,5 m", "1,7 m", "2,3 m", "3,3 m"], "B", "2,5 − 0,8 = 1,7 m.")
};

async Task<WorkTask> Package(string name, string state, bool full)
{
    var existing = await db.WorkTasks.SingleOrDefaultAsync(x => x.Name == name); if (existing is not null) return existing;
    var task = new WorkTask { Name = name, TaskType = "QUESTION", Status = "ASSIGNED", CreatedByUserId = lead.Id, AssignedToUserId = teacher.Id, CreatedAt = DateTime.UtcNow, DueAt = today.AddDays(state == "OVERDUE" ? -2 : 7).ToDateTime(TimeOnly.MinValue) };
    var sourceLessons = full ? lessons : lessons.Take(1).ToArray();
    foreach (var lesson in sourceLessons)
    {
        var group = new QuestionTask { Task = task, Lesson = lesson, Status = "ASSIGNED", AssignedQuestionCount = full && lesson.Id == lessons[0].Id ? 4u : 3u };
        foreach (var level in MatrixCognitiveLevels.All) group.Details.Add(new() { CognitiveLevel = level.Code, QuestionType = "MULTIPLE_CHOICE", QuestionCount = full && lesson.Id == lessons[0].Id && level.Code == "NHAN_BIET" ? 2u : 1u });
        db.QuestionTasks.Add(group);
    }
    await db.SaveChangesAsync();
    if (state == "OVERDUE") return task;
    var package = await service.GetAsync(teacherActor, task.Id, default);
    var written = 0;
    foreach (var target in package.Targets.ToArray())
        for (var i = 0; i < target.RequiredCount; i++)
        {
            if (state == "IN_PROGRESS" && written >= 2) continue;
            var index = target.LessonId == lessons[0].Id ? target.CognitiveLevel switch { "NHAN_BIET" => i, "THONG_HIEU" => 2, _ => 3 } : target.CognitiveLevel switch { "NHAN_BIET" => 4, "THONG_HIEU" => 5, _ => 6 };
            var row = catalog[index];
            package = await service.SaveAsync(teacherActor, task.Id, null, new(package.Version, target.DetailId, row.Content, row.Options, row.Correct, row.Explanation), default); written++;
        }
    if (state is "PENDING_REVIEW" or "NEEDS_REVISION" or "COMPLETED") package = await service.SubmitAsync(teacherActor, task.Id, package.Version, default);
    if (state == "NEEDS_REVISION") await service.ReviewAsync(leadActor, task.Id, false, new(package.Version, "Bổ sung giải thích rõ cách tính ở câu vận dụng, sau đó gửi lại cả gói.", [new(package.Questions.Last().Id, "Giải thích từng bước tính để học sinh đối chiếu.")]), default);
    if (state == "COMPLETED") await service.ReviewAsync(leadActor, task.Id, true, new(package.Version, null, null), default);
    return task;
}
await Package("QM DEMO · Biên soạn câu hỏi Toán 5", "IN_PROGRESS", true);
await Package("QM DEMO · Gói câu hỏi chờ kiểm duyệt", "PENDING_REVIEW", false);
await Package("QM DEMO · Gói cần chỉnh sửa", "NEEDS_REVISION", false);
var approvedTask = await Package("QM DEMO · Câu hỏi đã chốt", "COMPLETED", true);
await Package("QM DEMO · Nhiệm vụ quá hạn", "OVERDUE", false);
var usedTask = await Package("QM DEMO · Câu hỏi dùng trong kỳ thi thử", "COMPLETED", false);

var year = await db.AcademicYears.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
if (year is null) { year = new() { Name = "2026-2027", StartDate = new(2026, 8, 1), EndDate = new(2027, 6, 30) }; year.AssignCode("QM_DEMO_2026"); db.AcademicYears.Add(year); await db.SaveChangesAsync(); }
var semester = await db.Semesters.FirstOrDefaultAsync(x => x.AcademicYearId == year.Id);
if (semester is null) { semester = new() { AcademicYearId = year.Id, Order = 1, Name = "Học kỳ I", StartDate = year.StartDate, EndDate = year.EndDate }; db.Semesters.Add(semester); await db.SaveChangesAsync(); }
var context = await db.AcademicContexts.FirstOrDefaultAsync(x => x.SchoolBranchId == branch.Id && x.SubjectId == subject.Id && x.GradeLevelId == grade.Id && x.AcademicYearId == year.Id)
    ?? new AcademicContext { SchoolId = branch.SchoolId, SchoolBranchId = branch.Id, SubjectId = subject.Id, GradeLevelId = grade.Id, AcademicYearId = year.Id };
if (context.Id == 0) { db.AcademicContexts.Add(context); await db.SaveChangesAsync(); }
var matrix = await db.ExamMatrices.SingleOrDefaultAsync(x => x.Name == "QM DEMO · Ma trận Toán 5");
if (matrix is null)
{
    matrix = new() { Name = "QM DEMO · Ma trận Toán 5", AcademicContextId = context.Id, SemesterId = semester.Id, TotalScore = 10, Status = "APPROVED", CreatedByUserId = pht.Id, CreatedAt = DateTime.UtcNow, ApprovedByUserId = pht.Id, ApprovedAt = DateTime.UtcNow };
    var approved = await service.GetAsync(teacherActor, approvedTask.Id, default);
    foreach (var t in approved.Targets) matrix.Details.Add(new() { LessonId = t.LessonId, CognitiveLevel = t.CognitiveLevel, QuestionType = "MULTIPLE_CHOICE", QuestionCount = t.RequiredCount, Percentage = t.RequiredCount == 2 ? 30 : 14 });
    db.ExamMatrices.Add(matrix); await db.SaveChangesAsync();
}
if (!await db.WorkTasks.AnyAsync(x => x.Name == "QM DEMO · Chọn câu hỏi cho hai đề"))
{
    var task = new WorkTask { Name = "QM DEMO · Chọn câu hỏi cho hai đề", TaskType = "EXAM", Status = "ASSIGNED", CreatedByUserId = pht.Id, AssignedToUserId = lead.Id, CreatedAt = DateTime.UtcNow, DueAt = today.AddDays(10).ToDateTime(TimeOnly.MinValue), ExamMatrixId = matrix.Id, AcademicContextId = context.Id };
    for (var i = 1; i <= 2; i++) db.ExamTaskPapers.Add(new() { Task = task, Name = $"Đề {i}", SortOrder = (uint)(i - 1) }); await db.SaveChangesAsync();
}
if (!await db.Exams.AnyAsync(x => x.Name == "QM DEMO · Kỳ thi đã kết thúc"))
{
    var questionIds = (await service.GetAsync(teacherActor, usedTask.Id, default)).Questions.Select(x => x.Id).ToArray();
    var examSet = new ExamSet { Name = "QM DEMO · Đề đã thi", ExamMatrixId = matrix.Id, Purpose = "exam", Status = "APPROVED", CreatedByUserId = lead.Id, ApprovedAt = DateTime.UtcNow, ApprovedByUserId = pht.Id };
    foreach (var id in questionIds.Take(2)) examSet.Questions.Add(new() { QuestionId = id });
    var backup = new ExamSet { Name = "QM DEMO · Đề dự phòng chưa dùng", ExamMatrixId = matrix.Id, Purpose = "exam", Status = "APPROVED", CreatedByUserId = lead.Id, ApprovedAt = DateTime.UtcNow, ApprovedByUserId = pht.Id }; backup.Questions.Add(new() { QuestionId = questionIds[2] });
    var exam = new Exam { Name = "QM DEMO · Kỳ thi đã kết thúc", SchoolBranchId = branch.Id, SemesterId = semester.Id, GradeLevelId = grade.Id, SubjectId = subject.Id, StartDate = today.AddDays(-3), EndDate = today.AddDays(-3), StartTime = new(8, 0), EndTime = new(9, 0), Status = "COMPLETED" };
    var examGrade = new ExamSubjectGradeLevel { ExamSubject = new() { Exam = exam, SubjectId = subject.Id, DurationMinutes = 30, Status = "ACTIVE" }, GradeLevelId = grade.Id, PrimaryExamSet = examSet, BackupExamSet = backup, Status = "ACTIVE" };
    var variant = new ExamVariant { ExamSet = examSet, VariantCode = "QM_A" };
    uint position = 1; foreach (var q in examSet.Questions) variant.Questions.Add(new() { ExamSetQuestion = q, PositionNo = position++ });
    var registration = new ExamRegistration { ExamSubjectGradeLevel = examGrade, Student = new Student { Code = "QM_DEMO_HS", FullName = "Học sinh thử câu hỏi" }, Status = "ACTIVE" };
    db.ExamAttempts.Add(new() { ExamRegistration = registration, ExamVariant = variant, StartedAt = DateTime.UtcNow.AddDays(-3), SubmittedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(20), TotalScore = 10, Status = "SUBMITTED" }); await db.SaveChangesAsync();
}
var moved = await new QuestionBankService(db, service, TimeProvider.System).ReleaseCompletedAsync(default);
Console.WriteLine($"Seed complete in {builder.Database}; branch: {branch.Name}. {moved} used questions moved to practice.");
Console.WriteLine("Accounts: question_demo_gv / question_demo_tl / question_demo_pht / question_demo_ht");
Console.WriteLine("Default demo password: Question-demo-123! (or your TELO_QUESTION_DEMO_PASSWORD).");
