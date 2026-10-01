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

        var first = Semesters.FirstOrDefault(term => term.Order == 1);
        var second = Semesters.FirstOrDefault(term => term.Order == 2);
        if (Semesters.Count != 2 || first is null || second is null ||
            Semesters.Any(term => string.IsNullOrWhiteSpace(term.Name) || term.Status == "CLOSED" ||
                term.StartDate is null || term.EndDate is null ||
                term.StartDate < StartDate || term.EndDate > EndDate || term.StartDate >= term.EndDate) ||
            string.Equals(first.Name.Trim(), second.Name.Trim(), StringComparison.OrdinalIgnoreCase) ||
            second.StartDate <= first.EndDate)
        {
            throw new AcademicCalendarDomainException(
                "INCOMPLETE_TERMS",
                "Cần đủ 2 học kỳ có tên riêng và ngày hợp lệ, nằm trong năm học, không chồng lấn trước khi kích hoạt.");
        }
    }

    public void Activate()
    {
        EnsureCanActivate();
        Status = "ACTIVE";
        var first = Semesters.Single(term => term.Order == 1);
        first.Status = "ACTIVE";
        first.Version++;
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

        if (Status != "ACTIVE")
        {
            throw new AcademicCalendarDomainException("ACADEMIC_YEAR_NOT_ACTIVE", "Chỉ được kết thúc năm học đang áp dụng.");
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

        var semester = Semesters.FirstOrDefault(semester => semester.Order == order)
            ?? throw new AcademicCalendarDomainException("TERM_NOT_FOUND", "Không tìm thấy học kỳ cần cấu hình.");
        semester.EnsureCanConfigure();
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

        if (Status != "ACTIVE")
            throw new AcademicCalendarDomainException("ACADEMIC_YEAR_NOT_ACTIVE", "Chỉ được kết thúc học kỳ của năm học đang áp dụng.");
        if (Semesters.Any(item => item.Order < semester.Order && item.Status != "CLOSED"))
            throw new AcademicCalendarDomainException("PREVIOUS_TERM_OPEN", "Cần kết thúc học kỳ I trước học kỳ II.");
        semester.Close();
        var next = Semesters.FirstOrDefault(item => item.Order == semester.Order + 1 && item.Status == "PLANNED");
        if (next is not null)
        {
            next.Status = "ACTIVE";
            next.Version++;
        }
        Version++;
        return semester;
    }
}
