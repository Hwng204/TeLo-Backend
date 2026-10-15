# Phucvm — chuyển nghiệp vụ sang DB v2

Baseline BE main ac5d3f2, FE main 75191eb, ngày 15/10/2026. Làm trên nhánh phucvm có sẵn, không commit/push.

Kế hoạch dưới đây mô tả giai đoạn port ban đầu. Sau đó người dùng đã cho phép thêm dữ liệu thử: [dataset và kiểm chứng persistence](online-test-data-20261015/README.md). Luồng đề mới nhất theo [hiệu chỉnh](PHUCVM_WORKFLOW_CORRECTION.md): chỉ PHT sinh đề tự động rồi giao giáo viên/tổ trưởng review; không tạo task biên soạn đề trống. Không migration/thay schema; lượt hiệu chỉnh code không ghi thêm dữ liệu online.

## Hợp đồng và phạm vi

- Nguồn đọc: DB_V2.md, README, QUESTION_MANAGEMENT, TASKS, MATRIX, tài liệu phucvm-rebuild và source working tree cũ.
- Không migration, không đổi entity mapping/schema/snapshot migration, không seed, không ghi DB online. DB đích sep_490; sys không phải DB nghiệp vụ.
- Không chạy API ghi hoặc worker vào TiDB. Build/test thuần và kiểm tra SQL translation không kết nối DB.
- Academic Year: chỉ tên YYYY-YYYY và DRAFT/ACTIVE/CLOSED, bỏ học kỳ và lịch ngày khỏi luồng quản lý; giữ UI/layout hiện tại với các phần đã bỏ được loại ra.
- Role: toàn hệ thống; bảo vệ Admin/Teacher/Tổ trưởng/Hiệu trưởng/Học sinh/PHT theo mã hệ thống và alias đang dùng. Không tự thêm bản ghi vai trò vào DB.
- Module: giữ CRUD/trạng thái/delete gate và UI; không giả định tồn tại cột code/version đã bị bỏ.
- Question/exam package: giữ luồng, validation, quyền, snapshot và lịch sử cũ. Người dùng đã đồng ý JSON có phiên bản định dạng trong task_history.content, giới hạn nghiệp vụ QUESTION/EXAM và task neo EXAM_PACKAGE; không thay lịch sử MATRIX.
- Email chưa xử lý. Không mở khóa kỳ thi/phòng thi/import học sinh/tỉnh của người khác.
- Lọc giáo viên: người dùng đã quyết định hoãn, chờ nhóm bổ sung nguồn lịch sử import. Không dùng User.CreatedAt hoặc thời gian cập nhật hồ sơ làm thay thế, không tạo task TEACHER_IMPORT.
- Người dùng đồng ý bỏ ô mã module và mô tả role, giữ bố cục còn lại. Không dùng cột khác để lưu hai trường đã bỏ.

## Capability map

| ID | Trách nhiệm | Phụ thuộc |
|---|---|---|
| academic-year | CRUD năm học và vòng đời, chọn năm | identity hiện có |
| roles-modules | Role toàn cục và module | identity hiện có |
| question | Soạn/import/gửi/duyệt/bank/picker | task, curriculum hiện có; journal |
| exam-package | Sinh đề/review/chốt/preview/copy | question, matrix hiện có; journal |
| teacher-year | Đọc danh sách theo đợt import/năm | contract import và dữ liệu lịch sử |

## Các phase và điều kiện hoàn tất

1. [x] Bảo toàn working tree cũ, mở đúng nhánh phucvm, fast-forward main, đọc DB_V2.md.
2. [x] Academic Year: không tạo/đòi 2 học kỳ; giữ validation tên/trạng thái, một ACTIVE; FE hỗ trợ catalog không ngày; test domain/service và UI build.
3. [x] Role/module: role global, bảo vệ 6 nhóm và alias, bỏ input không có cột; kiểm tra quyền/token và điều kiện xóa/trạng thái. Bổ sung role là POST chủ động, chưa gọi online.
4. [x] Journal: format schema 1, Brotli/chunk TEXT/checksum, revision dưới khóa; test malformed/unrelated/missing chunk/revision/snapshot bất biến.
5. [x] Question authoring/import: persistence native, quota/draft/submit/reject/resubmit/approve và immutable submissions. Build/unit/contract qua; chưa chạy luồng DB.
6. [x] Bank/picker: scope/filters/availability/4 đáp án/protection/release theo DB v2, options giữ mảng. Worker opt-in mặc định tắt, chưa chạy online.
7. [x] Exam package: code sinh đúng ô, review task riêng, thay đúng câu, PHT chốt/return, snapshot/preview/in/copy chống trùng và đồng bộ task cũ. Pure rules/policy/contract qua; chưa nghiệm thu persistence/browser.
8. Hoãn Teacher-year theo xác nhận người dùng, chờ nguồn lịch sử import của nhóm.
9. [x] Port UI cũ và tích hợp router/task tối thiểu; build/lint và đối chiếu source. Chưa xác nhận pixel parity bằng browser thật.
10. [x] Build BE/FE, tests an toàn, SQL translation, rà diff, giữ nguyên migrations/config/schema, không commit/push/ghi DB. Bằng chứng và checklist còn lại ở tài liệu triển khai.

Checkbox đánh dấu phần code và kiểm chứng an toàn đã hoàn tất, **không** biểu thị đã nghiệm thu end-to-end trên DB online. Chi tiết mapping, protocol, giới hạn và checklist: [triển khai DB v2](implementation.md).

## Kiểm chứng

dotnet test Tests/Application.Tests/Application.Tests.csproj (focused rồi full); dotnet test Tests/Infrastructure.Tests/Infrastructure.Tests.csproj; WebAPI tests chỉ khi đã xác minh không tạo/ghi DB. Không đặt TELO_TEST_MYSQL vào TiDB.

FE: npm run build; selfcheck liên quan; đối chiếu JSX/CSS gốc, popup/toast/required và preview/in. Hạn chế runtime do không được ghi DB phải ghi rõ, không tuyên bố đã nghiệm thu luồng online.
