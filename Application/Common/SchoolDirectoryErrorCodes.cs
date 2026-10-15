namespace Application.Common;

// Error codes shared by student, class, and school services; the controllers map them to
// status codes, so they must stay stable.
public static class SchoolDirectoryErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string SchoolScopeRequired = "SCHOOL_SCOPE_REQUIRED";
    public const string SchoolNotFound = "SCHOOL_NOT_FOUND";
    public const string StudentNotFound = "STUDENT_NOT_FOUND";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string ActiveAcademicYearNotFound = "ACTIVE_ACADEMIC_YEAR_NOT_FOUND";
    public const string StudentCodeDuplicate = "STUDENT_CODE_DUPLICATE";
    public const string ClassCodeDuplicate = "CLASS_CODE_DUPLICATE";
    public const string TeacherAlreadyHomeroom = "TEACHER_ALREADY_HOMEROOM";
    public const string ClassHasActiveStudents = "CLASS_HAS_ACTIVE_STUDENTS";
    public const string ImportBatchNotFound = "IMPORT_BATCH_NOT_FOUND";
    public const string ImportFileInvalid = "IMPORT_FILE_INVALID";
    public const string ImportBatchStateInvalid = "IMPORT_BATCH_STATE_INVALID";
    public const string ImportHasInvalidRows = "IMPORT_HAS_INVALID_ROWS";
}
