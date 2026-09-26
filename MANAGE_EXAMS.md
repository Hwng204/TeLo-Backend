# Manage Exams

## Mục đích

Module Manage Exams cung cấp API quản trị vòng đời bản ghi kỳ thi: tạo, cập nhật, xóa an toàn, tìm kiếm/phân trang và xem chi tiết kèm số liệu tổng hợp. Module dùng entity/schema Examination hiện có và không thay đổi cấu trúc database.

## Kiến trúc và các file

Module tuân theo luồng Controller → Application Service → Repository/Unit of Work → EF Core:

- Domain: tái sử dụng `Domain/Entities/Examination/Exam.cs` và các quan hệ hiện có, không sửa entity.
- Application contracts: `Application/DTOs/ExamDtos.cs`.
- Application validation/status: `Application/Common/ExamValidator.cs`, `Application/Common/ExamStatusCodes.cs`.
- Application mapping/service: `Application/Mappings/ExamMappingExtensions.cs`, `Application/Services/Interfaces/IExamService.cs`, `Application/Services/Implementations/ExamService.cs`.
- Application DI: `Application/DependencyInjection.cs`.
- Infrastructure repository: `Infrastructure/Repositories/Interfaces/IExamRepository.cs`, `Infrastructure/Repositories/Implementations/ExamRepository.cs`.
- Infrastructure Unit of Work/DI: `Infrastructure/UnitOfWork/IUnitOfWork.cs`, `Infrastructure/UnitOfWork/UnitOfWork.cs`, `Infrastructure/DependencyInjection.cs`.
- Web API: `WebAPI/Controllers/ExamsController.cs`.
- Tests: `Tests/Application.Tests/ExamServiceTests.cs`, `Tests/WebAPI.Tests/ExamsControllerTests.cs`; các fake Unit of Work cũ được cập nhật để đáp ứng contract mới.

Repository dùng `AsNoTracking`, projection và `Count` dạng correlated subquery cho read model. List áp dụng filter, sort, `Skip` và `Take` trước khi materialize. Detail không tải các collection lớn và không phát sinh N+1.

## API và phân quyền

Tất cả endpoint yêu cầu JWT thỏa policy `OperationalAdmin` hiện có: role `OperationalAdmin` hoặc claim `permission=academic_calendar.manage`.

| Chức năng | HTTP | Route | Thành công |
|---|---|---|---|
| Tạo kỳ thi | POST | `/api/exams` | 201 |
| Danh sách | GET | `/api/exams` | 200 |
| Chi tiết | GET | `/api/exams/{id}` | 200 |
| Cập nhật | PUT | `/api/exams/{id}` | 200 |
| Xóa | DELETE | `/api/exams/{id}` | 200 |

Các lỗi nghiệp vụ trả trong `ApiResponse` với status 404, 409 hoặc 422. Lỗi không xử lý được đi qua global exception handler và không lộ stack trace/database detail.

### POST `/api/exams`

Request:

```json
{
  "semesterId": 12,
  "schoolBranchId": 3,
  "name": "Kiểm tra cuối học kỳ I",
  "startDate": "2026-12-10",
  "endDate": "2026-12-20"
}
```

Trạng thái tạo mới luôn là `DRAFT`.

### PUT `/api/exams/{id}`

Request:

```json
{
  "semesterId": 12,
  "schoolBranchId": 3,
  "name": "Kiểm tra cuối học kỳ I",
  "startDate": "2026-12-11",
  "endDate": "2026-12-21",
  "status": "SCHEDULED"
}
```

Update chỉ thay các scalar field được công khai ở trên; không ghi đè navigation collection.

### GET `/api/exams`

Query parameters:

| Tên | Ý nghĩa |
|---|---|
| `keyword` | Tìm theo tên, tối đa 255 ký tự |
| `semesterId` | Lọc theo học kỳ |
| `schoolBranchId` | Lọc theo cơ sở |
| `status` | Lọc theo trạng thái |
| `fromDate` | Lấy kỳ thi có khoảng thời gian giao với cửa sổ từ ngày này |
| `toDate` | Lấy kỳ thi có khoảng thời gian giao với cửa sổ đến ngày này |
| `pageNumber` | Trang, mặc định 1 |
| `pageSize` | Kích thước trang 1–100, mặc định 20 |
| `sortBy` | `name`, `startDate`, `endDate`, `status`, `semester`, `schoolBranch` |
| `sortDirection` | `asc` hoặc `desc`; mặc định `startDate desc` |

List chỉ trả semester/branch rút gọn, không trả collection con.

### GET `/api/exams/{id}`

Response data mẫu:

```json
{
  "id": 25,
  "name": "Kiểm tra cuối học kỳ I",
  "semester": { "id": 12, "name": "Học kỳ 1" },
  "schoolBranch": { "id": 3, "code": "CS1", "name": "Cơ sở 1" },
  "startDate": "2026-12-10",
  "endDate": "2026-12-20",
  "status": "DRAFT",
  "subjectCount": 4,
  "sessionCount": 8,
  "roomCount": 6,
  "candidateCount": 320,
  "proctorCount": 18
}
```

### DELETE `/api/exams/{id}`

Đây là hard delete vì schema hiện tại không có soft-delete field. API chỉ xóa khi Exam không có `ExamSubject`, `ExamRoom` hoặc `ExamProctor`. Các quan hệ sâu hơn (session, registration, attempt, assignment) đều đi qua ba aggregate con này, nên kiểm tra trên ngăn cascade ngoài ý muốn. Khi có dữ liệu phụ thuộc API trả 409 với code `EXAM_HAS_DEPENDENCIES`.

Source hiện không quy định chỉ được xóa ở trạng thái cụ thể, vì vậy module không tự thêm restriction theo trạng thái.

## Validation và business rules

- `name` được trim, bắt buộc và tối đa 255 ký tự theo column.
- `startDate` phải nhỏ hơn hoặc bằng `endDate`.
- `semesterId` và `schoolBranchId` phải tồn tại.
- Status hợp lệ: `DRAFT`, `SCHEDULED`, `ONGOING`, `COMPLETED`, `CANCELLED`.
- Create không nhận status và dùng `DRAFT`.
- `pageSize` giới hạn 1–100; sort field/direction được whitelist.
- Bộ lọc ngày dùng quy tắc giao nhau: `exam.EndDate >= fromDate` và `exam.StartDate <= toDate`.
- Schema không có unique index tên Exam, vì vậy module không tự áp đặt unique name.
- Source không có rule yêu cầu ngày Exam nằm trong ngày Semester; module chỉ kiểm tra tham chiếu Semester tồn tại.

## Database và migration

Không có migration mới. Các bảng, cột, FK và index hiện tại đã đủ cho CRUD. Không chạy `database update`.

## Build và test

Từ thư mục solution:

```bash
dotnet restore TeLoSchoolManagement.sln
dotnet build TeLoSchoolManagement.sln --no-restore
dotnet test TeLoSchoolManagement.sln --no-build
```

Tests bao phủ create thành công/tham chiếu thiếu/ngày sai, update thành công/not-found/status sai, delete thành công/bị chặn, list filter/pagination, detail count/not-found, response controller và authorization không có JWT.

Kết quả xác minh tại môi trường phát triển hiện tại:

- `dotnet build TeLoSchoolManagement.sln --no-restore -m:1`: thành công, 0 warning, 0 error.
- `dotnet test TeLoSchoolManagement.sln --no-build --no-restore -m:1`: thành công, 71/71 tests.
- `dotnet restore TeLoSchoolManagement.sln --disable-parallel`: môi trường dùng SDK 9.0.301 trả exit code 1 trong bước `_GenerateRestoreProjectPathWalk` nhưng không phát sinh NuGet/package error (`Build FAILED`, 0 warnings, 0 errors). Các project độc lập `Domain` và `Infrastructure` restore thành công; build/test dùng assets hiện có thành công. Hãy kiểm tra lại bằng .NET 8 SDK hoặc repair workload/SDK 9.0.301 rồi chạy lại lệnh restore ở trên.

## Assumptions và TODO

- Domain chưa có enum, constants hoặc state transition cho Exam. Bộ status ở trên được tập trung trong `ExamStatusCodes` để tránh magic string, nhưng cần được product/domain owner xác nhận. Không áp đặt transition giữa các status.
- Chưa có permission riêng cho quản lý kỳ thi; endpoint tái sử dụng policy quản trị vận hành hiện có. Khi hệ thống bổ sung permission chính thức cho Exam, cập nhật policy/controller tương ứng.
- Không có audit fields/soft-delete fields trên `Exam`, nên response không thể cung cấp audit metadata và delete là hard delete có guard.
- Không có unique index cho tên Exam. Nếu nghiệp vụ xác nhận tính duy nhất trong semester/branch, cần thêm database constraint bằng migration riêng để chống race condition, không chỉ kiểm tra ở application.
