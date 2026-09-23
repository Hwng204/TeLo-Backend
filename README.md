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
