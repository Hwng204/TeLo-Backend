# Phucvm — triển khai lại các tính năng trên DB v2

Ngày cập nhật: 15/10/2026. Đây là tài liệu triển khai **code hiện tại**, bổ sung cho đặc tả và bản chụp code cũ tại [phucvm-rebuild](../../docs/phucvm-rebuild/README.md). Đọc cùng [DB_V2.md](../DB_V2.md), [plan](../tasks/plan.md) và [todo](../tasks/todo.md).

Hiệu chỉnh mới nhất: [luồng PHT sinh đề trước, module hệ thống và toast](PHUCVM_WORKFLOW_CORRECTION.md). Yêu cầu này thay thế quyền tổ trưởng tạo/giao đề trong đặc tả cũ; tài liệu chụp source cũ chỉ dùng tham khảo giao diện/lịch sử.

## 1. Những quyết định đã được người dùng xác nhận

| Nội dung | Quyết định áp dụng |
|---|---|
| Nhánh làm việc | Nhánh `phucvm` có sẵn của cả BE và FE; không tạo nhánh mới |
| Nguồn main | BE `ac5d3f221c2fead534597dd14bfaa4ffd4017013`; FE `75191eb47a9e2eade479f63770eff5a22790fa3e` |
| DB nghiệp vụ | `sep_490`; tuyệt đối không tạo bảng trong `sys` |
| Schema | Giữ nguyên model mapping, cấu hình và migration từ main; không thêm 5 cột từng đề xuất trong bản review ERD |
| Ghi DB | Sau triển khai, người dùng đã cho phép thêm dữ liệu thử vào `sep_490`; đã thực hiện theo mục 15. Vẫn cấm migration, database update và thay schema |
| Git | Không commit, push hoặc tạo PR |
| Năm học | Quản lý tên năm `YYYY-YYYY` và trạng thái; bỏ học kỳ/ngày bắt đầu/ngày kết thúc khỏi màn hình quản lý |
| Vai trò | Dùng chung toàn hệ thống; 6 nhóm vai trò bắt buộc không được xóa |
| Hai trường không còn trong DB | Bỏ ô mã module và mô tả vai trò; giữ phần giao diện còn lại |
| Snapshot/lịch sử | Đồng ý lưu JSON có định dạng và phiên bản trong `task_history.content`, riêng nghiệp vụ câu hỏi/bộ đề |
| Người chốt đề | Giáo viên được giao gửi kết quả review; PHT chốt bản cuối |
| Lọc giáo viên theo năm | **Hoãn**, chờ nhóm bổ sung nguồn lịch sử import; không tạo task `TEACHER_IMPORT` |
| Email | Xử lý sau |

Thông tin đăng nhập TiDB không được lưu trong tài liệu, source, appsettings hoặc file seed. Không có file seed được tạo. Lượt đọc metadata trước đó thấy bảng `role` chưa có bản ghi; chưa thêm role, tài khoản hay dữ liệu mẫu.

## 2. Thư mục làm việc và bảo toàn code cũ

| Mục đích | Thư mục |
|---|---|
| BE mới, nhánh phucvm | `TeLo-Backend-phucvm` |
| FE mới, nhánh phucvm | `TeLo-Frontend-phucvm` |
| BE cũ giữ nguyên thay đổi chưa commit | `TeLo-Backend` |
| FE cũ giữ nguyên thay đổi chưa commit | `TeLo-FrontEnd/TeLo-Frontend` |
| Tài liệu/bản chụp chức năng cũ | `docs/phucvm-rebuild` tại thư mục workspace cha |

Hai thư mục mới là Git worktree của repository hiện có, không phải hai nhánh tự tạo. Nhánh `phucvm` được fast-forward đến main rồi mới chỉnh code. HEAD vẫn bằng các commit main nêu trên; code đang ở trạng thái chưa commit. Mở đúng thư mục có hậu tố `-phucvm` để xem bản DB v2. Không dùng `reset --hard`, `clean`, hoặc chép đè toàn bộ thư mục cũ.

## 3. Ranh giới với các phần của thành viên khác

Phạm vi chính: Academic Year, Role Management, Module Management, Question và Exam Package. Chỉ tích hợp tối thiểu vào DI, router/sidebar, DTO task và các nhánh EXAM của `TaskService`.

- Không sửa model/migration/schema dùng chung để phục vụ riêng tính năng này.
- Không triển khai lại ma trận, chương/bài, kỳ thi, phòng thi, phân hiệu, hồ sơ người dùng, lớp hoặc học sinh của thành viên khác.
- Các API kỳ thi/phòng thi/email/import học sinh/tỉnh đang bị khóa theo main vẫn giữ khóa.
- Không thay định dạng nhận xét task ma trận. Chuỗi nhận xét cũ trong `task_history.content` giữ nguyên.
- Component popup/toast/required dùng chung được khôi phục từ phần chuẩn hóa UI cũ của phucvm. Đây là thay đổi có ảnh hưởng trình bày toàn app nên cần kiểm tra trực quan các màn hình tiêu biểu trước khi merge.
- `UserRoleEditor` và `RoleMembers` chỉ đổi cách gọi nhãn phạm vi thành toàn hệ thống; không viết lại quản lý tài khoản.

## 4. Mapping chức năng sang DB v2

| Nghiệp vụ | Nơi lưu native | Phần không có cột riêng và cách xử lý |
|---|---|---|
| Năm học | `academic_year` | Version là token tính từ trạng thái hiện tại; không có ngày/học kỳ trong form |
| Vai trò và thành viên | `role`, `user_roles`, `user` | Không scope trường/phân hiệu, không mô tả role; cờ hệ thống suy ra từ mã |
| Module | `module`, liên kết navbar hiện có | Không mã module; dùng tên/mô tả/trạng thái/liên kết hiện có |
| Task biên soạn câu hỏi | `task`, `question_task`, `question_task_details` | Version và bản nộp trong journal |
| Câu hỏi/đáp án | `question`, `answer` qua `QuestionOption` | Bài học ở `question.lesson_id`; ngân hàng ở `question.bank_type` |
| Ngân hàng thi/ôn tập | `question.bank_type` | Thời điểm/lý do chuyển ôn tập ghi journal `bank:{questionId}` |
| Ma trận nguồn | `exam_matrix`, `matrix_detail` | Không thêm năm học cho ma trận; không thay chức năng ma trận |
| Bộ đề và câu trong đề | `exam_set`, `exam_set_question` | Cấu hình ô, snapshot, người tạo/chốt, năm, mục đích, lịch sử, liên kết bản sao ghi `exam:{setId}` |
| Người review | `exam_set.review_by_user_id`, task EXAM được giao | Không dùng người review làm người PHT chốt; người chốt nằm trong snapshot |
| Biến thể đề đã sử dụng | `exam_variant.question_order`, `test_result`, `exam_subject`, `exam` | Chỉ đọc cấu trúc hiện có của module kỳ thi; không sửa dữ liệu kỳ thi |
| Lịch sử import giáo viên | Chưa có nguồn được nhóm chốt | Hoãn; không suy diễn từ ngày tạo/cập nhật hồ sơ |

Không tạo `question_bank`, bảng submission/version/snapshot mới, hoặc `exam_task_papers`. Các FK native hiện có tiếp tục được dùng. Các API giữ shape mà FE cũ cần ở mức hợp lý; ví dụ `QuestionItem.options` luôn là mảng A/B/C/D dù DB dùng bảng đáp án.

## 5. Academic Year

### Giao diện và vòng đời

- Giữ trang danh sách, tạo mới, cấu hình, tìm kiếm, phân trang, trạng thái, nút và popup cũ.
- Form chỉ còn tên năm học bắt buộc, có dấu `*` đỏ. Không có form hai học kỳ hoặc các ô ngày.
- Tên `YYYY-YYYY`, năm sau bằng năm trước + 1, năm bắt đầu từ 2000 đến 2100, không trùng tên.
- Trạng thái tiếp tục là `DRAFT`, `ACTIVE`, `CLOSED`. Kích hoạt không đòi tồn tại học kỳ.
- Kích hoạt năm mới và đóng năm đang áp dụng trong cùng transaction/khóa theo repository hiện có; chỉ có một năm ACTIVE. Năm CLOSED không kích hoạt lại.
- Chọn năm làm việc vẫn ưu tiên lựa chọn còn hợp lệ, sau đó ACTIVE, sau đó năm mới nhất. Khi không còn `startDate`, sắp xếp theo tên năm.

### Tương thích

Không xóa entity/bảng semester hoặc endpoint cũ mà phần khác có thể dùng. Request tạo năm chỉ có tên không sinh ngầm hai học kỳ. Request legacy có `Terms` vẫn đi luồng tương thích hiện có, nhưng UI năm học không gọi luồng đó. Version của response tính từ ID/tên/trạng thái và được kiểm tra khi cập nhật tên; không thêm cột version.

File chính: `AcademicYearService.cs`, `AcademicYearRepository.cs`, `AcademicYear.cs`, `AcademicYearDto.cs`; FE `features/academicYears`, `utils/academicYear.ts`, `utils/adminAcademicYear.ts`.

## 6. Role Management và Module Management

### Vai trò toàn cục

Danh sách không gửi filter trường/phân hiệu. Form không còn chọn scope hoặc mô tả vai trò. Nhãn phạm vi hiển thị “Toàn hệ thống”. Gán role không kiểm tra trùng trường/phân hiệu; vẫn kiểm tra trạng thái tài khoản và role.

| Nhóm bắt buộc | Mã chuẩn khi bổ sung | Alias được nhận diện/bảo vệ |
|---|---|---|
| Admin | `ADMIN` | `ADMIN`, `OperationalAdmin` |
| Teacher | `TEACHER` | `TEACHER`, `GIAO_VIEN` |
| Tổ trưởng | `TEAM_LEAD` | `TEAM_LEAD`, `TO_TRUONG`, `SUBJECT_COORDINATOR` |
| Hiệu trưởng | `HIEU_TRUONG` | `HIEU_TRUONG`, `PRINCIPAL` |
| Học sinh | `STUDENT` | `STUDENT`, `HOC_SINH` |
| PHT | `PHT` | `PHT`, `PHO_HIEU_TRUONG`, `VICE_PRINCIPAL` |

Tên có thể hiển thị theo bản ghi hiện có; logic bảo vệ dùng mã, không dò tên tiếng Việt. Mã role không được đổi sau khi tạo, dài tối đa 50 ký tự theo DB v2. Các mã role cấu hình trong catalog cũng được bảo vệ. Việc bảo vệ alias không tự cấp quyền nghiệp vụ cho một mã chưa nằm trong cấu hình phân quyền của task/matrix; ưu tiên mã chuẩn khi tạo dữ liệu mới.

Không cho xóa role hệ thống kể cả chưa có người dùng; giữ chặn xóa role đã có người dùng. Admin không được ngừng áp dụng để tránh khóa quản trị. Các role khác vẫn có vòng đời trạng thái theo logic cũ. Thay role/trạng thái làm tăng `user.security_version` của người bị ảnh hưởng theo xử lý identity hiện có.

### Có đủ 6 nhóm role mà không seed tự động

- `GET /api/roles/required`: chỉ đọc, cho biết nhóm nào còn thiếu.
- `POST /api/roles/required/restore`: hành động Admin chủ động xác nhận trên UI, chỉ thêm nhóm còn thiếu, nhận diện alias để không tạo trùng nhóm.
- Không gọi POST từ startup, login, GET, hoặc test. Chưa gọi vào DB online.
- Nếu DB chưa có tài khoản Admin và grant hợp lệ, nhóm phải cấp dữ liệu khởi tạo bằng quy trình được phê duyệt trước; endpoint này không bỏ qua xác thực để bootstrap.

### Module

Giữ CRUD, mô tả, trạng thái, phân trang, thông tin số navbar liên quan và điều kiện xóa. Bỏ ô/nhãn mã module và tìm theo tên module. Không cất mã module vào cột mô tả/tên. Không đổi hợp đồng navbar của nhóm.

Module là **phân hệ ứng dụng**, ví dụ Thi; ma trận, ngân hàng câu hỏi, bộ đề và kỳ thi có thể là các chức năng bên trong phân hệ Thi. Không mặc định mỗi chức năng nhỏ là một module. Màn chọn module sau đăng nhập do người khác triển khai; phần phucvm chỉ quản lý danh mục module cho Admin. Không mở quyền API quản trị module cho người dùng thường hoặc can thiệp cơ chế login/landing của nhóm.

### Chống ghi đè

Role/module response trả token `uint` băm từ các giá trị native hiện tại và số liên kết liên quan. FE gửi lại token khi sửa/trạng thái/xóa/gán/bỏ người dùng; BE khóa row rồi so sánh, trả `STALE_VERSION` khi khác. Đây là token **trạng thái hiện tại**, không phải số revision tăng mãi trong DB; nếu dữ liệu đổi rồi trở về đúng giá trị cũ thì token có thể giống cũ. Không dùng token này làm lịch sử kiểm toán. Các caller legacy bỏ version vẫn được tương thích ở các endpoint trước đây cho phép.

File chính: `IdentityManagementRules`, `RevisionToken`, `RoleService`, `ModuleService`, `RequiredRolesService`, DTO và `RolesController`; FE `IdentityEditor`, `RoleListPage`, `ModuleListPage`, `requiredRolesApi`, helper identity.

## 7. Journal trong task_history.content

### Phạm vi và stream

| Stream | Nội dung | Row lock bảo vệ |
|---|---|---|
| `question` | Thay đổi bản nháp, SUBMITTED snapshot và quyết định REVIEWED | Task QUESTION |
| `bank:{questionId}` | Chuyển câu sang ôn tập và lý do | Phân hiệu rồi câu hỏi |
| `exam:{setId}` | Toàn bộ trạng thái/snapshot/lịch sử bộ đề sau mỗi thao tác | Phân hiệu → bộ đề → task |
| `paper-links` | Danh sách ID đề thuộc task biên soạn sau khi có task review riêng | Task biên soạn EXAM |

Task `EXAM_PACKAGE` nội bộ chỉ là chỗ neo journal khi tạo đề trực tiếp. Không hiển thị nó trong danh sách nhiệm vụ thông thường. Khi giao review trực tiếp, task này chuyển thành EXAM bình thường và người review thấy được nhiệm vụ.

### Định dạng envelope, schema 1

```json
{
  "format": "phucvm.task-journal",
  "schema": 1,
  "eventId": "<guid chung cho các mảnh>",
  "stream": "exam:123",
  "revision": 1,
  "action": "GENERATED",
  "index": 0,
  "count": 1,
  "base64": "<một mảnh dữ liệu đã nén>",
  "lookup": { "entityId": 123, "purpose": "exam", "academicYearId": 7 },
  "encoding": "br",
  "hash": "<SHA-256 của JSON trước khi nén>"
}
```

`lookup` có ở bộ đề để lọc mục đích/năm trong SQL trước khi phân trang; không đặt keyword chỉ mục vào payload nén. `create_by`, `create_at`, `task_id` dùng cột native. Một sự kiện logic có thể chiếm nhiều row, cùng eventId và revision. Không lấy số row task_history làm version.

- JSON UTF-8 → Brotli → chia mảnh 16.384 byte → Base64. Mỗi envelope thấp hơn giới hạn TEXT 65.535 byte.
- Giới hạn payload chưa nén 128 MB, sau nén 48 MB; vượt giới hạn báo `SNAPSHOT_TOO_LARGE` và rollback. Không khẳng định snapshot không có giới hạn.
- Decoder hỗ trợ encoding `json` cho tương thích, writer hiện tại ghi `br`. Kiểm tra schema, số/thứ tự mảnh, header, người ghi, checksum, revision liên tục.
- Dữ liệu thiếu/hỏng trả `JOURNAL_INVALID`/409; không tự bỏ qua rồi dựng một bản cuối không đầy đủ.
- Chỉ đọc envelope có prefix riêng. Nhận xét ma trận dạng text giữ nguyên.
- `ReadLatestAsync` chỉ giải nén payload cuối nhưng vẫn kiểm tra chuỗi header/revision; màn hình lịch sử câu hỏi dùng đọc đầy đủ để phục hồi từng bản nộp.
- Append và thay đổi native được commit trong cùng transaction. Không cập nhật snapshot cũ khi teacher sửa bản nháp mới.

### Payload câu hỏi

`SUBMITTED` chứa `QuestionSubmission`: số lần nộp, thời gian, danh sách `QuestionItem` gồm nội dung, A/B/C/D, đáp án đúng và giải thích. ID bản nộp là ID row mảnh đầu; không có bảng submission riêng.

`REVIEWED` chứa ID bản nộp, APPROVED/REJECTED, nhận xét chung và nhận xét từng câu. Khi đọc, service ghép quyết định vào đúng bản nộp; không sửa envelope SUBMITTED. SAVED là mốc version, không cần lưu lại toàn bộ nháp ở mỗi lần gõ/lưu.

### Payload bộ đề

`ExamPackageState` lưu người tạo/ngày tạo, năm, purpose/source, task biên soạn gốc, trạng thái đã giao review, người/ngày PHT chốt, ô ma trận, danh sách row và snapshot câu hỏi, lịch sử, cờ xóa và ID bản sao ôn tập. Row snapshot lưu cả `exam_set_question_id` và `matrix_detail_id` để đổi câu đúng ô, không suy đoán theo thứ tự hiển thị.

`History` có action/actor/thời gian/nhận xét/ID câu cũ-mới. Event mới chứa trạng thái sau hành động; lịch sử cũ không bị cập nhật. Trường status native của bộ đề vẫn là nguồn cho tích hợp kỳ thi.

Lưu ý vận hành: phương án journal tránh đổi schema nhưng lịch sử lớn dần; truy vấn stream/purpose/year phải đọc TEXT vì không có index cột mới. Danh sách vẫn lọc và phân trang trong SQL, chỉ dựng snapshot cho trang đang xem. Nếu quy mô lớn cần đo thực tế trước khi bàn tối ưu schema; không tự thêm index/cột trong lượt này.

## 8. Quản lý câu hỏi

### Soạn và nhập Excel

1. Tổ trưởng giao task QUESTION với các bài/mức nhận thức/quota theo module task hiện có.
2. Giáo viên được giao mở gói, thêm/sửa/xóa nháp trong đúng target. Người ngoài task không xem/sửa được.
3. Nháp giữ validation cũ và có thể chưa hoàn chỉnh: luôn có bốn ô A–D, ô chưa dùng được để trống, giới hạn độ dài và mã đáp án hợp lệ. Câu hoàn chỉnh để gửi cần 2–4 lựa chọn có nội dung, nội dung các lựa chọn khác nhau và một đáp án đúng trong ô đã điền, đủ quota từng target, không vượt quota. Quy tắc bộ đề/picker cũ chặt hơn: chỉ lấy câu có đủ 4 lựa chọn; vì vậy câu đã duyệt có 2–3 lựa chọn vẫn có thể nằm trong ngân hàng nhưng không được tính là ứng viên sinh đề tự động.
4. Import dùng template/parser hiện có, kiểm tra toàn bộ trước khi ghi, lỗi thì rollback toàn đợt; không import một phần rồi bỏ các dòng lỗi.
5. DB v2 không có mã chương/bài cũ: dropdown và template dùng tên chương/bài thực tế. Hidden target ID ưu tiên để tránh trùng tên; nhãn legacy trùng nhiều target bị báo lỗi thay vì tự chọn target đầu tiên. Không tạo mã giả CH-/L-.
6. Version là revision của stream `question`, bắt đầu 0 khi chưa có sự kiện; request mutation bắt buộc gửi version. FE tải lại khi conflict.

### Gửi, duyệt và yêu cầu sửa

| Hành động | Người thực hiện | Kết quả |
|---|---|---|
| Lưu nháp | Giáo viên được giao | Task ASSIGNED → IN_PROGRESS, tăng version |
| Gửi | Giáo viên được giao | Lưu snapshot; task PENDING_REVIEW, câu SUBMITTED, khóa sửa |
| Yêu cầu sửa | Tổ trưởng đã giao | Lưu quyết định/nhận xét; task NEEDS_REVISION, câu DRAFT |
| Sửa/gửi lại | Giáo viên được giao | Bản nộp mới, bản trước giữ nguyên |
| Duyệt | Tổ trưởng đã giao | Task COMPLETED, câu APPROVED, `bank_type=exam` |

Reviewer thấy snapshot đã nộp, không thấy nháp mới khi tác giả đang sửa. Request gửi/duyệt bị retry sau mất response có kiểm tra phiên bản/trạng thái để không sinh thêm bản nộp trùng. Cả trạng thái native và journal trong cùng transaction.

### Ngân hàng và picker

- Giữ danh sách, chi tiết, lọc exam/practice, từ khóa, môn/khối/chương/bài/mức nhận thức, phân trang và popup xem trước.
- Chỉ câu APPROVED vào ngân hàng. PHT/giáo viên/tổ trưởng giới hạn theo phân hiệu; Hiệu trưởng giữ phạm vi đọc theo trường từ quy tắc hiện tại.
- Picker ma trận lọc đúng phân hiệu, môn, khối, bài, mức nhận thức, loại câu, 4 đáp án A–D có nội dung và đúng một đáp án. Backend kiểm tra lại khi thêm/thay câu; FE filter không phải lớp bảo vệ duy nhất.
- Câu thuộc đề đang được kỳ thi SCHEDULED/ONGOING sử dụng không được chuyển ôn tập.
- PHT cùng phân hiệu có thể chuyển thủ công; kiểm tra lại tài khoản/quyền/phân hiệu sau khi chờ khóa.
- Reconciler chỉ chuyển câu thuộc biến thể có `test_result` ở kỳ thi COMPLETED, ID có trong JSON `question_order` và có thật trong exam_set_question. Đề dự phòng chưa có lượt sử dụng không được tự chuyển. Nếu câu đang được kỳ thi khác bảo vệ thì tiếp tục giữ trong ngân hàng thi.
- JSON question_order không nhận diện được thì bỏ qua, không đoán ID. Tối đa 200 câu chuyển mỗi lượt; đọc biến thể theo batch 100 và keyset, không tải toàn bộ lịch sử sử dụng vào một mảng.

Worker đã biên dịch, **mặc định không đăng ký/chạy**. Chỉ bật `QuestionPractice:Enabled=true` (hoặc biến môi trường tương ứng) sau khi được phép ghi DB; chu kỳ 30 giây. Không bật trong lượt triển khai này.

## 9. Quản lý bộ đề thi

### Giao diện phải giữ

Danh sách Kho đề có tìm tên, tab trạng thái, mục đích thi/ôn tập, năm/môn/khối, phân trang, thông tin reviewer và cập nhật. Trang tạo tự động chọn ma trận đã duyệt, xem đủ/thiếu câu theo ô. Trang chi tiết có thông tin đề, người tạo/review/chốt, hạn review, các ô ma trận/câu hỏi, lịch sử và nút theo quyền. Giữ trang preview/in có câu hỏi và đáp án theo tùy chọn giao diện cũ.

### Sinh đề

- Chỉ PHT có phân hiệu tạo đề mới từ ma trận APPROVED cùng phân hiệu. Tổ trưởng và giáo viên được nhận đề đã sinh để chỉnh sửa/review.
- Đường vào từ Nhiệm vụ hoặc Kho đề đều dẫn đến Tạo đề tự động; URL `/matrix-tasks/new/exam` chuyển sang `/exam-packages/new`. Không giao task đề trống rồi yêu cầu tổ trưởng tự chọn câu để tạo đề mới.
- Kiểm tra tổng tỷ lệ bằng 100%, số câu từng ô > 0, loại MULTIPLE_CHOICE, mức nhận thức hợp lệ, không trùng ô, tổng tối đa 1.000 câu.
- Lấy ngẫu nhiên đủ số câu, không trùng ID, đúng toàn bộ điều kiện từng ô. Thiếu câu ở bất kỳ ô nào thì trả lỗi chi tiết các ô thiếu và rollback toàn bộ.
- Lưu native exam_set và exam_set_question cùng snapshot ô/câu. Chèn các liên kết câu theo đợt, không SaveChanges từng câu.
- DB ma trận không còn academic_year_id: bộ đề mới gắn năm ACTIVE tại thời điểm tạo trong journal. Bộ đề cũ đã có journal giữ năm đã ghi, không đổi theo năm ACTIVE mới. Đề native chưa từng có journal không có lịch sử năm đáng tin; không tự backfill DB, trường hợp fallback được dựng từ dữ liệu hiện có và cần rà khi nghiệm thu dữ liệu ngoài module.

### Trạng thái và phân quyền

| Trước | Hành động | Người có quyền | Sau |
|---|---|---|---|
| DRAFT | Đổi tên | PHT | DRAFT |
| DRAFT | Giao review | PHT | IN_REVIEW; task ASSIGNED |
| IN_REVIEW / REVIEWED | Đổi reviewer/hạn | PHT | IN_REVIEW; giao lại task |
| IN_REVIEW | Thay câu | Giáo viên/tổ trưởng đúng người được giao | IN_REVIEW; task IN_PROGRESS |
| IN_REVIEW | Gửi kết quả | Đúng người được giao | REVIEWED; task PENDING_REVIEW |
| REVIEWED | Trả lại, bắt buộc lý do | PHT | IN_REVIEW; task NEEDS_REVISION |
| REVIEWED | Chốt cuối | PHT | APPROVED; task COMPLETED, lưu finalizer/snapshot |
| DRAFT chưa dùng | Xóa | PHT | Xóa/logical delete theo nguồn task |
| APPROVED, purpose exam | Tạo bản sao ôn tập | PHT | Tạo đề APPROVED purpose practice riêng |

Teacher chỉ thấy đề đã chốt hoặc đề được giao review trong phạm vi được phép; không đọc tất cả nháp. PHT và tổ trưởng có phạm vi đọc theo phân hiệu, Hiệu trưởng theo quy tắc đọc của trường. Mọi mutation kiểm tra scope/quyền trên server, khóa và kiểm tra lại version. Sau APPROVED không sửa tên/thay câu/giao lại reviewer/xóa đề.

Reviewer phải ACTIVE, cùng phân hiệu, có role giáo viên hoặc tổ trưởng và khác người tạo đề. Hạn review bắt buộc từ hôm nay. Khi thay câu, ID mới phải chưa có trong bộ đề và khớp đúng `matrix_detail_id` của vị trí đang thay. Submit/finalize đều kiểm tra lại cấu trúc và tính hợp lệ trong ngân hàng.

### Đồng bộ task tạo đề cũ và task review mới

Task EXAM cũ có thể giao tạo nhiều đề. ID exam_set placeholder là ID paper ổn định. Khi tổ trưởng chọn câu, lưu ngay vào đúng native set và journal của set đó.

Đây chỉ là đường tương thích cho nhiệm vụ đã tồn tại. `POST /api/tasks/exam` hiện trả 409 `EXAM_TASK_FLOW_CHANGED` cho PHT kèm hướng dẫn sinh đề tại Kho đề; không phát sinh task/set mới. Người ngoài quyền PHT bị chặn. Luồng mới là `POST /api/exam-packages` → xem đề → giao review; task review được sinh khi giao đề đã có câu.

Khi một đề được giao review, tạo task EXAM riêng cho đề đó, chuyển `exam_set.task_id` sang task review và sao chép chỉ stream `exam:{setId}` sang task mới. Task tạo đề gốc giữ danh sách ID trong stream `paper-links`; không mất đề khi task_id thay đổi. Scope/detail/status của task gốc dùng cả native task_id và registry.

- Task review không sửa bằng form task EXAM thông thường; cập nhật reviewer/hạn ở trang bộ đề.
- Task biên soạn tổng hợp COMPLETED khi mọi đề APPROVED; PENDING_REVIEW khi mọi đề ít nhất đang review; còn lại IN_PROGRESS.
- Xóa một đề nháp từ task biên soạn giữ placeholder/native ID, xóa liên kết câu và ghi cờ Deleted. Có thể soạn lại vị trí đó theo version hiện tại.
- Đề trực tiếp không có task biên soạn gốc thì xóa native set khi đủ điều kiện; task neo giữ journal.
- Đề native bên ngoài có task_id NULL vẫn đọc được khi cấu trúc hợp lệ nhưng mutation trả conflict có thông báo, không tự tạo task hay lỗi 500. Chức năng tạo đề của module này luôn tạo task neo.

### Chốt, snapshot và bản sao ôn tập

Chốt lưu người PHT chốt và thời gian riêng, không ghi đè người review. Nội dung dùng snapshot để in/xem lại. Bản sao ôn tập giữ câu/đáp án/ô từ bản đã chốt và thời lượng đề gốc, có native exam_set riêng. `SourceId` và `PracticeCopyId` trong journal liên kết hai đề và chặn tạo trùng dưới khóa nguồn. Không đổi purpose của đề thi gốc. Chặn công bố bản sao nếu đề gốc đang được kỳ thi chưa COMPLETED/CANCELLED sử dụng.

## 10. API và file đầu mối

| Chức năng | API |
|---|---|
| Năm học | Giữ `/api/academic-years` và endpoint trạng thái hiện có |
| Role/module | Giữ `/api/roles`, `/api/modules`; thêm `/api/roles/required` và POST `/required/restore` |
| Gói câu hỏi | Giữ `/api/tasks/{id}/question-package` và các action submit/approve/reject/import/template |
| Câu nháp | Giữ `/api/tasks/{id}/questions` |
| Ngân hàng | Giữ `/api/question-banks/...` |
| Picker trong task | Giữ `/api/tasks/{taskId}/papers/{paperId}/questions` |
| Danh sách/tạo đề | GET/POST `/api/exam-packages` |
| Chi tiết/đổi tên/xóa | GET/PUT/DELETE `/api/exam-packages/{id}` |
| Options | GET `/api/exam-packages/options?packageId=...` |
| Giao review | POST `/{id}/review-assignment` |
| Thay câu | PUT `/{id}/questions/{rowId}` |
| Gửi/trả lại/chốt/copy | POST `/{id}/submit-review`, `return-review`, `finalize`, `practice-copy` |

Mutation câu hỏi/bộ đề JSON bắt buộc có `version`; DELETE dùng query version bắt buộc. Không gửi version trả 400 trước persistence. Version lệch trả 409; FE hiển thị toast và tải lại, không retry mutation mù.

File chính BE: `QuestionService`, `QuestionBankService`, `QuestionImportService`, `QuestionPaperServiceV2` (lớp `QuestionPaperService`), `QuestionAccess`, `TaskJournal`, `TaskJournalCodec`, `ExamPackageService/Commands/Storage`, `ExamPackageRules/Workflow/Index`, controller/DTO tương ứng. File `QuestionPaperService.cs` legacy vẫn bị loại khỏi compile theo main; không có hai implementation cùng chạy.

File chính FE: `features/questions`, `features/examPackages`, `features/academicYears`, `features/identity`, task detail, routes/sidebar và component thông báo/form chung. Router bộ đề: `/exam-packages`, `/exam-packages/new`, `/exam-packages/:id`, `/exam-packages/:id/preview`.

## 11. Chuẩn giao diện, popup và thông báo

- Giữ bố cục, nhãn, bảng, nút, filter và thao tác trong bản code cũ phucvm; các khác biệt chủ động chỉ ở năm học, role global, mã module/mô tả role và nhãn chương/bài không có mã.
- FormDialog dùng native dialog, header/nút đóng, focus input và trả focus về nút mở; chặn đóng Escape khi đang lưu. CSS giới hạn chiều rộng/max-height, box-sizing và chiều rộng textarea để tránh tràn ngang như ảnh lỗi cũ.
- ConfirmDialog chỉ xác nhận trước hành động. Notice success/error dùng toast cùng cơ chế thông báo đăng nhập, không mở popup lớn biểu tượng tròn giữa màn hình nữa.
- Field/SelectField/RequiredLabel thống nhất dấu `*` đỏ; tên đề, reviewer, hạn, lý do trả lại và các field bắt buộc có marker. Nhận xét review khi gửi là tùy chọn.
- Nút lưu/xác nhận/đóng phù hợp busy, không gửi lặp; trường lỗi có aria-invalid và thông báo. Dữ liệu render bằng text React, không chèn HTML câu hỏi tùy ý.
- Chưa kiểm tra trực quan bằng trình duyệt thật do công cụ browser không khả dụng. Build/lint và so sánh source không đủ để khẳng định pixel-identical; cần nghiệm thu riêng ở mục 13.

## 12. Kiểm chứng đã thực hiện và giới hạn

Các test offline dưới đây không chạy migration, seed hoặc ghi DB. API contract dùng connection local `unused`, chặn request ở xác thực/model validation trước persistence. Lượt kiểm chứng persistence sau khi người dùng cho phép thêm dữ liệu được ghi riêng tại mục 15.

| Kiểm tra | Kết quả tại lượt kiểm chứng |
|---|---|
| BE solution build | Đạt, 0 warnings / 0 errors |
| Application.Tests | 263 passed, 0 failed, 0 skipped |
| Infrastructure.Tests | 8 passed, 0 failed, 0 skipped |
| WebAPI filter an toàn | 90 passed, 0 failed, 0 skipped |
| FE build | Đạt; còn cảnh báo bundle > 500 kB có sẵn, không tăng ngưỡng để che |
| Lint TS/TSX trong diff | Đạt sau sửa helper phạm vi role |
| Năm làm việc | 16 self-check đạt, có case không startDate |
| Validator lịch cũ tương thích | 21 self-check đạt |
| Validator năm học v2 | 9 self-check đạt |
| Identity validation | Self-check đạt, gồm global role/mã 50 ký tự/module không mã |
| Model/migration | Giữ 52 bảng mapped và một migration InitialCreate từ main; không có cột Version mới |
| SQL translation | Query lọc journal bộ đề dịch sang MySQL trước pagination, không mở connection |

Test mới bao gồm năm không học kỳ; bảo vệ alias role; token trạng thái; journal Unicode nhiều mảnh/thiếu mảnh/revision sai/checksum sai/payload sai/bản nộp bất biến; đủ/thiếu câu đúng ô, sai scope/bài/level/type/bank/đáp án; ma trận quyền/trạng thái bộ đề; 401/403 và thiếu version của endpoint.

Persistence TiDB đã có bằng chứng runtime ở mục 15. **Chưa được xác nhận**: race/deadlock với thao tác từ các module khác; hiệu năng quy mô lớn; preview/in và kích thước popup bằng browser thật. Các integration test câu hỏi/đề cũ bị loại khỏi compile trên main do schema cũ vẫn chưa được coi là đã chạy. Test `AcademicYearActivationIntegrationTests` đã chỉnh kỳ vọng theo năm-only nhưng chưa chạy vì tạo/migrate DB. Không chạy toàn bộ test solution một cách mù vì một số test tích hợp tự tạo/xóa database.

## 13. Checklist nghiệm thu khi được phép ghi DB

Không thực hiện các bước tạo/sửa dưới đây trên DB chung khi chưa được người dùng cho phép. Nhóm cấp tài khoản/phạm vi/curriculum/matrix hợp lệ trước; không tạo file seed.

### Dữ liệu tối thiểu

Một trường và phân hiệu ACTIVE; PHT, tổ trưởng, ít nhất hai giáo viên ACTIVE cùng phân hiệu; một giáo viên khác phân hiệu và tài khoản học sinh để test chặn quyền. Đủ 6 nhóm role và user_roles; một năm ACTIVE; môn/khối/chương/bài ACTIVE; ma trận APPROVED có nhiều ô; ngân hàng mỗi ô có nhiều hơn quota để test thay câu; một case ô thiếu câu. Nếu thử reconciler cần dữ liệu kỳ thi/variant/result do người phụ trách module đó cung cấp đúng contract.

### Academic Year / Role / Module

1. Tạo năm chỉ có tên; không phát sinh học kỳ. Kích hoạt đổi năm, một ACTIVE, năm cũ CLOSED. Không thấy ô ngày/học kỳ, dropdown không lỗi khi thiếu startDate.
2. Hai tab sửa năm: tab cũ bị conflict nếu giá trị hiện tại đã khác. Kiểm tra tên trùng/sai định dạng/xóa năm có liên kết.
3. Thêm/sửa role global; không có form mô tả/scope; gán người khác phân hiệu vẫn đúng; 6 nhóm role và alias không xóa được. Kiểm tra thu hồi quyền/session version.
4. Nếu thiếu role, GET không tự ghi; Admin POST sau xác nhận mới thêm nhóm thiếu, không nhân bản alias.
5. Module thêm/sửa mô tả/trạng thái/xóa, không có mã giả; module đã có navbar liên quan vẫn chặn xóa.

### Question

1. Giao task nhiều bài và ba mức nhận thức; giáo viên đúng người thấy gói, người khác không truy cập được bằng URL/ID.
2. Nháp thiếu đáp án vẫn theo quy tắc nháp; submit thiếu quota/đáp án bị chặn. Thêm quá quota, sửa target sai task, sửa sau submit/approve bị chặn.
3. Excel hợp lệ nhập toàn bộ; một dòng lỗi không nhập dòng nào. Kiểm tra template có tên chương/bài và hidden ID, trùng tên không gán nhầm.
4. Submit → reject có nhận xét từng câu → sửa → submit → approve. Bản nộp lần 1 giữ nguyên, reviewer lần 2 thấy đúng snapshot lần 2. Retry response đã thành công không nhân bản submission.
5. Bank filters, availability và picker đúng ô, không lấy câu practice/draft/khác branch/khác bài/khác level/loại/đáp án lỗi. Thử thao tác API trực tiếp để chứng minh server chặn.
6. Chuyển ôn tập thủ công bị chặn khi SCHEDULED/ONGOING; sau sử dụng hoàn tất chỉ câu thực sự dùng được reconciler chuyển; backup và câu còn bị kỳ thi khác giữ không chuyển.

### Exam Package và task liên quan

1. Sinh đủ câu không trùng, tổng điểm/ô đúng; thiếu một ô phải rollback, không có task/set rác.
2. Xem chi tiết/preview/in; đổi tên nháp; giao reviewer cùng branch khác creator với hạn hợp lệ. Task hiện đúng người và link quay về bộ đề.
3. Reviewer thay câu đúng ô; cố dùng câu sai ô/trùng trong đề/câu practice phải bị từ chối, kể cả gọi API trực tiếp.
4. Submit → PHT return có lý do → reviewer sửa/gửi → PHT finalize. Giáo viên không finalize được. Sau finalize không mutation nội dung/giao lại/xóa.
5. Hai tab cùng version thay câu/gửi/chốt; request đi sau trả 409, không tạo hai sự kiện cùng revision.
6. Xem/in đề đã chốt sau khi dữ liệu nguồn thay đổi bằng quy trình hợp lệ: snapshot cũ không đổi. Tạo copy ôn tập một lần, giữ thời lượng và liên kết nguồn; request thứ hai bị chặn. Kỳ thi chưa kết thúc chặn copy.
7. Task biên soạn có hai đề: giao review một đề không làm mất đề khỏi task gốc; reviewer chỉ thấy đề được giao. Chốt từng đề cập nhật status tổng hợp đúng. Không sửa task review qua form task thường.
8. Xóa nháp trực tiếp; xóa nháp thuộc task rồi soạn lại đúng ID; không xóa đề đã giao/sử dụng/có variant.
9. Thu hồi role/đổi phân hiệu khi request đang chờ khóa: mutation phải kiểm tra lại và bị từ chối khi không còn quyền.

### Giao diện chung

Đối chiếu desktop và màn hình nhỏ; popup return/assign/replace/confirm không có thanh cuộn ngang, textarea nằm trong khung, Tab/Escape/focus/busy đúng. Mọi field bắt buộc có một dấu `*` đỏ, field tùy chọn không có. Toast success/error cùng vị trí/kiểu với đăng nhập. Kiểm tra thêm một màn hình chung của thành viên khác dùng ConfirmDialog/Field để phát hiện regression CSS mà không viết lại nghiệp vụ màn hình đó.

## 14. Các bước tiếp theo, không tự thực hiện

1. Người dùng/nhóm review diff trên nhánh phucvm và tài liệu này.
2. Cấu hình kết nối `sep_490` bằng User Secrets/env của máy chạy theo DB_V2.md; không lưu credential trong Git. Không chạy `dotnet ef database update` chỉ để thử kết nối.
3. Dữ liệu thử và kiểm tra API đã thực hiện theo mục 15; tiếp tục nghiệm thu phần UI, in và concurrency còn thiếu. Không trỏ `TELO_TEST_MYSQL` vào TiDB chung vì các test đó có thể tạo/xóa DB.
4. Chờ contract lịch sử import giáo viên và xử lý Email ở lượt riêng.
5. Chỉ commit/push/merge khi người dùng yêu cầu riêng; lượt này để nguyên thay đổi chưa commit.

## 15. Bổ sung sau khi được phép thêm dữ liệu thử online

Ngày 15/10/2026, người dùng yêu cầu thêm nhiều dữ liệu đủ trạng thái cho các phần của phucvm và cho biết sẽ xóa sau khi thử. Đã thêm trực tiếp vào `sep_490`, không tạo file seed, không migration/thay schema. Dữ liệu và hướng dẫn đầy đủ ở [online-test-data-20261015](online-test-data-20261015/README.md); [manifest](online-test-data-20261015/manifest.json) giới hạn exact ID để dọn riêng về sau, không chứa mật khẩu hay payload câu hỏi.

Đã có 44 tài khoản, 26 năm học, 36 role, 35 module, 1.596 câu hỏi và 50 bộ đề cùng dependency cần thiết. Workflow câu hỏi và bộ đề đã chạy thật qua API: gửi/duyệt/trả lại/gửi lại, thay câu, giao lại reviewer, chốt PHT, copy ôn tập; task biên soạn giữ danh sách paper khi chuyển review và đồng bộ trạng thái tổng hợp. Có 29 mục kiểm tra runtime trong manifest và các kiểm tra nhất quán dữ liệu. Đây không phải bằng chứng browser UI hoặc kiểm thử đầy đủ race/deadlock.

Persistence thật phát hiện 3 câu SQL khóa còn dùng tên bảng/cột cũ; đã sửa riêng trong `ExamPackageCommands`, `QuestionBankService`, `QuestionPaperServiceV2` sang `branch.branch_id` đúng mapping v2. Build solution và 263 Application.Tests đạt sau sửa. Không đổi model mapping hoặc phần kỳ thi của thành viên khác. Một semester, 5 kỳ thi, 2 variant và 1 bài nộp chỉ là fixture phụ thuộc để kiểm tra bảo vệ/chuyển ôn tập của câu hỏi/đề; chức năng quản lý học kỳ không được khôi phục và worker vẫn tắt.
