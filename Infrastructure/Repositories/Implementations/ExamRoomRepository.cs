using Domain.Entities.Examination;
using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implement;

public sealed class ExamRoomRepository(ApplicationDbContext context) : IExamRoomRepository
{
    public async Task<IReadOnlyList<ExamRoomData>> ListByExamAsync(
        ulong examId,
        CancellationToken cancellationToken) =>
        await BuildListByExamQuery(context, examId).ToArrayAsync(cancellationToken);

    internal static IQueryable<ExamRoomData> BuildListByExamQuery(
        ApplicationDbContext db,
        ulong examId) =>
        Project(db.ExamRooms.AsNoTracking()
            .Where(item => item.ExamId == examId)
            .OrderBy(item => item.Code)
            .ThenBy(item => item.Id));

    public async Task<IReadOnlyList<ExamRoomOptionData>> ListRoomOptionsAsync(
        ulong examId,
        CancellationToken cancellationToken) =>
        await context.Rooms.AsNoTracking()
            .Where(room => context.Exams.Any(exam =>
                exam.Id == examId && exam.SchoolBranchId == room.SchoolBranchId) &&
                room.Status == "ACTIVE")
            .OrderBy(room => room.Code)
            .ThenBy(room => room.Id)
            .Select(room => new ExamRoomOptionData(
                room.Id,
                room.Code,
                room.Name,
                room.RoomType,
                room.Status))
            .ToArrayAsync(cancellationToken);

    public Task<ExamRoomData?> GetDetailAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken) =>
        Project(context.ExamRooms.AsNoTracking().Where(item => item.ExamId == examId && item.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ExamRoom?> GetByIdAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken) =>
        context.ExamRooms.FirstOrDefaultAsync(
            item => item.ExamId == examId && item.Id == id,
            cancellationToken);

    public async Task<ExamRoom?> GetForDeleteAsync(
        ulong examId,
        ulong id,
        CancellationToken cancellationToken)
    {
        var matches = await context.ExamRooms
            .FromSqlInterpolated($"SELECT * FROM exam_rooms WHERE id = {id} AND exam_id = {examId} FOR UPDATE")
            .ToArrayAsync(cancellationToken);
        return matches.SingleOrDefault();
    }

    public Task<bool> ExamExistsAsync(ulong examId, CancellationToken cancellationToken) =>
        context.Exams.AsNoTracking().AnyAsync(exam => exam.Id == examId, cancellationToken);

    public Task<bool> RoomBelongsToExamBranchAsync(
        ulong examId,
        ulong roomId,
        CancellationToken cancellationToken) =>
        context.Exams.AsNoTracking().AnyAsync(
            exam => exam.Id == examId && exam.SchoolBranch.Rooms.Any(room =>
                room.Id == roomId && room.Status == "ACTIVE"),
            cancellationToken);

    public Task<bool> CodeExistsAsync(
        ulong examId,
        string code,
        ulong? excludedId,
        CancellationToken cancellationToken) =>
        context.ExamRooms.AsNoTracking().AnyAsync(
            item => item.ExamId == examId &&
                item.Code == code &&
                (!excludedId.HasValue || item.Id != excludedId.Value),
            cancellationToken);

    public Task<bool> RoomExistsAsync(
        ulong examId,
        ulong roomId,
        ulong? excludedId,
        CancellationToken cancellationToken) =>
        context.ExamRooms.AsNoTracking().AnyAsync(
            item => item.ExamId == examId &&
                item.RoomId == roomId &&
                (!excludedId.HasValue || item.Id != excludedId.Value),
            cancellationToken);

    public Task<bool> HasDependentDataAsync(ulong id, CancellationToken cancellationToken) =>
        context.SessionRooms.AsNoTracking().AnyAsync(item => item.ExamRoomId == id, cancellationToken);

    public async Task<ExamCandidateRosterData?> GetCandidateRosterAsync(
        ulong examId,
        CancellationToken cancellationToken)
    {
        var exam = await context.Exams.AsNoTracking()
            .Where(item => item.Id == examId)
            .Select(item => new
            {
                item.SchoolBranchId,
                AcademicYearId = item.Semester.AcademicYearId,
                item.GradeLevelId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (exam is null)
        {
            return null;
        }

        if (!exam.GradeLevelId.HasValue)
        {
            return new ExamCandidateRosterData(false, []);
        }

        var students = await context.StudentEnrollments.AsNoTracking()
            .Where(enrollment =>
                enrollment.SchoolClass.AcademicYearId == exam.AcademicYearId &&
                enrollment.EndedOn == null &&
                enrollment.Student.User.Status == StudentStatusCodes.Active &&
                enrollment.SchoolClass.Status == "ACTIVE" &&
                enrollment.SchoolClass.SchoolBranchId == exam.SchoolBranchId &&
                enrollment.SchoolClass.GradeLevelId == exam.GradeLevelId.Value)
            .OrderBy(enrollment => enrollment.Student.User.FullName)
            .ThenBy(enrollment => enrollment.Student.User.Username)
            .ThenBy(enrollment => enrollment.StudentId)
            .Select(enrollment => new ExamCandidateData(
                enrollment.StudentId,
                enrollment.Student.User.Username,
                enrollment.Student.User.FullName))
            .ToArrayAsync(cancellationToken);

        return new ExamCandidateRosterData(true, students);
    }

    public Task AddAsync(ExamRoom examRoom, CancellationToken cancellationToken) =>
        context.ExamRooms.AddAsync(examRoom, cancellationToken).AsTask();

    public void Delete(ExamRoom examRoom) => context.ExamRooms.Remove(examRoom);

    private static IQueryable<ExamRoomData> Project(IQueryable<ExamRoom> query) =>
        query.Select(item => new ExamRoomData(
            item.Id,
            item.ExamId,
            item.Code,
            item.RoomId,
            item.Room.Code,
            item.Room.Name,
            item.Room.RoomType,
            item.Room.Status,
            item.CandidateLimit,
            item.Sessions.Count));
}
