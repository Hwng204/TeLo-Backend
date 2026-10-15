namespace Domain.Entities.Academic;

public sealed class AcademicYear
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";

    public ICollection<Semester> Semesters { get; set; } = new List<Semester>();

    public void EnsureCanUpdateSchedule()
    {
        if (Status == "CLOSED")
        {
            throw new AcademicCalendarDomainException(
                "ACADEMIC_YEAR_CLOSED",
                "Năm học đã đóng không thể chỉnh sửa.");
        }
    }

    public void UpdateSchedule(string name)
    {
        EnsureCanUpdateSchedule();
        Name = name;
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

        // The academic-year workflow no longer depends on semesters. Existing
        // semester rows remain available to other modules without being changed.
    }

    public void Activate()
    {
        EnsureCanActivate();
        Status = "ACTIVE";
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

        if (Semesters.All(semester => semester.Order != order))
            throw new AcademicCalendarDomainException("TERM_NOT_FOUND", "Không tìm thấy học kỳ cần cấu hình.");
    }

    public void ConfigureTerm(byte order, string name)
    {
        EnsureCanConfigureTerm(order);
        Semesters.First(semester => semester.Order == order).Configure(name);
    }
}
