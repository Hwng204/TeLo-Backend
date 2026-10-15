# Phucvm DB v2 — bằng chứng kiểm tra

Ngày 15/10/2026. Phạm vi và phần chưa nghiệm thu: [implementation](implementation.md).

## Build/test không ghi DB

Các lệnh đã thực hiện trong worktree BE:

```powershell
dotnet build TeLoSchoolManagement.sln --nologo -v quiet --no-restore
dotnet test Tests/Application.Tests/Application.Tests.csproj --nologo -v quiet --no-restore
dotnet test Tests/Infrastructure.Tests/Infrastructure.Tests.csproj --nologo -v quiet --no-restore
dotnet test Tests/WebAPI.Tests/WebAPI.Tests.csproj --filter 'FullyQualifiedName~QuestionV2ContractTests|FullyQualifiedName~LockedFeaturesControllerTests|FullyQualifiedName~AcademicYearsControllerTests|FullyQualifiedName~TasksControllerTests|FullyQualifiedName~MatrixImportsControllerTests' --nologo -v quiet --no-restore
```

Kết quả: build 0 warnings/errors; Application 263, Infrastructure 8, HTTP contract 90 test đạt, không skip/fail trong các suite/filter đã chạy. Các filter HTTP chỉ kiểm tra auth/model binding/controller contract bằng factory được kiểm soát; không chứng minh persistence transaction đã chạy trên TiDB.

Có RED/GREEN cho thay đổi năm-only và xử lý payload journal sai định dạng. Test checksum từng phát hiện exception chưa được đổi sang domain conflict; đã sửa và test lại đạt, không bỏ assertion.

Các lệnh đã thực hiện trong worktree FE:

```powershell
npm run build
node --experimental-strip-types src/utils/adminAcademicYear.selfcheck.ts
node --experimental-strip-types src/utils/academicYear.selfcheck.ts
node --experimental-strip-types src/utils/academicYearV2.selfcheck.ts
node --experimental-strip-types src/utils/identity.selfcheck.ts
```

Build đạt. Self-check lần lượt 16, 21, 9 case và identity self-check đạt. ESLint tất cả TS/TSX đã sửa hoặc tạo mới trong diff đạt; không thêm eslint-disable hoặc tăng ngưỡng bundle. Vite vẫn báo bundle >500 kB như trước.

## Bảo toàn và phạm vi

- Hash SHA-256 687 file nguồn trong hai working tree cũ trùng baseline trước triển khai: **0 file thay đổi**.
- BE `phucvm` HEAD `ac5d3f221c2fead534597dd14bfaa4ffd4017013`; FE `phucvm` HEAD `75191eb47a9e2eade479f63770eff5a22790fa3e`.
- `git rev-list --count origin/main..HEAD`: 0 cho cả hai repo. Không có commit mới, không push.
- `git diff --check`: đạt cho cả hai repo. Git có thông báo chuyển LF/CRLF ở hai file CSS/TSX cũ, không phải lỗi whitespace.
- Không có diff ở `Infrastructure/Migrations`, `Infrastructure/Configurations`, `Infrastructure/Context` hoặc appsettings.
- Kiểm tra model giữ 52 bảng mapped, không có property mapped Version/QuestionVersion mới, đúng một migration InitialCreate từ main.
- Quét các file code/tài liệu đã sửa hoặc tạo mới không thấy host/user/password marker đã được cấp; không ghi credentials vào repository.
- Link local trong các tài liệu mới hợp lệ. Tài liệu chính để trong `tasks` vì `/docs` bị main gitignore; không sửa quy tắc ignore của nhóm.
- Các chuỗi UI đã đổi không có ký tự thay thế Unicode hoặc dấu hiệu mất dấu do encoding trong lượt quét.

## Giới hạn giai đoạn trước khi được phép thêm dữ liệu

Không migration, `database update`, `EnsureCreated`, seed, thêm tài khoản/dữ liệu mẫu, gọi API ghi hoặc bật worker vào DB online. Metadata TiDB chỉ đọc. Không tạo file seed. Không commit/push/PR.

Không chạy integration test tự tạo/xóa DB hoặc bộ test cũ dùng schema cũ. Không bật `QuestionPractice:Enabled`; worker vẫn opt-in và mặc định tắt.

Browser tool báo không có browser khả dụng; chưa mở/xác nhận UI trực quan hay in đề. Persistence, race/deadlock với thành viên khác, hiệu năng thực tế và UI responsive/popup cần nghiệm thu sau theo checklist. Teacher-year import history và Email hoãn đúng quyết định của người dùng.

## Bổ sung: runtime TiDB sau khi người dùng cho phép ghi dữ liệu thử

Các giới hạn không ghi dữ liệu ở mục trước mô tả giai đoạn kiểm chứng ban đầu. Sau đó người dùng đã yêu cầu thêm dữ liệu thử online, đủ thông tin và trạng thái cho phần phucvm. Đã thực hiện trên `sep_490`; không tạo file seed, không migration/schema update, không commit/push.

- [Hướng dẫn và kịch bản](online-test-data-20261015/README.md), [ledger exact ID và kết quả](online-test-data-20261015/manifest.json).
- 44 user, 26 năm, 36 role, 35 module; 1.596 câu hỏi/6.384 answer; 50 bộ đề/868 liên kết câu; 85 task/384 dòng history. Có dependency trường/phân hiệu/curriculum/ma trận và 5 kỳ thi giả lập cho guard sử dụng đề.
- Workflow Question, Exam Package và task biên soạn thực sự ghi/đọc journal, snapshot, revision và native rows trên TiDB. API trả đúng 29 mục kiểm tra runtime được lưu trong manifest; list/filter/pagination có dữ liệu nhiều trạng thái.
- Kiểm tra đúng scope/bài, không trùng câu trong một đề, đáp án đúng cho câu đã gửi/duyệt, một năm ACTIVE, bài nộp 18 câu/9 điểm và mã dự phòng chưa có bài làm đều đạt. Câu nháp cố ý thiếu đáp án là trường hợp test, không nằm trong ngân hàng đã duyệt.
- Sửa 3 raw SQL lock trong phần phucvm từ `school_branch.school_branch_id` sang bảng/cột native `branch.branch_id`. Sau sửa: build solution 0 warning/error; Application.Tests 263 passed. Không đổi migration/configuration/schema và không sửa nghiệp vụ của thành viên khác.
- Worker chuyển ôn tập vẫn tắt. Dữ liệu mã đã dùng/dự phòng đã có nhưng chưa chạy worker tự động. Không gửi email. Không chạy integration suite tự tạo/xóa database.

Chưa kiểm chứng trực quan UI/popup/toast/preview/in trên browser thật; chưa kiểm thử concurrency/race với module của thành viên khác hoặc hiệu năng lớn. Không coi runtime API đạt là toàn bộ checklist nghiệm thu đã hoàn thành. Dataset được giữ lại cho người dùng thử; chưa thực hiện xóa.

## Hiệu chỉnh sau phản hồi về luồng nhiệm vụ đề

Xem [đặc tả và kết quả hiệu chỉnh](PHUCVM_WORKFLOW_CORRECTION.md). Chỉ PHT sinh đề rồi giao giáo viên/tổ trưởng review, API tạo task đề trống bị chặn 409, route cũ chuyển đến màn sinh đề. Module được làm rõ là phân hệ ứng dụng; màn chọn module/login của người khác giữ nguyên. Bổ sung toast còn thiếu trong các thao tác Question/Task/Exam Package theo cùng helper đăng nhập.

Regression RED: 3/5 case HTTP mới thất bại trước sửa (TEAM_LEAD, TO_TRUONG sinh đề và PHT tạo task trống); thêm 3 case policy thuần tái hiện quyền quản lý cũ của tổ trưởng. GREEN: Application.Tests 266 và filter HTTP an toàn 95 test đạt, không skip/fail; solution build 0 warning/error. FE build và ESLint 14 file của lượt sửa đạt. Các suite/filter dùng kết nối test riêng, không tạo/xóa hoặc ghi DB online.

Runtime đọc trên dataset có sẵn: PHT options có 25 reviewer gồm tổ trưởng và giáo viên; canCreate chỉ true cho PHT. POST bị từ chối của tổ trưởng/giáo viên trả 403, route task cũ của PHT trả 409; danh sách paper của 3 task cũ còn nguyên. Không thực hiện mutation thành công vào TiDB ở lượt hiệu chỉnh. Công cụ browser không có phiên nên chưa xác minh trực quan. Không migration, thay schema, seed, commit/push.
