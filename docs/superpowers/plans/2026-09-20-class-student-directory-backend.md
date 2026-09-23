# Kế hoạch backend: Class & Student Directory, lịch sử điểm và CRUD admin

> Revision: 2026-09-20
> Phạm vi: backend cho SC-CL01, SC-CL02, SC-HS01, SC-HS02.
> Không thuộc phạm vi: upload/parse/đối soát Excel và frontend.

## 1. Nghiệp vụ đã chốt

- Phía trường và admin hệ thống đều được xem danh sách/chi tiết lớp và học sinh.
- Phía trường chỉ được đọc dữ liệu của trường mình. Các role đọc gồm:
  - hiệu trưởng: `HIEU_TRUONG`, `PRINCIPAL`;
  - phó hiệu trưởng: `PHT`;
  - giáo viên: `GIAO_VIEN`, `TEACHER`;
  - tổ trưởng: `TEAM_LEAD`, `TO_TRUONG`.
- Phía trường không có API tạo/sửa/xóa lớp hoặc học sinh.
- Admin hệ thống được đọc và CRUD theo một `schoolId` được chỉ rõ trong route.
- Tên lớp và tên học sinh trên bảng là link frontend; backend phải trả `id` ổn định trong từng row.
- Student detail trả lịch sử các lớp đã học. Frontend dùng lịch sử này làm dropdown.
- Khi chọn một lớp trong dropdown, frontend gọi API điểm theo `classId`; backend chỉ trả điểm đã công bố.
- Thao tác xóa là xóa logic để không làm mất lịch sử lớp và điểm thi.

## 2. Đánh giá code hiện tại

### 2.1 Đã đáp ứng

| Yêu cầu | Trạng thái hiện tại | Bằng chứng |
|---|---|---|
| Student list | Có | `GET /api/students`, có search/filter/paging |
| Student detail | Có một phần | Trả hồ sơ, lớp hiện tại, `AcademicHistory` |
| Class list | Có | `GET /api/classes`, có filter/paging |
| Class detail + student roster | Có | `GET /api/classes/{id}`, roster có `studentId` |
| Link sang detail | Backend đã đủ dữ liệu | `StudentListItem.Id`, `ClassListItem.Id`, `ClassStudentItem.StudentId` |
| Cô lập dữ liệu phía trường | Có | Scope lấy từ `users.school_branch_id -> school_id`, không lấy school từ query |
| Lịch sử học sinh | Có | `StudentEnrollment` lưu theo học sinh + năm học + lớp |
| Phía trường không CRUD | Đúng | Controller hiện chỉ khai báo `GET` |
| Migration read model | Có | `20260919192748_AddClassStudentDirectoryReadModel` |

### 2.2 Chưa đáp ứng hoặc chưa đủ

| Mức độ | Khoảng trống | Vị trí hiện tại | Hệ quả |
|---|---|---|---|
| Blocker | Policy đọc chỉ gồm hiệu trưởng và PHT | `WebAPI/Program.cs` | Giáo viên và tổ trưởng bị `403` |
| Blocker | Admin hệ thống không vào được directory | `SchoolDirectoryRead` không gồm admin; repository bắt buộc actor có branch | Admin không xem được list/detail nếu không gắn vào một trường |
| Blocker | Chưa có API điểm theo lớp | DTO/service/repository/controller directory | Dropdown có thể hiển thị lịch sử nhưng không có dữ liệu cho bảng điểm |
| Blocker | Chưa có CRUD admin | Chỉ có các action `GET` | Chưa đáp ứng quy trình admin cập nhật thủ công |
| High | Chưa có test role matrix | API test chỉ kiểm tra `PHT`, `PRINCIPAL`, `STUDENT` | Dễ tái phát lỗi phân quyền |
| High | Policy `OperationalAdmin` kiểm tra claim `permission` nhưng JWT không phát claim này | `Program.cs`, `JwtTokenService.cs` | Nhánh `academic_calendar.manage` không hoạt động qua login thông thường |
| Medium | Các project test directory mới chưa nằm trong solution | `Application.Tests`, `Infrastructure.Tests`, `WebAPI.IntegrationTests` | `dotnet test TeLoSchoolManagement.sln` không chạy các test này |
| Medium | Chưa có trạng thái xóa logic cho học sinh | `StudentStatusCodes` | Không thể xóa an toàn mà vẫn giữ lịch sử/điểm |

### 2.3 Giới hạn dữ liệu hiện tại

`StudentEnrollment` đang unique theo `(StudentId, AcademicYearId)`. Vì vậy bản tối thiểu coi mỗi học sinh thuộc một lớp trong một năm học. Điểm của lớp được suy ra qua:

`ExamAttempt -> ExamRegistration -> ExamSubjectGradeLevel -> ExamSubject -> Exam -> Semester.AcademicYearId`

và khớp với `StudentEnrollment.AcademicYearId`, grade và branch của lớp được chọn.

Không thêm bảng điểm mới và không thêm `StudentEnrollmentId` vào đăng ký thi trong phase này. Chỉ nâng cấp schema khi nghiệp vụ cần lưu chính xác nhiều lần chuyển lớp trong cùng một năm học.

## 3. API contract đích

### 3.1 Phía trường — read-only, tự scope theo JWT actor

Giữ các API hiện tại:

```http
GET /api/students
GET /api/students/{studentId}
GET /api/classes
GET /api/classes/reference-data
GET /api/classes/{classId}
```

Thêm:

```http
GET /api/students/{studentId}/scores?classId={classId}&page=1&pageSize=20
```

Quy tắc:

- không nhận `schoolId` từ phía trường;
- student/class/classId ngoài trường trả `404`, không làm lộ dữ liệu;
- điểm chỉ trả khi `ExamSubject.ResultPublishedAt != null` và `ExamAttempt.TotalScore != null`;
- class được chọn phải nằm trong `AcademicHistory` của học sinh tại trường actor;
- sort mặc định: ngày thi giảm dần, sau đó tên môn.

### 3.2 Admin hệ thống — scope trường rõ trong route

```http
GET    /api/admin/schools/{schoolId}/students
GET    /api/admin/schools/{schoolId}/students/{studentId}
GET    /api/admin/schools/{schoolId}/students/{studentId}/scores?classId={classId}
POST   /api/admin/schools/{schoolId}/students
PUT    /api/admin/schools/{schoolId}/students/{studentId}
DELETE /api/admin/schools/{schoolId}/students/{studentId}

GET    /api/admin/schools/{schoolId}/classes
GET    /api/admin/schools/{schoolId}/classes/reference-data
GET    /api/admin/schools/{schoolId}/classes/{classId}
POST   /api/admin/schools/{schoolId}/classes
PUT    /api/admin/schools/{schoolId}/classes/{classId}
DELETE /api/admin/schools/{schoolId}/classes/{classId}
```

Không nhân đôi query. Admin controller truyền explicit `schoolId`; school controller dùng school được resolve từ actor; cả hai gọi chung service/repository theo scope đã xác thực.

### 3.3 DTO điểm

```text
StudentScoreItem
- attemptId
- examId, examName
- semesterId, semesterName
- subjectId, subjectName
- examDate
- totalScore
- resultPublishedAt
```

Response dùng `DirectoryPage<StudentScoreItem>` hiện có.

### 3.4 DTO CRUD tối thiểu

`CreateStudentRequest`:

- `code`, `fullName`, `dateOfBirth`, `gender`, `admissionDate`, `status`;
- `schoolClassId` bắt buộc để tạo enrollment đầu tiên.

`UpdateStudentRequest`:

- các trường hồ sơ trên;
- `schoolClassId` tùy chọn:
  - lớp thuộc năm học mới: thêm enrollment mới;
  - lớp khác trong cùng năm: cập nhật enrollment hiện có;
  - luôn kiểm tra lớp thuộc `schoolId` của route.

`CreateClassRequest` / `UpdateClassRequest`:

- `schoolBranchId`, `code`, `name`, `academicYearId`, `gradeLevelId`, `status`;
- `homeroomTeacherId` tùy chọn; giáo viên phải thuộc cùng trường và chưa chủ nhiệm lớp khác.

Không thêm DTO bulk/Excel trong kế hoạch này.

## 4. Phân quyền đích

Thêm section cấu hình dùng đúng pattern role-code hiện tại:

```json
"SchoolDirectoryAuth": {
  "SchoolReadRoleCodes": [
    "HIEU_TRUONG", "PRINCIPAL", "PHT",
    "GIAO_VIEN", "TEACHER", "TEAM_LEAD", "TO_TRUONG"
  ],
  "AdminRoleCodes": [ "OperationalAdmin" ]
}
```

Policies:

- `SchoolDirectorySchoolRead`: chỉ các role phía trường ở trên;
- `SchoolDirectoryAdmin`: chỉ role admin hệ thống;
- không dùng claim `permission` cho directory vì token hiện không phát claim này.

Role code thực tế trong database phải được kiểm tra trước khi deploy; nếu dùng alias khác thì chỉ bổ sung config, không sửa controller.

## 5. Quy tắc CRUD

### Student

- Code học sinh vẫn unique toàn hệ thống theo constraint hiện tại.
- Create là một transaction: tạo student + enrollment.
- Update không được chuyển học sinh sang class ngoài trường của route.
- `DELETE` là xóa logic:
  - đổi student status thành `INACTIVE`;
  - đổi enrollment đang `ACTIVE` thành `TRANSFERRED_OUT`;
  - giữ nguyên exam registration/attempt và lịch sử cũ.
- Thêm `INACTIVE` vào `StudentStatusCodes` và check constraint bằng migration mới; không sửa migration đã có.

### Class

- Code class unique trong `(schoolBranchId, academicYearId)` theo constraint hiện tại.
- Branch phải thuộc school của route.
- Academic year phải phù hợp province của school.
- `DELETE` là đổi status thành `INACTIVE`.
- Nếu còn enrollment `ACTIVE`, trả `409 CLASS_HAS_ACTIVE_STUDENTS`; admin phải chuyển/đóng enrollment trước.
- Không hard-delete class để tránh mất lịch sử.

### HTTP/error mapping

- `400`: JSON/model binding sai;
- `401`: chưa đăng nhập;
- `403`: role không đủ quyền hoặc school actor thiếu scope;
- `404`: school/student/class không tồn tại trong scope;
- `409`: duplicate code, teacher đã chủ nhiệm lớp khác, class còn học sinh active;
- `422`: vi phạm business rule/giá trị enum/paging;

## 6. Kế hoạch implement

### Phase 0 — Khóa baseline test

Files:

- `TeLoSchoolManagement.sln`
- các `.csproj` test directory hiện có.

Tasks:

1. Add `Application.Tests`, `Infrastructure.Tests`, `WebAPI.IntegrationTests` vào solution hiện tại; không tạo thêm test project.
2. Giữ các test school isolation/read hiện có.
3. Chuẩn hóa command verify để solution thực sự chạy toàn bộ test.

Checkpoint:

- `dotnet test TeLoSchoolManagement.sln --no-restore` chạy cả ba project directory mới.

### Phase 1 — Sửa authorization và admin scope

Files chính:

- `WebAPI/Program.cs`
- `WebAPI/appsettings.json`
- `WebAPI/Controllers/SchoolDirectoryControllerBase.cs`
- `WebAPI.IntegrationTests/SchoolDirectoryApiTests.cs`

Tasks:

1. Tách policy school-read và admin.
2. Thêm role giáo viên/tổ trưởng và test từng alias.
3. Giữ school scope lấy từ actor cho route hiện tại.
4. Tạo admin controllers với `schoolId` bắt buộc trên route; không yêu cầu admin có branch.
5. Test negative matrix: `STUDENT`/unknown role bị `403`; school role không gọi được admin route; admin không gọi được write route nếu thiếu role.

Checkpoint:

- tất cả role đã nêu xem được đúng route;
- school A không xem được school B;
- admin xem được school được chỉ định mà không cần `SchoolBranchId` trên user.

### Phase 2 — Điểm theo lớp trong Student detail

Files chính:

- `Application/DTOs/SchoolDirectoryDtos.cs`
- `Application/Services/Interfaces/ISchoolDirectoryService.cs`
- `Application/Services/Implementations/SchoolDirectoryService.cs`
- `Infrastructure/Repositories/Interfaces/ISchoolDirectoryRepository.cs`
- `Infrastructure/Repositories/Implementations/SchoolDirectoryRepository.cs`
- `WebAPI/Controllers/StudentsController.cs`
- admin student controller từ Phase 1.

Tasks:

1. Viết repository integration test cho score query trước.
2. Query theo student + selected class + academic year + grade + branch.
3. Chỉ project kết quả đã publish và có `TotalScore`.
4. Thêm paging/validation và map `StudentScoreItem`.
5. Mở school/admin score endpoints dùng chung service method.

Checkpoint:

- class dropdown lấy trực tiếp từ `AcademicHistory`;
- chọn mỗi class chỉ trả đúng điểm của năm/grade/branch tương ứng;
- kết quả chưa publish không xuất hiện;
- class ngoài history hoặc ngoài scope trả `404`.

### Phase 3 — CRUD class cho admin

Files chính:

- DTO/service/repository directory hiện có;
- `WebAPI/Controllers/Admin/ClassesAdminController.cs`;
- `Domain/Entities/Organization/SchoolClass.cs` nếu cần gom status constants;
- test application/infrastructure/API hiện có.

Tasks:

1. Viết test create/update/deactivate và validation scope.
2. Reuse `SchoolClass`, `Teacher.ClassId`, unique constraints hiện có.
3. Implement create/update trong transaction EF Core khi thay giáo viên chủ nhiệm.
4. Implement logical delete và conflict khi class còn active enrollment.
5. Map duplicate constraint sang `409`, không để lộ exception database.

Checkpoint:

- chỉ admin gọi được write APIs;
- school role vẫn chỉ có GET;
- không thể gán branch/teacher ngoài school;
- deactivated class vẫn còn trong lịch sử nhưng không nhận enrollment active mới.

### Phase 4 — CRUD student cho admin

Files chính:

- `Domain/Entities/Identity/StudentEnrollment.cs`
- `Infrastructure/Configurations/Identity/StudentConfiguration.cs`
- DTO/service/repository directory hiện có;
- `WebAPI/Controllers/Admin/StudentsAdminController.cs`;
- migration mới sau `AddClassStudentDirectoryReadModel`.

Tasks:

1. Viết test create profile + enrollment trong cùng transaction.
2. Viết test update profile, chuyển class cùng năm và thêm enrollment ở năm mới.
3. Thêm `INACTIVE` và migration cập nhật check constraint.
4. Implement logical delete; đóng active enrollment, không đụng exam data.
5. Map duplicate student code sang `409`.

Checkpoint:

- create lỗi enrollment thì không để lại student mồ côi;
- mỗi student tối đa một enrollment mỗi năm theo constraint hiện tại;
- delete không xóa academic history hoặc score;
- student inactive không còn bị tính vào active roster.

### Phase 5 — Verification và bàn giao

1. `dotnet build TeLoSchoolManagement.sln --no-restore`.
2. `dotnet test TeLoSchoolManagement.sln --no-restore`.
3. Dùng database test riêng cho repository/migration; không chạy vào database dev dùng chung.
4. `dotnet ef migrations list --project Infrastructure --startup-project WebAPI`.
5. Apply migration vào database test và smoke các luồng:
   - từng school role xem list/detail;
   - school isolation;
   - admin chọn school và CRUD;
   - student history -> chọn class -> score;
   - logical delete vẫn giữ history/score.
6. Kiểm tra OpenAPI chỉ hiển thị write routes dưới `/api/admin/...`.

## 7. Acceptance checklist

- [ ] Hiệu trưởng, PHT, giáo viên và tổ trưởng xem được student/class list và detail của trường mình.
- [ ] Admin hệ thống xem được dữ liệu của school được chọn mà không cần gắn branch.
- [ ] School roles không thể POST/PUT/DELETE.
- [ ] Student/class list và class roster trả ID để frontend tạo link detail.
- [ ] Student detail trả đủ academic history cho dropdown.
- [ ] Score API lọc đúng class và chỉ trả kết quả đã publish.
- [ ] Admin CRUD class/student có school-scope validation.
- [ ] Delete là logical delete và không phá lịch sử/điểm.
- [ ] Duplicate/conflict trả `409`; validation trả `422`.
- [ ] Migration mới không sửa migration đã có.
- [ ] Tất cả test project được solution chạy và pass trên môi trường đủ connection string.

## 8. Explicitly skipped

- Upload/template Excel, detect diff, duyệt import và batch CRUD.
- Frontend `<a href>`/router implementation; backend chỉ đảm bảo các ID cần thiết.
- Lịch sử nhiều lần chuyển lớp trong cùng một năm học.
- Hard delete student/class.
- Permission-claim framework mới; role policies hiện tại đủ cho phạm vi này.
