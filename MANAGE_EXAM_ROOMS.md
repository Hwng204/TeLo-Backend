# Manage Exam Rooms

Module quản lý phòng thi dành cho role `PHT` (Phó hiệu trưởng), sử dụng policy
`ExamManager` và route lồng trong kỳ thi.

## API

| Chức năng | HTTP | Route |
|---|---|---|
| Danh sách phòng thi | GET | `/api/exams/{examId}/rooms` |
| Danh sách phòng vật lý hợp lệ | GET | `/api/exams/{examId}/rooms/options` |
| Chi tiết phòng thi | GET | `/api/exams/{examId}/rooms/{id}` |
| Thêm phòng thi | POST | `/api/exams/{examId}/rooms` |
| Cập nhật phòng thi | PUT | `/api/exams/{examId}/rooms/{id}` |
| Xóa phòng thi | DELETE | `/api/exams/{examId}/rooms/{id}` |

Request tạo mới:

```json
{
  "code": "k5p01",
  "roomId": 12,
  "candidateLimit": 30
}
```

Request cập nhật có cùng cấu trúc.

## Quy tắc nghiệp vụ

- Mã phòng thi được trim, chuyển thành chữ thường và phải có dạng `k5p01`.
- Mã phòng thi là duy nhất trong một kỳ thi.
- Một phòng vật lý chỉ được thêm một lần vào cùng kỳ thi.
- Phòng vật lý phải thuộc cùng cơ sở trường với kỳ thi.
- Sức chứa thí sinh phải lớn hơn 0.
- Không cho xóa phòng thi đã được gán vào ca thi.
- ID phòng thi luôn được kiểm tra trong phạm vi `examId` trên route.

## Database

Migration `AddExamRoomCode` thêm cột `exam_rooms.code` (`varchar(50)`, `NOT NULL`)
và unique index `(exam_id, code)`. Dữ liệu cũ được backfill mã duy nhất trước khi
áp dụng ràng buộc `NOT NULL` và unique index.
