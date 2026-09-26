namespace Domain.Entities.Academic;

public sealed class AcademicYear
{
    public ulong Id { get; set; }
    public string? Code { get; private set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    /// <summary>Computed column: non-null only when status is ACTIVE; enforces one active year system-wide.</summary>
    public string? ActiveSystemKey { get; private set; }
    public uint Version { get; set; } = 1;

    public ICollection<Semester> Semesters { get; set; } = new List<Semester>();

    public void AssignCode(string code)
    {
        if (!string.IsNullOrEmpty(Code))
        {
            throw new InvalidOperationException("Academic year code is immutable once assigned.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Length > 64)
        {
            throw new ArgumentException("Academic year code must contain 1 to 64 characters.", nameof(code));
        }

        Code = code;
    }

    public void EnsureCanUpdateSchedule(DateOnly startDate, DateOnly endDate)
    {
        if (Status == "CLOSED")
        {
            throw new AcademicCalendarDomainException(
                "ACADEMIC_YEAR_CLOSED",
                "Năm học đã đóng không thể chỉnh sửa.");
        }

        foreach (var semester in Semesters)
        {
            if (semester.StartDate.HasValue && semester.StartDate.Value < startDate)
            {
                throw new AcademicCalendarDomainException(
                    "SEMESTER_OUT_OF_BOUNDS",
                    $"Ngày bắt đầu năm học ({startDate:dd/MM/yyyy}) không thể sau ngày bắt đầu của {semester.Name} ({semester.StartDate.Value:dd/MM/yyyy}).");
            }

            if (semester.EndDate.HasValue && semester.EndDate.Value > endDate)
            {
                throw new AcademicCalendarDomainException(
                    "SEMESTER_OUT_OF_BOUNDS",
                    $"Ngày kết thúc năm học ({endDate:dd/MM/yyyy}) không thể trước ngày kết thúc của {semester.Name} ({semester.EndDate.Value:dd/MM/yyyy}).");
            }
        }
    }

    public void UpdateSchedule(string name, DateOnly startDate, DateOnly endDate)
    {
        EnsureCanUpdateSchedule(startDate, endDate);
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Version++;
    }

    public void EnsureCanActivate()
    {
        if (Status == "ACTIVE")
        {
            throw new AcademicCalendarDomainException(
                "ACADEMIC_YEAR_ALREADY_ACTIVE",
                "Năm học này đang ở trạng thái áp dụng.");
        }

        if (Status == "CLOSED")
        {
            throw new AcademicCalendarDomainException(
                "ACADEMIC_YEAR_CLOSED",
                "Năm học đã đóng không thể kích hoạt lại.");
        }

        if (Semesters.Count != 2)
        {
            throw new AcademicCalendarDomainException(
                "INCOMPLETE_TERMS",
                "Năm học cần có đầy đủ 2 học kỳ trước khi kích hoạt.");
        }
    }

    public void Activate()
    {
        EnsureCanActivate();
        Status = "ACTIVE";
        Version++;
    }

    public void Close()
    {
        if (Status == "CLOSED")
        {
            throw new AcademicCalendarDomainException(
                "ACADEMIC_YEAR_ALREADY_CLOSED",
                "Năm học đã đóng từ trước.");
        }

        Status = "CLOSED";
        Version++;

        foreach (var semester in Semesters)
        {
            semester.CloseWithAcademicYear();
        }
    }

    public void EnsureCanConfigureTerms()
    {
        if (Status == "CLOSED")
        {
            throw new AcademicCalendarDomainException(
                "ACADEMIC_YEAR_CLOSED",
                "Năm học đã đóng không thể cấu hình học kỳ.");
        }
    }

    public void EnsureCanConfigureTerm(byte order)
    {
        EnsureCanConfigureTerms();

        Semesters.FirstOrDefault(semester => semester.Order == order)?
            .EnsureCanConfigure();
    }

    public void ConfigureTerm(
        byte order,
        string name,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        EnsureCanConfigureTerm(order);
        Semesters.FirstOrDefault(semester => semester.Order == order)?
            .Configure(name, startDate, endDate);
    }

    public Semester CloseTerm(ulong termId)
    {
        var semester = Semesters.FirstOrDefault(item => item.Id == termId)
            ?? throw new AcademicCalendarDomainException(
                "TERM_NOT_FOUND",
                "Không tìm thấy học kỳ.");

        semester.Close();
        return semester;
    }
}
