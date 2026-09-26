# Manage Exam Subjects

Module quản lý môn thi dùng luồng `Controller -> Application Service -> Repository/Unit of Work -> EF Core`
và sử dụng bảng `exam_subjects` hiện có, không cần migration.

Tất cả endpoint yêu cầu JWT thỏa policy `ExamSubjectManager`: role `PHT`
(Phó hiệu trưởng) hoặc claim `permission=exam_subject.manage`.

## API

| Chức năng | HTTP | Route | Thành công |
|---|---|---|---|
| Xem danh sách môn thi | GET | `/api/exams/{examId}/subjects` | 200 |
| Thêm môn thi | POST | `/api/exams/{examId}/subjects` | 201 |
| Cập nhật môn thi | PUT | `/api/exams/{examId}/subjects/{id}` | 200 |
| Xóa môn thi | DELETE | `/api/exams/{examId}/subjects/{id}` | 200 |

### Thêm môn thi

```json
{
  "subjectId": 5,
  "durationMinutes": 90
}
```

Môn thi mới có trạng thái `ACTIVE`.

### Cập nhật môn thi

```json
{
  "subjectId": 5,
  "durationMinutes": 120,
  "status": "INACTIVE"
}
```

Trạng thái hợp lệ là `ACTIVE` hoặc `INACTIVE`.

## Quy tắc nghiệp vụ

- Kỳ thi và môn học phải tồn tại.
- `subjectId` phải lớn hơn 0 và `durationMinutes` phải lớn hơn 0.
- Một môn học chỉ được thêm một lần trong cùng kỳ thi.
- ID môn thi luôn được kiểm tra trong phạm vi `examId`; không thể cập nhật hoặc xóa bản ghi của kỳ thi khác.
- Không cho xóa môn thi đã có cấu hình khối lớp (`exam_subject_grade_levels`) để tránh cascade dữ liệu ngoài ý muốn.
- `resultPublishedAt` và `resultPublishedByUserId` là dữ liệu chỉ đọc trong module này.

## Mã lỗi chính

| Code | HTTP | Ý nghĩa |
|---|---:|---|
| `VALIDATION_ERROR` | 422 | Request không hợp lệ |
| `EXAM_NOT_FOUND` | 404 | Không tìm thấy kỳ thi |
| `SUBJECT_NOT_FOUND` | 404 | Không tìm thấy môn học |
| `EXAM_SUBJECT_NOT_FOUND` | 404 | Không tìm thấy môn thi trong kỳ thi |
| `EXAM_SUBJECT_ALREADY_EXISTS` | 409 | Môn đã tồn tại trong kỳ thi |
| `EXAM_SUBJECT_HAS_DEPENDENCIES` | 409 | Môn thi đã có cấu hình khối lớp |
