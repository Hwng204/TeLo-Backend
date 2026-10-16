# Nhiệm vụ (UC 16)

Một danh sách chung cho ba loại nhiệm vụ, phân biệt bằng cột **Loại nhiệm vụ**:

| Loại | Ai giao → ai làm | Người kiểm duyệt |
|---|---|---|
| Ma trận (`MATRIX`) | PHT → Tổ trưởng | PHT giao việc |
| Đề thi (`EXAM`) | PHT → Tổ trưởng | PHT giao việc |
| Câu hỏi (`QUESTION`) | Tổ trưởng → Giáo viên | Tổ trưởng giao việc |

## Quy tắc

- **Người kiểm duyệt:** luôn là người giao việc; form không chọn người kiểm duyệt và không có hạn kiểm duyệt. `reviewer`
  trong danh sách và chi tiết trả người giao; client cũ gửi `reviewerUserId`, `reviewDueAt` thì hai trường này bị bỏ qua.
- **Ai thấy (GB-54):** chỉ người giao, người thực hiện, người kiểm duyệt. Tài khoản phải thuộc một phân hiệu
  (Hiệu trưởng không có phân hiệu chỉ xem nhiệm vụ của mình).
- **Trạng thái (GB-53):** `ASSIGNED` Đã giao, `IN_PROGRESS` Đang thực hiện, `PENDING_REVIEW` Chờ kiểm duyệt,
  `NEEDS_REVISION` Cần chỉnh sửa, `COMPLETED` Hoàn thành ("Đã nộp" gộp vào Chờ kiểm duyệt, `SUBMITTED` không còn dùng).
  Quá hạn (`isOverdue`, lọc `status=OVERDUE`): Đã giao, Đang thực hiện hoặc Cần chỉnh sửa mà `dueAt` trước hôm nay.
  Chờ kiểm duyệt (người thực hiện đã gửi xong) và Hoàn thành thì không quá hạn.
- **Nhiệm vụ ma trận** đổi trạng thái cùng transaction với ma trận: Tổ trưởng lưu nháp → `IN_PROGRESS`; xoá bản nháp →
  `ASSIGNED`; nộp (kể cả nộp lại sau khi bị từ chối) → `PENDING_REVIEW`; PHT từ chối → `NEEDS_REVISION`; PHT duyệt →
  `COMPLETED`. Ma trận PHT tự tạo (không gắn nhiệm vụ) không đổi nhiệm vụ nào.
- **Hạn (GB-52):** không ở trong quá khứ; khi sửa, giữ nguyên hạn cũ thì được (kể cả đã quá hạn). Đề thi bắt buộc hạn.
  Câu hỏi bắt buộc hạn biên soạn (`dueAt`); không có hạn kiểm duyệt.
- **Sửa (GB-56):** chỉ người giao, khi chưa Hoàn thành. Người thực hiện đã bắt đầu (có ma trận, bộ đề hoặc câu hỏi
  gắn với nhiệm vụ, hoặc nhiệm vụ đã rời Đã giao) thì chỉ đổi được nội dung và hạn.
  Người, ma trận không đổi thì không bị kiểm tra lại (giáo viên đã nghỉ, ma trận đã lưu trữ vẫn sửa được hạn).
- **Xoá (GB-55):** chỉ người giao, khi chưa bắt đầu. Kiểm tra và xoá trong một câu lệnh; bài học, số câu và đề cần tạo xoá theo.
- **Đề thi (GB-57):** không gắn kỳ thi (khi tạo kỳ thi mới chọn các đề sẽ thi). Ma trận nguồn phải Đã duyệt và cùng phân
  hiệu với người giao; phạm vi nhiệm vụ lấy theo ma trận. PHT chọn số đề `paperCount` 1–20 (lỗi `paperCount`: "Số đề cần
  tạo từ 1 đến 20."), hệ thống đặt tên `Đề 1` … `Đề N`, không sửa tên. Sửa khi chưa bắt đầu mà đổi số đề thì dựng lại các
  dòng `exam_task_papers` cho đủ N; đã bắt đầu thì số đề bị khoá như nội dung chuyên môn khác (GB-56).
- **Email (GB-58):** đang khoá (`LockedEmailNotificationService`), giao hay sửa nhiệm vụ đều không gửi email. Khi mở lại: giao nhiệm vụ ma trận gửi sự kiện `MATRIX_ASSIGNED`; sửa mà đổi người nhận hoặc hạn thì gửi lại
  sự kiện đó. Nhiệm vụ đề thi và câu hỏi chưa có sự kiện email trong danh mục.
- `GET /api/matrix-tasks/{id}` cũng chỉ cho người tham gia đọc nhiệm vụ; xoá nhiệm vụ dùng `DELETE /api/tasks/{id}`.
- Ma trận của nhiệm vụ chỉ người kiểm duyệt nhiệm vụ được duyệt/từ chối. Tạo ma trận và sửa người nhận được tuần tự hoá bằng khoá dòng nhiệm vụ.
- Phạm vi nhiệm vụ lấy theo ma trận (môn, khối); nhiệm vụ không có năm học, học kỳ. Sửa hạn/tên nhiệm vụ giữ phạm vi cũ nếu không chọn lại.
- Chưa làm (thuộc chức năng khác): soạn và nộp từng đề, soạn, gửi và duyệt câu hỏi. Màn chi tiết hiện các nút này ở trạng thái "Sắp có".

## API

Trả về bọc `ApiResponse`. Lỗi: `VALIDATION_ERROR` 422 (`error.details` theo trường), `BRANCH_REQUIRED` / `FORBIDDEN` 403,
`TASK_NOT_FOUND` 404, `TASK_STARTED` / `TASK_COMPLETED` 409.

| API | Tác dụng |
|---|---|
| `GET /api/tasks` | Danh sách. Lọc `page`, `pageSize` (≤ 100), `keyword`, `taskType`, `status` (mã trạng thái hoặc `OVERDUE`) |
| `GET /api/tasks/options` | `{ teachers, teamLeads, matrices }`: giáo viên (cho Tổ trưởng); Tổ trưởng và mọi ma trận Đã duyệt của phân hiệu `{ id, name, subjectName, gradeLevelName, academicYearName }` (cho PHT) |
| `GET /api/tasks/{id}` | Chi tiết theo loại: phạm vi + ma trận đã lập `matrix: { id, name, status, statusLabel, updatedAt, rejectComment, rejectedAt }` (bản nháp: `id`/`name` null với người không phải Tổ trưởng được giao; `rejectComment`/`rejectedAt` là lần từ chối gần nhất, ai xem được nhiệm vụ cũng thấy, null khi ma trận chưa bị từ chối hoặc đã nộp lại); `exam: { matrixId, matrixName, subjectName, gradeLevelName, academicYearName, papers: [{ name, status, statusLabel, updatedAt }] }`; hoặc các bài học và tiến độ |
| `DELETE /api/tasks/{id}` | Xóa; `message` là câu báo `Đã xóa nhiệm vụ "{name}".` |
| `PUT /api/tasks/matrix/{id}` | Sửa nhiệm vụ ma trận, body như `POST /api/matrix-tasks` |
| `POST /api/tasks/exam`, `PUT /api/tasks/exam/{id}` | Giao / sửa nhiệm vụ đề thi |
| `POST /api/tasks/question`, `PUT /api/tasks/question/{id}` | Giao / sửa nhiệm vụ câu hỏi |

Tạo nhiệm vụ ma trận vẫn là `POST /api/matrix-tasks` (xem MATRIX.md), nay chặn hạn ở trong quá khứ.

```json
{ "name": "Tạo bộ đề Toán 5 · Cuối học kỳ I", "examMatrixId": 896, "assignedToUserId": 4,
  "dueAt": "2026-11-30T00:00:00", "paperCount": 2 }
```

```json
{ "name": "Biên soạn câu hỏi ôn tập Chương 1", "assignedToUserId": 9641, "dueAt": "2026-10-20T00:00:00",
  "lessons": [{ "lessonId": 1, "knowledge": 8, "understanding": 6, "application": 4 }] }
```

## Migration và cấu hình

- Migration: sau khi chuyển DB v2 chỉ còn `InitialCreate` và `AddChapterLessonNumber` (xem DB_V2.md); các migration cũ,
  kể cả `AddTaskReviewersAndExamPapers`, đã được gộp và xoá.
- `TaskAuth:TeacherRoleCodes` (mặc định `GIAO_VIEN`, `TEACHER`): vai trò được giao nhiệm vụ câu hỏi.
  PHT, Hiệu trưởng, Tổ trưởng đọc từ `MatrixAuth:*`.
