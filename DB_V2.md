# DB v2 — chuyển sang database theo ERD (EF code-first)

## 1. Trạng thái

| Mục | Hiện tại |
|---|---|
| Nhánh | `feat/db-v2` (tách từ `vanh`) |
| Nguồn schema | EF migrations như trước. Migration cũ đã xoá hết, chỉ còn `Infrastructure/Migrations/20261015020254_InitialCreate.cs` (52 bảng, không seed) |
| Database | Đủ 51 bảng của ERD (Figma, section `167:6584`), đúng cột, khoá, quan hệ + bảng `user_roles` |
| Ngoại lệ so với ERD (đã chốt) | Giữ bảng `user_roles` (một người nhiều vai trò) thay cho `user.role_id`, và cột `user.security_version` (thu hồi token khi đổi mật khẩu/khoá tài khoản). Bỏ mọi cột `version` |
| Chức năng khoá tạm | Endpoint kỳ thi, quản lý email, import học sinh, danh mục tỉnh và Question: trả **501** (mục 4). Gửi thông báo và ghi audit bị vô hiệu hoá nội bộ. |
| Frontend | Chưa đổi |

## 2. Mỗi người chuyển sang DB mới trên máy mình

Chạy từ thư mục gốc repo (`LeTo-Backend`), PowerShell hoặc Git Bash. `-p` sẽ hỏi mật khẩu MySQL.

1. Lấy code mới:

```
git fetch origin
git switch feat/db-v2
git pull --ff-only origin feat/db-v2
```

2. Cài công cụ `dotnet-ef` của repo:

```
dotnet tool restore
```

3. Tạo database trống (hoặc chạy câu `CREATE DATABASE` này trong MySQL Workbench):

```
mysql -h127.0.0.1 -uroot -p -e "CREATE DATABASE leto_v2 CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;"
```

4. Trỏ app sang DB mới: trong `WebAPI/appsettings.Development.json` của máy mình (đã gitignore), đổi `Database=...`
   thành `Database=leto_v2`. Không commit mật khẩu.

5. Tạo bảng bằng migration:

```
dotnet tool run dotnet-ef database update --project Infrastructure --startup-project WebAPI
```

Kết quả đúng: in ra `Applying migration '20261015020254_InitialCreate'` rồi `Done.`; trong Workbench, schema
`leto_v2` có 53 bảng (52 bảng + `__efmigrationshistory`).

Lưu ý:

- **DB cũ không dùng tiếp được** với code mới; giữ nguyên để tham khảo, không cần xoá.
- **Chưa có seed data**: chưa có tài khoản, khối, môn. Muốn đăng nhập phải có seed hoặc tự chèn một `user` (mật
  khẩu hash theo `IPasswordHasher<User>`) + dòng `user_roles`.
- Nhánh nào đang có **migration riêng chưa đẩy**: tạo lại migration đó trên nền nhánh DB v2 bằng
  `dotnet ef migrations add <Tên>` trên nền `InitialCreate` mới.
- Làm lại từ đầu: `DROP DATABASE leto_v2;` rồi bước 3 và 5.

## 3. Thay đổi hành vi & API

Nguyên tắc: request gửi trường đã bỏ vẫn được nhận và **bỏ qua** (không 4xx); response bỏ trường không còn chỗ lưu.

| Endpoint | Request: nhận rồi bỏ qua | Response: bỏ | Response: suy ra |
|---|---|---|---|
| `/api/schools*`, `/api/branches/*` | — | — | logo > 500 ký tự → 400 `VALIDATION_ERROR`; `province` là chuỗi tự do |
| `/api/academic-years*` | startDate, endDate, version, terms[].startDate/endDate | startDate, endDate, version, semesters[].startDate/endDate/status/version | code ← name; semesters[].order ← semester.code |
| `POST /api/academic-years/{y}/terms/{t}/close` | — | — | luôn 409 `TERM_STATUS_REMOVED` |
| `/api/users*` | email | — | email ← teacher.email hoặc ""; version ← security_version; tài khoản học sinh bị ẩn khỏi danh sách |
| `/api/roles*` | description, schoolId, schoolBranchId, version | description, schoolId/Name, schoolBranchId/Name, version | isSystem ← danh sách mã cố định + mã trong catalog ma trận |
| `/api/modules*` | version, code | version | code ← "" (ERD không có `module.code`) |
| `/api/exams*`, `/api/schools/{id}/emails*`, `/api/provinces*`, `/api/student-imports*`, `/api/admin/schools/{id}/student-imports*` | — | — | **501** `FEATURE_LOCKED_DB_V2` (mục 4) |
| `/api/auth/forgot-password`, `verify-otp`, `reset-password`, `profile` | — | — | tìm user theo teacher.email; profile email ← teacher.email |
| `/api/teachers*`, admin teachers | department, mainSubjectId, position, joinedOn, version | department, mainSubject*, joinedOn, position, version, homeroomClass* | staffCode ← user.moet_identifier; employmentStatus ← user.status |
| Danh sách lớp/học sinh | — | — | Giữ bộ lọc academicYearId từ main; lịch sử chuyển lớp lấy theo các kỳ lớp trong student_enrollment. Mỗi năm hiển thị kỳ lớp gần nhất. |
| Danh sách giáo viên | academicYearId không lọc lịch sử ngày vào làm | — | Vẫn kiểm tra năm học có tồn tại; trả hồ sơ hiện tại vì DB v2 không có joinedOn và ngày năm học. |
| `/api/classes*`, admin classes | code, homeroomTeacherId | homeroomTeacher* (null) | code ← name |
| `/api/students*`, admin students | — | — | code/fullName/status ← user; tạo học sinh = tạo kèm `user` |
| `/api/curriculum*` | code của chapter/lesson | — | fields[] ← subjects; fieldId ← subject_id; code ← ""; sortOrder ← vị trí theo id |
| `/api/matrices*`, import | semesterId, academicYearId | semesterId, createdAt, approvedAt (null) | academicContextId ← mã phạm vi (phân hiệu, môn, khối); lý do từ chối ← `task_history` |
| `/api/matrix-tasks*`, `/api/tasks*` | semesterId, academicYearId | năm học, học kỳ | phạm vi ← ma trận của nhiệm vụ; đề của gói ← `exam_set` (thời lượng mặc định 40 phút); không gửi email (khoá tạm) |

**Frontend chưa đổi**: màn dùng trường đã bỏ sẽ hiện rỗng (mã và thứ tự chương/bài, năm học/học kỳ trên ma trận và
nhiệm vụ, bộ môn/chức vụ/chủ nhiệm của giáo viên, mã lớp); màn của chức năng khoá tạm báo lỗi.

### Rủi ro cần biết

| # | Rủi ro | Thực tế |
|---|---|---|
| R1 | Không còn `user.email` | Email lấy từ `teacher.email`; admin, PHT… không có dòng teacher thì không nhận mail, không dùng được quên mật khẩu |
| R2 | Năm học không có ngày, học kỳ không có trạng thái | Không kiểm tra trùng thời gian; đóng học kỳ → 409 |
| R3 | Mỗi học sinh có một `user` (mật khẩu ngẫu nhiên, không vai trò) | Không đăng nhập được; ẩn khỏi danh sách user |
| R4 | Bỏ giáo viên chủ nhiệm và mã lớp | Phạm vi chủ nhiệm lấy từ `teacher_assignment`, chưa có chức năng ghi → giáo viên chủ nhiệm chưa thấy lớp |
| R5 | Vai trò toàn cục | Không theo trường/phân hiệu, không có mô tả |
| R6 | Ma trận/nhiệm vụ mất năm học, học kỳ; chương/bài mất mã và thứ tự tay | Import chương bài khớp theo tiêu đề |
| R7 | `matrix_detail.allocated_score` | Đang lưu phần trăm như cũ, không phải điểm |
| R8 | Không seed; không còn nhật ký audit | Picker khối/môn rỗng tới khi có dữ liệu; thao tác user/role/module không được ghi lại |

## 4. Chức năng khoá tạm và việc của người phụ trách

DB đã đúng ERD; phần **code** dưới đây chưa viết lại trên bảng ERD nên tạm khoá. Code cũ **không bị xoá**: file nằm
nguyên chỗ cũ nhưng bị loại khỏi biên dịch bằng `<Compile Remove>` trong các `.csproj` (khối có chú thích
"Khoá tạm DB v2"). Route cũ trả 501 qua `WebAPI/Controllers/LockedFeaturesController.cs`.

| Phần | Bị khoá | Bảng ERD dùng khi viết lại |
|---|---|---|
| Kỳ thi | Kỳ thi, môn thi, phòng thi, coi thi, kết quả (`/api/exams*`) | `exam`, `exam_subject`, `exam_session`, `room`, `room_student`, `invigilator`, `test_result`, `test_result_detail`, `incident`, `incident_type`, `violation`, `violation_type`. Entity đã có trong `Domain/Entities/Examination/V2`, config trong `Infrastructure/Configurations/Examination/V2` |
| Email / thông báo | Quản lý mẫu/sự kiện/cấu hình email; gửi thông báo khi giao/duyệt ma trận (đang là `LockedEmailNotificationService`, không gửi gì) | `notification_config`, `notification`, `notification_user`, `parameter` (entity trong `Domain/Entities/Notification/V2`) |
| Import học sinh | `/api/student-imports*`, `/api/admin/schools/{id}/student-imports*` | `student`, `user`, `student_enrollment` |
| Tỉnh / trường | Danh mục tỉnh, đồng bộ NSO (`/api/provinces*`) | `school.province` là chuỗi; ERD không có bảng tỉnh |
| Định danh | Nhật ký audit (hàm `Audit` trong UserService/RoleService không làm gì) | ERD không có bảng audit, cần thêm vào ERD nếu muốn giữ |
| Yêu cầu (`request_*`) | Chưa có chức năng | 6 bảng `request_*` đã có entity |
| Phân công giáo viên | Chưa có chức năng ghi | `teacher_assignment` |
| Question từ main | `/api/question-banks*`, `/api/tasks/{id}/question-package*`, `/api/tasks/{id}/questions*`, `/api/tasks/{id}/papers/{paperId}/questions*` trả 501; không chạy QuestionPracticeWorker | Giữ code qua Compile Remove. Chưa chuyển lịch sử gửi gói, version và lịch chuyển câu hỏi ôn tập sang DB v2. Không thêm bảng/cột ngoài 52 bảng. |

### Kết hợp thay đổi từ main

- Giữ kiến trúc tách `ClassService`, `StudentService`, `ClassRepository`, `StudentRepository`, `SchoolRepository` và `DirectoryScopeHelper`.
- Các lớp này dùng entity DB v2: thông tin học sinh nằm trên user; năm học lấy qua class; trạng thái nhập học suy ra từ ended_on; phạm vi giáo viên lấy từ teacher_assignment.
- Xoá hai migration `20261013162424_AddQuestionPackages` và `20261013170332_AddQuestionPaperLinks` cùng Designer vì chúng dùng schema cũ. Giữ nguyên `20261015020254_InitialCreate` và snapshot DB v2.
- Các test tích hợp Question chưa tương thích được giữ trong repo nhưng loại khỏi biên dịch cùng module; không tính chúng là test pass. Test thuần QuestionRules vẫn chạy.
- `Tools/QuestionDemo` cũng bị khoá: chỉ in thông báo và thoát mã 1, không chạy seed/migration. File seed cũ được giữ nhưng loại khỏi biên dịch.

**Mở khoá một phần:** viết lại repository/service trên bảng ERD (hoặc sửa code cũ cho khớp), xoá dòng tương ứng trong
khối `<Compile Remove>` của các `.csproj` và route tương ứng trong `LockedFeaturesController`, rồi build và chạy test.

## 5. Khi ERD đổi tiếp

1. Sửa entity (`Domain/Entities/**`) và cấu hình (`Infrastructure/Configurations/**`) theo ERD mới.
2. Tạo migration:

```
dotnet tool run dotnet-ef migrations add <TenMigration> --project Infrastructure --startup-project WebAPI
```

3. Áp vào DB của mình:

```
dotnet tool run dotnet-ef database update --project Infrastructure --startup-project WebAPI
```

Đừng sửa DB bằng tay hay bằng script SQL; mọi thay đổi đi qua migration. Cần cột đã bị bỏ thì báo nhóm thêm vào ERD
trước.

## 6. Dùng DB chung trên TiDB Cloud

DB chung của nhóm đã được tạo với tên `sep_490`; thành viên dùng cấu hình ở bước 2. Bước 1 dành cho người phụ trách khi khởi tạo môi trường mới.

Host/user/mật khẩu lấy từ người quản lý TiDB; **không** ghi vào file trong repo. Code đã sửa cho TiDB:
cột `title` của chương/bài dùng `utf8mb4_0900_ai_ci` (TiDB không có `as_ci`), transaction dùng
`ReadCommitted` cho user/role/module để kiểm tra quyền mới nhất sau khi chờ khoá, và `RepeatableRead` cho năm học (TiDB không hỗ trợ `Serializable`). TiDB mặc định không kiểm tra `CHECK` constraint.

Khi kích hoạt năm học, repository dùng đọc có khoá `FOR UPDATE` để các yêu cầu đồng thời vẫn chỉ giữ một năm học ACTIVE.

**Bước 1 — một người (phụ trách DB) làm một lần**, trong Workbench đã kết nối:

```sql
CREATE DATABASE IF NOT EXISTS sep_490 CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
```

Rồi tạo bảng từ thư mục `LeTo-Backend`:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project Infrastructure --startup-project WebAPI --connection "Server=<host>;Port=4000;Database=sep_490;User ID=<user>;Password=<mật khẩu>;SslMode=VerifyFull;"
```

Kiểm tra: `USE sep_490; SHOW TABLES;` → 53 bảng. Có migration mới thì chỉ người này chạy lại lệnh trên.

**Bước 2 — mỗi người**, cho backend trỏ vào TiDB (User Secrets, không nằm trong repo):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<host>;Port=4000;Database=sep_490;User ID=<user>;Password=<mật khẩu>;SslMode=VerifyFull;" --project WebAPI
dotnet run --project WebAPI
```

Quay về DB local: `dotnet user-secrets remove "ConnectionStrings:DefaultConnection" --project WebAPI`.

**Lưu ý:** cả nhóm dùng chung dữ liệu; không chạy `migrations add`/`database update` nếu không phụ trách;
không trỏ `TELO_TEST_MYSQL` vào TiDB (test tự tạo/xoá database); không tạo bảng trong schema `sys`.
