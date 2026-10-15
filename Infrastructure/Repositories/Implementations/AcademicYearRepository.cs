using System.Data;
using Domain.Entities.Academic;
using Infrastructure.Context;
using Infrastructure.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Infrastructure.Repositories.Implement;

public sealed class AcademicYearRepository(ApplicationDbContext context) : IAcademicYearRepository
{
    public async Task<AcademicYearCreateOutcome> TryAddAsync(
        AcademicYear academicYear,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            if (await context.AcademicYears.AsNoTracking().AnyAsync(
                year => year.Name == academicYear.Name, cancellationToken))
                return AcademicYearCreateOutcome.Conflict;

            context.AcademicYears.Add(academicYear);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AcademicYearCreateOutcome.Created;
        }
        catch (Exception exception) when (IsWriteConflict(exception))
        {
            return AcademicYearCreateOutcome.Conflict;
        }
    }

    public async Task<(IReadOnlyList<AcademicYear> Items, int TotalCount)> ListAsync(
        AcademicYearListFilter filter,
        CancellationToken cancellationToken)
    {
        var query = context.AcademicYears
            .Include(y => y.Semesters)
            .AsNoTracking()
            .AsQueryable();

        if (filter.Status is not null)
        {
            query = query.Where(year => year.Status == filter.Status);
        }

        if (filter.Search is not null)
        {
            query = query.Where(year => year.Name.Contains(filter.Search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(year => year.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToArrayAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<AcademicYear?> GetByIdWithSemestersAsync(
        ulong id,
        CancellationToken cancellationToken) =>
        context.AcademicYears
            .Include(year => year.Semesters)
            .FirstOrDefaultAsync(year => year.Id == id, cancellationToken);

    public Task<bool> HasConflictExceptCurrentAsync(
        ulong currentYearId,
        string name,
        CancellationToken cancellationToken) =>
        context.AcademicYears.AsNoTracking().AnyAsync(
            year => year.Id != currentYearId && year.Name == name,
            cancellationToken);

    public Task<bool> HasActiveYearAsync(
        ulong exceptYearId,
        CancellationToken cancellationToken) =>
        context.AcademicYears.AsNoTracking().AnyAsync(
            year => year.Id != exceptYearId && year.Status == "ACTIVE",
            cancellationToken);

    public async Task<bool> UpdateAsync(
        AcademicYear academicYear,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            var originalName = context.Entry(academicYear).Property(year => year.Name).OriginalValue;
            var originalStatus = context.Entry(academicYear).Property(year => year.Status).OriginalValue;
            var current = await context.Database.SqlQuery<YearState>($"SELECT name AS Name, status AS Status FROM academic_year WHERE academic_year_id = {academicYear.Id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (current is null || current.Name != originalName || current.Status != originalStatus) return false;
            context.ChangeTracker.DetectChanges();
            if (context.Entry(academicYear).Property(year => year.Name).IsModified && await HasConflictExceptCurrentAsync(
                academicYear.Id, academicYear.Name, cancellationToken))
                return false;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (Exception exception) when (IsWriteConflict(exception))
        {
            return false;
        }
    }

    private sealed class YearState { public string Name { get; set; } = ""; public string Status { get; set; } = ""; }

    private static bool IsWriteConflict(Exception exception) =>
        exception.GetBaseException() is MySqlException { Number: 1062 or 1213 or 1205 };

    public async Task<bool> TryActivateAsync(AcademicYear academicYear, CancellationToken cancellationToken)
    {
        academicYear.EnsureCanActivate();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            // Serialize activation on a stable year row, including when no year is active.
            // Locking reads see the latest committed status after waiting on another activation.
            await context.Database.SqlQueryRaw<ulong>(
                "SELECT academic_year_id AS Value FROM academic_year ORDER BY academic_year_id LIMIT 1 FOR UPDATE")
                .ToListAsync(cancellationToken);
            await context.Entry(academicYear).ReloadAsync(cancellationToken);
            if (academicYear.Status != "DRAFT") return false;
            var currentYears = await context.AcademicYears.FromSqlInterpolated(
                $"SELECT * FROM academic_year WHERE status = {"ACTIVE"} AND academic_year_id <> {academicYear.Id} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            foreach (var current in currentYears) current.Close();
            academicYear.Activate();
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (Exception exception) when (IsWriteConflict(exception))
        {
            return false;
        }
    }

    public Task<AcademicYear?> GetActiveYearAsync(CancellationToken cancellationToken) =>
        context.AcademicYears.AsNoTracking()
            .Where(y => y.Status == "ACTIVE")
            .OrderByDescending(y => y.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ulong?> GetActiveYearIdAsync(CancellationToken cancellationToken) =>
        context.AcademicYears.AsNoTracking()
            .Where(y => y.Status == "ACTIVE")
            .OrderByDescending(y => y.Id)
            .Select(y => (ulong?)y.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
