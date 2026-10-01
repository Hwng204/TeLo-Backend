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
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            // The conflict read and insert share range locks, including overlapping years with different names.
            if (await context.AcademicYears.AsNoTracking().AnyAsync(
                year => year.Name == academicYear.Name ||
                    (year.StartDate <= academicYear.EndDate && academicYear.StartDate <= year.EndDate), cancellationToken))
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
            .OrderByDescending(year => year.StartDate)
            .ThenByDescending(year => year.Id)
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
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken) =>
        context.AcademicYears.AsNoTracking().AnyAsync(
            year => year.Id != currentYearId &&
                    (year.Name == name ||
                     (year.StartDate <= endDate && startDate <= year.EndDate)),
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
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            context.ChangeTracker.DetectChanges();
            var entry = context.Entry(academicYear);
            var scheduleChanged = entry.Property(year => year.Name).IsModified ||
                entry.Property(year => year.StartDate).IsModified || entry.Property(year => year.EndDate).IsModified;
            if (scheduleChanged && await HasConflictExceptCurrentAsync(
                academicYear.Id, academicYear.Name, academicYear.StartDate, academicYear.EndDate, cancellationToken))
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

    private static bool IsWriteConflict(Exception exception) =>
        exception.GetBaseException() is MySqlException { Number: 1062 or 1213 or 1205 };
}
