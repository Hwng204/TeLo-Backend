Bản dưới đây giữ database `sep`, dùng user riêng, đồng nhất `localhost`, bổ sung `dotnet tool restore` và tránh danh sách migration bị lỗi thời.

````markdown
# TeLo Backend - chạy với MySQL local

Backend sử dụng .NET 8, EF Core 8 và MySQL 8. Docker không bắt buộc.

Toàn đội sử dụng EF Migration làm nguồn chuẩn của schema. Không tự tạo hoặc sửa bảng thủ công trong MySQL Workbench.

## 1. Yêu cầu

- .NET SDK 8 trở lên.
- MySQL Server 8.0.16 trở lên.
- MySQL Workbench để tạo database/user, xem và truy vấn dữ liệu.

Kiểm tra phiên bản .NET:

```powershell
dotnet --version
```

Kiểm tra MySQL trên Windows:

```powershell
Get-Service MySQL80
```

Nếu service chưa chạy, mở PowerShell bằng quyền Administrator:

```powershell
Start-Service MySQL80
```

## 2. Tạo database phát triển

Mở MySQL Workbench bằng tài khoản quản trị và chạy:

```sql
CREATE DATABASE IF NOT EXISTS sep
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

CREATE USER IF NOT EXISTS 'telo_app'@'localhost'
  IDENTIFIED BY 'THAY_BANG_MAT_KHAU_LOCAL_CUA_BAN';

ALTER USER 'telo_app'@'localhost'
  IDENTIFIED BY 'THAY_BANG_MAT_KHAU_LOCAL_CUA_BAN';

GRANT ALL PRIVILEGES ON sep.* TO 'telo_app'@'localhost';

FLUSH PRIVILEGES;
```

Thay `THAY_BANG_MAT_KHAU_LOCAL_CUA_BAN` bằng mật khẩu riêng trên máy của bạn.

Không đưa mật khẩu thật vào source code, ảnh chụp, chat, issue hoặc Git.

## 3. Lưu connection string an toàn

Chạy tại thư mục `LeTo-Backend`:

```powershell
dotnet user-secrets set `
  "ConnectionStrings:DefaultConnection" `
  "Server=localhost;Port=3306;Database=sep;User=telo_app;Password=MAT_KHAU_LOCAL;Allow User Variables=true;" `
  --project WebAPI
```

Thay `MAT_KHAU_LOCAL` bằng mật khẩu đã tạo ở bước 2.

Kiểm tra User Secrets:

```powershell
dotnet user-secrets list --project WebAPI
```

Lưu ý: kết quả có chứa connection string và mật khẩu. Chỉ kiểm tra trên máy cá nhân, không sao chép hoặc chia sẻ kết quả.

## 4. Khôi phục công cụ và dependencies

```powershell
dotnet tool restore
dotnet restore TeLoSchoolManagement.sln
```

Kiểm tra danh sách migration hiện có:

```powershell
dotnet tool run dotnet-ef migrations list `
  --project Infrastructure `
  --startup-project WebAPI
```

## 5. Tạo hoặc cập nhật schema bằng EF Migration

```powershell
dotnet tool run dotnet-ef database update `
  --project Infrastructure `
  --startup-project WebAPI
```

EF lưu các migration đã áp dụng trong bảng `__EFMigrationsHistory` và chỉ chạy những migration còn thiếu.

Không chạy file `CREATE TABLE` thủ công để tránh schema local khác với schema trong source code.

## 6. Build và test

Trong một số môi trường Windows, shared compiler có thể bị chặn. Các lệnh sau phù hợp để kiểm tra dự án:

```powershell
dotnet build TeLoSchoolManagement.sln `
  --disable-build-servers `
  -m:1 `
  -p:UseSharedCompilation=false

dotnet test TeLoSchoolManagement.sln `
  --disable-build-servers `
  -m:1 `
  -p:UseSharedCompilation=false
```

## 7. Chạy API

```powershell
dotnet run --project WebAPI --launch-profile https
```

Swagger:

- `https://localhost:7033/swagger`
- `http://localhost:5035/swagger`

Nếu máy chưa tin cậy HTTPS development certificate:

```powershell
dotnet dev-certs https --trust
```

## 8. Module Ma trận đề thi

API ma trận, đăng nhập JWT và quy trình nghiệp vụ: xem [MATRIX.md](MATRIX.md).

JWT cần `Jwt:SigningKey` tối thiểu 32 ký tự. Cấu hình bằng user secrets hoặc biến môi trường, không commit khóa thật.

## 9. Quy tắc đồng bộ database của đội

1. Thay đổi entity và EF configuration.
2. Tạo migration có tên rõ nghĩa.
3. Review cả phương thức `Up` và `Down`.
4. Commit migration cùng feature liên quan.
5. Thành viên khác pull code và chạy:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project Infrastructure `
  --startup-project WebAPI
```

6. Không tự sửa schema bằng MySQL Workbench.
7. Không chia sẻ database local; chỉ chia sẻ migration và seed data không nhạy cảm.
8. Dùng lệnh `migrations list` để xem danh sách migration hiện tại, tránh ghi cố định danh sách trong README vì có thể nhanh chóng lỗi thời.
````

## 10. Backend quản lý giáo viên (Manage Teacher)

Phạm vi đợt này: hồ sơ và tài khoản giáo viên do admin vận hành quản lý thủ công sau khi
đối chiếu danh sách ngoài hệ thống. Chưa có upload/import Excel, nghiệp vụ nhân sự hoặc
phân công giảng dạy theo năm học. Chi tiết giáo viên trả lớp chủ nhiệm hiện có;
không tự tạo lịch sử phân công từ dữ liệu không tồn tại.

### Các phase đã triển khai

1. Đối chiếu backlog `Sheet1!A48:G51` và mô hình `Teacher`/`User`, quyền, trường/phân hiệu hiện có.
2. Bổ sung DTO, validation, mapping, service, repository, controller và migration trong các thư mục sẵn có.
3. Quản lý hồ sơ/tài khoản, ngừng hoạt động, khóa/mở, đặt lại mật khẩu và thu hồi token.
4. Kiểm thử HTTP với MySQL riêng ngoài repository và chạy lại bộ test hiện có.

### Cấu hình và migration

Áp dụng migration `AddTeacherManagement` trước khi chạy phiên bản API mới (đăng nhập/JWT
cũng dùng cột `users.security_version`). Migration giữ nguyên bản ghi cũ; các trường hồ sơ
mới có thể null, phiên bản ban đầu là 1. Không tự động áp dụng migration lúc khởi động API.

```powershell
dotnet tool restore
dotnet ef database update --project Infrastructure --startup-project WebAPI
```

EF design-time factory hiện đọc `ConnectionStrings__DefaultConnection` hoặc
`WebAPI/appsettings.Development.json`. Nếu dùng user secrets, có thể nạp connection string
vào biến môi trường của terminal mà không in giá trị ra màn hình:

```powershell
$localSecretsPath = Join-Path $env:APPDATA 'Microsoft/UserSecrets/telo-school-management-webapi-local/secrets.json'
$localSettings = Get-Content -LiteralPath $localSecretsPath -Raw | ConvertFrom-Json
$env:ConnectionStrings__DefaultConnection = $localSettings.'ConnectionStrings:DefaultConnection'
dotnet ef database update --project Infrastructure --startup-project WebAPI
Remove-Item Env:ConnectionStrings__DefaultConnection
```

`TeacherManagement:TeacherRoleCode` mặc định là `TEACHER`. Bảng `roles` phải có role tương ứng.
Nếu dữ liệu hiện dùng `GIAO_VIEN`, cấu hình giá trị này thành `GIAO_VIEN`. API không tự tạo
role hoặc nhận danh sách quyền tùy ý từ request; thiếu role trả `503 TEACHER_ROLE_MISSING`.
Các tài khoản giáo viên kiêm quyền quản trị/hiệu trưởng/hiệu phó không được sửa qua module này.
Cấu hình role giáo viên trùng với role admin/hiệu trưởng/hiệu phó cũng bị từ chối (503).

### Phạm vi và endpoint

Admin dùng role trong `SchoolDirectoryAuth:AdminRoleCodes` (mặc định `OperationalAdmin`).
Hiệu trưởng dùng `MatrixAuth:PrincipalRoleCodes`, xem các phân hiệu trong trường mình.
Hiệu phó dùng `MatrixAuth:PhtRoleCodes`, xem phân hiệu gắn với tài khoản.
Phạm vi đọc lấy từ tài khoản đang hoạt động trong database, không nhận schoolId tùy ý
từ phía hiệu trưởng/hiệu phó. Giáo viên, tổ trưởng và học sinh không có quyền xem danh bạ này.

Luồng admin: chọn trường → chọn phân hiệu → quản lý giáo viên.

| Method | URL | Chức năng |
| --- | --- | --- |
| GET | `/api/admin/teacher-schools` | Tìm kiếm/phân trang trường |
| GET | `/api/admin/schools/{schoolId}/teacher-branches` | Tìm kiếm/phân trang phân hiệu |
| GET | `/api/admin/schools/{schoolId}/branches/{branchId}/teachers` | Danh sách giáo viên trong phân hiệu |
| GET | `.../teachers/reference-data` | Môn học, tổ, phân hiệu, trạng thái dùng cho bộ lọc/form |
| GET | `.../teachers/{id}` | Hồ sơ chi tiết |
| POST | `.../teachers` | Tạo đồng thời hồ sơ và tài khoản |
| PUT | `.../teachers/{id}` | Thay thế thông tin hồ sơ |
| DELETE | `.../teachers/{id}?version={version}` | Ngừng hoạt động, giữ lịch sử |
| GET | `.../teachers/{id}/account` | Thông tin đăng nhập, chỉ admin |
| PATCH | `.../teachers/{id}/account` | Đổi username/email, khóa/mở/ngừng tài khoản |
| POST | `.../teachers/{id}/reset-password` | Admin đặt lại mật khẩu |
| GET | `/api/teachers` | Danh sách trong phạm vi hiệu trưởng/hiệu phó |
| GET | `/api/teachers/reference-data` | Bộ lọc trong phạm vi hiệu trưởng/hiệu phó |
| GET | `/api/teachers/{id}` | Chi tiết trong phạm vi hiệu trưởng/hiệu phó |

Các endpoint cần Bearer token từ `POST /api/auth/login`.
Selector trường/phân hiệu nhận `search`, `page`, `pageSize`.
Danh sách giáo viên nhận `search` (tên hoặc mã cán bộ), `department`, `mainSubjectId`,
`employmentStatus`, `accountStatus`, `gender`, `schoolBranchId`, `sortBy`, `sortDirection`,
`page`, `pageSize`. `schoolBranchId` chỉ thu hẹp phạm vi; không thể vượt scope trong URL/tài khoản.

`page` mặc định 1, `pageSize` mặc định 20, tối đa 100. `sortBy` gồm `fullName` (mặc định),
`staffCode`, `joinedOn`, `createdAt`; `sortDirection` là `asc` hoặc `desc`. Luôn có ID làm
khóa sắp xếp phụ. Response danh sách: `data.items`, `page`, `pageSize`, `totalCount`, `totalPages`.
Không truyền trạng thái nghĩa là xem tất cả trạng thái trong phạm vi cho phép.

### Request mẫu

Tạo giáo viên (`POST .../teachers`, trả 201 và Location):

```json
{
  "profile": {
    "staffCode": "GV-PCB-018",
    "fullName": "Nguyễn Thị Lan",
    "department": "Tổ 4-5",
    "mainSubjectId": 1,
    "specialization": "Giáo dục tiểu học",
    "position": "Giáo viên",
    "gender": false,
    "phone": "0901234567",
    "workEmail": "lan.work@example.test",
    "dateOfBirth": "1990-01-01",
    "joinedOn": "2015-08-01",
    "employmentStatus": "WORKING"
  },
  "username": "lan.nguyen",
  "email": "lan.login@example.test",
  "password": "ReplaceWithAStrong!Password123"
}
```

`gender`: true = nam, false = nữ, null = chưa xác định. Mã cán bộ duy nhất toàn hệ thống
(nên có tiền tố trường như ví dụ); cho phép chữ Latin, số, `_`, `-`. Username/email đăng nhập
duy nhất toàn hệ thống. Tổ chuyên môn là tên trong hồ sơ, chưa tạo hệ thống quản lý tổ riêng.
Môn dạy chính được gán mới phải thuộc danh mục môn đang hoạt động; hồ sơ cũ được giữ
nguyên môn đã ngừng dùng khi sửa các thông tin khác. `workEmail` là email công tác và
độc lập với `email` dùng cho tài khoản. Thông tin tùy chọn được xóa khi gửi null trong PUT.

Sửa hồ sơ: gửi toàn bộ `profile` như trên và `version` lấy từ `data.teacher.version`.
Đổi tài khoản/khóa/mở:

```json
{ "username": "lan.nguyen", "email": "lan.login@example.test", "status": "LOCKED", "version": 1 }
```

Đặt lại mật khẩu:

```json
{ "newPassword": "ReplaceWithANew!Password123", "version": 2 }
```

Sau mỗi lần sửa, lấy version mới từ response. API account trả version tại `data.version`.
Version cũ trả `409 CONCURRENCY_CONFLICT`; thiếu/0 trả 422. Không có endpoint đọc mật khẩu
hoặc password hash. Mật khẩu phải dài 12–128 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt.

Trạng thái công tác: `WORKING`, `ON_LEAVE`, `RESIGNED`, `INACTIVE`.
Trạng thái tài khoản: `ACTIVE`, `LOCKED`, `INACTIVE`.
Chuyển công tác sang `RESIGNED`/`INACTIVE` cũng vô hiệu hóa tài khoản. Khôi phục công tác
không tự mở lại tài khoản; admin phải mở rõ ràng bằng API account. Khi xóa logic,
không xóa User, Teacher hay liên kết lớp/lịch sử đã phát sinh.

Sửa hồ sơ thông thường không làm giáo viên bị đăng xuất. Thay đổi tài khoản, reset mật khẩu
hoặc vô hiệu hóa giáo viên tăng security version để thu hồi token đã cấp. JWT kiểm tra
trạng thái và version từ database trên mỗi request. Giáo viên không được đăng nhập/sử dụng
token khi trường hoặc phân hiệu đã ngừng hoạt động. Admin vẫn được sửa hồ sơ, khóa,
reset mật khẩu và ngừng tài khoản ở đơn vị này, nhưng không được tạo mới hoặc mở tài khoản.
Token cũ chưa có claim version được xem là version 1 và bị vô hiệu hóa sau thay đổi bảo mật đầu tiên.
Client phải đăng nhập lại khi nhận 401. Các thao tác ghi log action, actor, school, branch,
teacher ID; không log mật khẩu hoặc toàn bộ request body.

Mã phản hồi: 400 (JSON/binding), 401 (chưa đăng nhập/token bị thu hồi), 403 (sai quyền/phạm vi),
404 (không tìm thấy trong scope), 409 (trùng, version cũ, trạng thái xung đột), 422 (validation),
503 (role giáo viên chưa cấu hình). Lỗi nghiệp vụ trả `ApiResponse` cùng error code.

## 11. Năm học và học kỳ

Lịch năm học dùng chung toàn hệ thống. Migration `AcademicYearSystemScope` bỏ phạm vi
tỉnh/thành và chỉ giữ một năm `ACTIVE` (ưu tiên ngày bắt đầu mới nhất, sau đó ID lớn nhất).
Các năm từng `ACTIVE` khác và học kỳ của chúng chuyển sang `CLOSED`. Migration giữ bản ghi,
mã năm học và các liên kết cũ; không tự gộp/xóa các lịch trùng nhau từ dữ liệu tỉnh/thành.
Sao lưu database trước khi áp dụng migration này. Dùng lệnh cập nhật schema ở mục 5;
nếu connection string nằm trong user secrets, nạp biến môi trường theo mục 10.

Quy tắc:

- Tên `YYYY-YYYY`, hai năm liên tiếp, năm bắt đầu từ 2000 đến 2100. Ngày bắt đầu/kết thúc
  thuộc hai năm tương ứng, kết thúc sau bắt đầu, thời lượng tối thiểu 180 ngày.
- Lịch mới hoặc thay đổi tên/ngày không được trùng tên hay chồng lấn năm học khác.
  Lịch cũ trùng nhau sau migration vẫn được sửa học kỳ nếu giữ nguyên tên/ngày năm học.
- Đúng hai học kỳ với thứ tự 1, 2; tên bắt buộc, tối đa 100 ký tự, không trùng nhau.
  Khi cấu hình, phải đủ ngày, nằm trong năm học; học kỳ II bắt đầu sau ngày kết thúc học kỳ I.
- Luồng năm học: `DRAFT → ACTIVE → CLOSED`. Áp dụng yêu cầu đủ lịch hai học kỳ và không
  có năm khác đang áp dụng; học kỳ I được kích hoạt cùng năm học.
- Kết thúc học kỳ I trước học kỳ II; kết thúc I tự kích hoạt II. Kết thúc năm khóa toàn bộ
  học kỳ. Năm/học kỳ đã kết thúc không được sửa hay mở lại.

Danh sách/chi tiết API cần đăng nhập. `GET /api/academic-years/current` cho phép chưa
đăng nhập để trang đăng nhập hiển thị lịch thực tế; trả năm `ACTIVE` hoặc `data: null`
nếu chưa có năm áp dụng. Endpoint này không chọn theo năm trên đồng hồ máy tính.
Ghi API cần policy `OperationalAdmin`: role `OperationalAdmin`,
`ADMIN` hoặc permission `academic_calendar.manage`.

`POST /api/academic-years` nhận `name`, `startDate`, `endDate` và `terms` gồm hai phần tử
`{ order, name, startDate, endDate }`. Năm học và học kỳ được lưu trong cùng giao dịch.
Client cũ không gửi `terms` vẫn tạo bản nháp với hai học kỳ chưa có ngày; cần cấu hình đủ
trước khi áp dụng. `PATCH /api/academic-years/{id}` nhận cùng các trường và `version` từ
response chi tiết để cập nhật lịch đồng thời. `version` cũ trả 409; client nên luôn gửi
version dù API vẫn cho phép bỏ qua để tương thích client cũ. Học kỳ đã kết thúc phải
được giữ nguyên trong payload cập nhật.

Các endpoint chuyển trạng thái: `POST /api/academic-years/{id}/activate`,
`POST /api/academic-years/{id}/close`, `POST /api/academic-years/{id}/terms/{termId}/close`.
Lỗi validation trả 422 kèm chi tiết trường; JSON/ngày sai định dạng trả 400;
trùng lịch, dữ liệu cũ hoặc trạng thái không hợp lệ trả 409. Không có quyền ghi trả 403.

Chạy kiểm thử theo mục 6. Frontend có hướng dẫn chạy và kiểm tra validation trong README
của `TeLo-Frontend`.

### Đối chiếu backlog quản lý năm học/học kỳ

| Chức năng | Hiện trạng |
| --- | --- |
| Xem danh sách, tìm kiếm, lọc, xác định năm hiện tại | Đã có; phạm vi toàn hệ thống, chưa theo từng trường |
| Tạo năm học với mã, tên, ngày | Đã có; mã sinh từ tên, chưa nhập mã riêng |
| Sửa năm học chưa khóa, kiểm tra xung đột | Đã có; năm đã kết thúc chỉ được xem |
| Khóa/chốt năm học, giữ lịch sử | Đã có trong module lịch; chưa chặn đầy đủ nghiệp vụ ghi ở các module phụ thuộc |
| Tạo học kỳ với tên, mã, thứ tự, ngày | Tự tạo đúng hai học kỳ cùng năm học; chưa có mã học kỳ và thêm học kỳ độc lập |
| Sửa học kỳ chưa khóa | Đã có qua lưu cấu hình hai học kỳ cùng nhau |
| Đóng học kỳ, giữ lịch sử | Đã có; đóng I trước II, không xóa bản ghi |

Các điểm cần hoàn thiện nếu áp dụng toàn bộ backlog: phạm vi theo trường và phân bổ lịch
cũ cần thống nhất trước migration; nhập mã năm học/mã học kỳ và luồng tạo học kỳ độc lập
chưa được hỗ trợ. `SchoolDirectoryAdminRepository.ValidateClassReferencesAsync`,
`ExamService.ValidateReferencesAsync` và `MatrixReferenceRepository.EnsureSemesterAsync`
chưa kiểm tra đầy đủ trạng thái năm/học kỳ khi phát sinh nghiệp vụ. Không xem việc khóa
form cấu hình lịch là bằng chứng mọi module đã khóa theo lịch.
# Role and module management — implementation checklist

This extension keeps the existing DTO → service → EF context → controller pattern used by SchoolService. Frontend uses the existing feature folders, API client, controls and pagination. The academic-calendar plan is left intact. No automatic commits.

- [x] Phase 1: additive identity schema, bounded pagination, role/module validation and optimistic concurrency; preserve existing grants and module links.
- [x] Phase 2: role CRUD/status and scoped user assignment. Keep append-only audit records; invalidate affected sessions; prevent self-removal of administrator access. Lists and scope pickers use server pagination.
- [x] Phase 3: module CRUD/status and matching frontend flows using the existing blue/white design and Figma ADMIN MULTI-TENANT suggestions.
- [x] Phase 4: focused regression tests, build/lint, MySQL migration and API smoke checks. Browser verification remains unavailable in this environment; production deployment has not been performed.

Contracts: `/api/roles`, `/api/modules`, `/api/users` and `/api/identity/scopes`. Codes are immutable and globally unique. A role applies system-wide (no school), to a school, or to a branch of that school. Existing built-in roles retain their scope. A used role is deactivated rather than deleted; historical changes are retained. Module status manages the module catalog, and does not automatically rewrite business API policies or existing permission masks. Assigning a custom role does not invent new API privileges.

### Chạy và kiểm tra chức năng phân quyền

Migration `20260930132745_AddIdentityManagement` đã áp dụng vào `sep` local. Đã sao lưu
tại `../sep-backup-before-identity-20260930-203837.sql`; không đưa bản sao dữ liệu này lên Git.
Sau migration vẫn có 4 vai trò, 4 tài khoản, 4 gán vai trò và 10 năm học; bổ sung 4 bản ghi
lịch sử gán có sẵn. Các module cũ nhận mã `MODULE_<id>` trước khi tạo unique index.
Migration chỉ bổ sung phần identity; không xóa cột trường/năm học cũ. Rollback schema sẽ
xóa phần lịch sử mới, nên phải sao lưu trước khi dùng lệnh downgrade trên database có dữ liệu.

Khởi động lại BE để nạp API và quy tắc quyền mới (dừng tiến trình BE cũ trong IDE trước):

```powershell
# Từ TeLo-Backend; dùng connection string và JWT key local đã cấu hình.
dotnet run --project WebAPI --launch-profile https
# Terminal khác, từ TeLo-FrontEnd/TeLo-Frontend
npm run dev
```

FE mặc định gọi `http://localhost:5035/api`; profile `https` của BE lắng nghe cả cổng
5035 và 7033. Đăng nhập lại bằng tài khoản `ADMIN`/`OperationalAdmin`, mở mục Phân quyền
→ Vai trò, Người dùng, Quản lý module trong sidebar. Danh sách, hộp chọn trường/phân hiệu
và hộp chọn thành viên đều phân trang phía server; `pageSize` từ 1 đến 100.

API ghi yêu cầu `version` hiện tại. Sai phiên bản trả 409 `STALE_VERSION`; xung đột mã,
tên module, phạm vi hoặc dữ liệu đang sử dụng trả 409 `CONFLICT`; lỗi trường trả 422.
Người dùng có tối đa 100 vai trò. Gán hàng loạt là một giao dịch: một người không hợp lệ
sẽ không làm phát sinh gán một phần. Không được tự thu hồi vai trò quản trị; trạng thái
vai trò quản trị được bảo vệ. Thay đổi quyền thu hồi token cũ của người bị ảnh hưởng.

Tài khoản mẫu chỉ được seed ở môi trường `Development`. Production không tự tạo tài khoản
mẫu hoặc schema; cần chạy migration và cấp tài khoản quản trị bằng quy trình triển khai riêng.

Kiểm thử đã chạy: 136 kiểm thử BE, gồm quy trình HTTP/JWT/MySQL thực và nâng cấp schema
có dữ liệu cũ; 22 self-check của FE; TypeScript, lint phần mới và build production.
Kiểm thử MySQL nhận connection string từ biến môi trường `TELO_TEST_MYSQL`, tạo database
ngẫu nhiên `telo_identity_test_*` riêng rồi dọn bỏ; không ghi vào database chỉ định trong
connection string. Nếu thiếu biến này, chỉ kiểm thử tích hợp MySQL được đánh dấu bỏ qua.

```powershell
# Đặt TELO_TEST_MYSQL bằng connection string local có quyền tạo database kiểm thử.
dotnet test TeLoSchoolManagement.sln -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
```

Chưa xác minh giao diện bằng trình duyệt do công cụ trình duyệt không khả dụng. Cần kiểm tra
trực quan/keyboard trên các màn mới trước khi triển khai; FE vẫn có cảnh báo bundle trên 500 KB.
Các mục phân quyền chi tiết theo chức năng và quản lý navbar chưa thuộc nhóm chức năng này.

### Review cuối role/module — 01/10/2026

| Phase | Kết quả |
| --- | --- |
| 1. Cấu trúc và contract | Giữ các lớp DTO/service/controller/configuration và feature FE hiện tại; service dùng DbContext theo mẫu SchoolService. Không thêm tầng repository trung gian, thư mục hoặc dependency trong đợt review này. |
| 2. Nghiệp vụ và bảo mật BE | Kiểm tra phân trang, validation, gán nhiều vai trò đúng phạm vi, giữ lịch sử, xung đột phiên bản và thu hồi phiên. Sửa lỗi mã cấu hình khác hoa/thường, mã cũ có khoảng trắng và sửa metadata khi đơn vị ngừng hoạt động. |
| 3. FE và tối ưu | Sửa trang rỗng khi tổng kết quả giảm; dùng lại bộ xử lý lỗi API có sẵn cho 400/422/409; định dạng code tại chỗ. BE dùng truy vấn kiểm tra tồn tại khi xóa và cập nhật phiên hàng loạt trong cùng giao dịch, tránh tải toàn bộ người dùng/liên kết. |
| 4. Hồi quy | 102 Application + 19 Infrastructure + 15 WebAPI tests đạt, không bỏ qua; gồm HTTP/JWT/MySQL thật trên database tạm riêng. FE có 22 self-check đạt, lint phạm vi sửa và production build đạt. |

Các mã hiệu trưởng do `MatrixAuth:PrincipalRoleCodes` xác định và mã quản trị danh bạ
do `SchoolDirectoryAuth:AdminRoleCodes` xác định chỉ được tạo với phạm vi toàn hệ thống.
Các API nghiệp vụ cũ diễn giải những mã này theo quyền rộng; gắn nhãn phạm vi trường/
phân hiệu sẽ không phản ánh đúng quyền thực tế. Vai trò mới thuộc nhóm này được bảo vệ
phạm vi bằng `IsSystem`. Muốn hiệu trưởng hoạt động riêng từng trường cần sửa cơ chế
phân quyền của các API ma trận/giáo viên trước; chưa coi đó là chức năng đã hỗ trợ.
Vai trò tùy chỉnh thông thường vẫn hỗ trợ phạm vi trường/phân hiệu.

Kiểm thử đã tái hiện lỗi tạo vai trò hiệu trưởng có phạm vi hẹp trước khi sửa, rồi đạt
sau khi sửa; bổ sung kiểm tra thu hồi token sau đổi phạm vi/trạng thái, giữ metadata
khi trường ngừng hoạt động và mã vai trò cũ có khoảng trắng. Đợt review này không cần
migration mới và không thay đổi dữ liệu `sep`. Chưa commit. Giới hạn kiểm tra trình
duyệt và cảnh báo bundle nêu trên vẫn còn; chưa tuyên bố sẵn sàng triển khai production.
