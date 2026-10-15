using Domain.Entities.Academic;
using Domain.Entities.Identity;
using Domain.Entities.Examination;
using Domain.Entities.Notification;
using Domain.Entities.Organization;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Context;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<SchoolBranch> SchoolBranches => Set<SchoolBranch>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Navbar> Navbars => Set<Navbar>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<ExamMatrix> ExamMatrices => Set<ExamMatrix>();
    public DbSet<MatrixDetail> MatrixDetails => Set<MatrixDetail>();
    public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();
    public DbSet<QuestionTask> QuestionTasks => Set<QuestionTask>();
    public DbSet<QuestionTaskDetail> QuestionTaskDetails => Set<QuestionTaskDetail>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamSet> ExamSets => Set<ExamSet>();
    public DbSet<ExamSetQuestion> ExamSetQuestions => Set<ExamSetQuestion>();
    public DbSet<ExamVariant> ExamVariants => Set<ExamVariant>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamSubject> ExamSubjects => Set<ExamSubject>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<Violation> Violations => Set<Violation>();
    public DbSet<TestResult> TestResults => Set<TestResult>();
    public DbSet<NotificationConfig> NotificationConfigs => Set<NotificationConfig>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
