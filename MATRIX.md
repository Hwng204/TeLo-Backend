# Module Ma trận đề thi

Các bước cài đặt chung (MySQL, chuỗi kết nối, restore, migration) xem [README.md](README.md). Tài liệu này chỉ nói riêng phần ma trận.

## Nhập Excel và tải mẫu

- `GET /api/matrices/import/template.xlsx`: tải mẫu gồm các bài học của phạm vi đang chọn × ba mức nhận thức.
- `POST /api/matrices/import/preview`: multipart trường `file`, kiểm tra toàn bộ tệp và trả `{canImport,name,totalScore,lessonCount,filledLines,details,errors}`. `errors` có `rowNumber` (null cho lỗi cả tệp) và `message`; có lỗi thì `details` rỗng.
- `POST /api/matrices/import`: gửi lại chính tệp để kiểm tra theo chương trình hiện tại. Tệp còn lỗi trả 422 `ImportHasInvalidRows`; hợp lệ trả nội dung đưa vào bản soạn. **Không tự ghi DB**; tiếp tục dùng tạo/cập nhật/lưu nháp/nộp/xác nhận.

Cả ba API nhận query `academicContextId`, `semesterId` (tùy chọn), `totalScore` (mặc định 10, nguyên dương), `name` (tùy chọn). Phân quyền PHT/Hiệu trưởng/Tổ trưởng và phạm vi theo reference data hiện có; trạng thái ma trận khi lưu vẫn do API ma trận kiểm tra.

Chỉ nhận `.xlsx` tối đa 5 MB, 5000 dòng và 32 cột; giới hạn nội dung giải nén 50 MB. Không dùng công thức. Đọc sheet đầu tiên, tìm cột theo tiêu đề Bài học / Mức nhận thức / Số câu / Tỷ lệ %; giữ tương thích mẫu và file xuất trước đây. Bài học cần khớp chính xác nhãn đầy đủ (bài + chương), nhãn xuất cũ `Tên chương / Tên bài`, hoặc tên bài duy nhất trong phạm vi. Không suy đoán lại tên bài khi tên chương sai. File xuất mới dùng nhãn có mã bài và mã chương. Mẫu và file xuất mới có cột ẩn `ID bài học` để phân biệt chương/bài cùng tên và mã ở các lĩnh vực của cùng môn. ID vẫn phải khớp tên bài và nằm trong phạm vi; file cũ không có cột này tiếp tục dùng đối chiếu tên, trường hợp mơ hồ cần tải mẫu mới.

Ô có cả Số câu và Tỷ lệ % trống được bỏ qua; ô điền một nửa, trùng bài/mức, bài ngoài phạm vi, sai số hoặc loại câu hỏi khác Trắc nghiệm đều chặn cả tệp. Số câu là uint dương, tỷ lệ trong (0,100] với tối đa hai chữ số thập phân; không tự làm tròn dữ liệu sai. Tổng tỷ lệ chưa bằng 100% vẫn được nhập/soạn nháp; nộp/xác nhận và sửa bản đã nộp tiếp tục áp dụng luật 100% hiện có.

## 1. Đăng nhập và chạy thử

Backend dùng access token JWT. Cần khóa ký (tối thiểu 32 ký tự) đặt qua biến môi trường, **không** ghi vào file được commit:

```powershell
$env:Jwt__SigningKey = "chuoi-bi-mat-it-nhat-32-ky-tu-cua-ban-1234"
dotnet run --project WebAPI --launch-profile http
```

Các biến `$env:` chỉ có hiệu lực trong cửa sổ PowerShell đang mở. Thiếu khóa thì backend không khởi động. Các thông số còn lại (`Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes`, mặc định 60 phút) nằm trong `appsettings.json`.

**Lấy access token:** `POST /api/auth/login`
```json
{ "username": "pht_a", "password": "mat-khau" }
```
Trả về `{ "accessToken": "...", "tokenType": "Bearer", "expiresAtUtc": "..." }`. Sai tên đăng nhập hoặc mật khẩu trả 401. Mọi user có trạng thái `ACTIVE` đều đăng nhập được, token mang đủ danh sách vai trò của user.

**Dùng token:** gửi header `Authorization: Bearer <accessToken>`. Trong Swagger bấm **Authorize** rồi dán token (không gõ chữ `Bearer`).

Mật khẩu trong cột `users.password_hash` phải được băm bằng `PasswordHasher` của ASP.NET Identity (PBKDF2). Cách băm khác (bcrypt...) sẽ không đăng nhập được.

Token chứa: `NameIdentifier` (id user), `Name` (tên đăng nhập), `Role` (mỗi vai trò một claim) và `branch_id` (chi nhánh của user, nếu có). API ma trận cần vai trò `HIEU_TRUONG`, `PHT` hoặc `TEAM_LEAD`; user có vai trò khác đăng nhập được nhưng gọi API ma trận bị 403.

API cần sẵn dữ liệu: năm học, học kỳ, sách giáo khoa (chương, bài học), ngữ cảnh học thuật, và user Tổ trưởng cùng chi nhánh để giao việc. Repo không kèm dữ liệu mẫu.

## 2. Quy ước API

- Thành công: trả object trực tiếp (không bọc `ApiResponse`).
- Lỗi: Problem Details, có `code` (tiếng Anh, để xử lý) và `detail` (tiếng Việt, để hiển thị).
- Mã HTTP: `401` chưa xác thực, `403` sai quyền hoặc sai chi nhánh, `404` không thấy, `409` xung đột trạng thái, `422` dữ liệu không hợp lệ.
- `statusLabel` là nhãn tiếng Việt của `status`. `allowedActions` cho biết thao tác nào được phép với người dùng và trạng thái hiện tại.
- Tổng số câu (`totalQuestions`) do server tính từ chi tiết, không gửi lên. Tổng điểm (`totalScore`) thì ngược lại: là số nguyên dương do người tạo tự nhập khi tạo/sửa, server chỉ lưu lại chứ không tính.
- **Người lập:** mỗi ma trận có `createdBy` / `createdAt` (người tạo; bản sao là người bấm sao chép), và `approvedBy` / `approvedAt` khi được duyệt hoặc xác nhận. Người dùng trả về dạng `{ userId, fullName, roleLabel }` với `roleLabel` là Hiệu trưởng / Phó Hiệu trưởng / Tổ trưởng (`null` nếu không có vai trò ma trận). Ma trận tạo trước khi có tính năng này: `createdBy` là Tổ trưởng được giao nếu gắn nhiệm vụ, còn không thì `null`. Không còn mã dạng `MT-2026-014` — định danh nghiệp vụ dùng `name`.
- **Nhiệm vụ:** bắt buộc có `name` (tên nhiệm vụ, tách biệt với `description` — yêu cầu công việc, vẫn tùy chọn) khi tạo; trả thêm `name` và `createdBy` (người giao). Không còn mã dạng `NV-MT-{id}`.
- **Thang điểm theo tỷ lệ %.** Mỗi dòng chi tiết gửi `percentage` (0, 100] — tỷ lệ % điểm của dòng đó trong tổng điểm ma trận (`totalScore`, số nguyên dương do người tạo tự nhập). Điểm ô và điểm mỗi câu là giá trị **suy ra**, server trả sẵn qua `cellScore` (= `totalScore × percentage / 100`) để tránh lệch làm tròn khi frontend tự tính. **Tổng `percentage` của các dòng chi tiết phải bằng đúng 100 khi Nộp, Xác nhận, và khi sửa ma trận đang ở trạng thái Đã nộp.** Sai thì trả `422` mã `InvalidTotalScore`. Ma trận **Nháp lưu được ở mọi tổng %** (kể cả rỗng, vượt 100 cho từng dòng vẫn bị chặn ở mức (0,100] mỗi dòng) để soạn dở dang nhiều lần; `allowedActions` chỉ có `Submit`/`Confirm` khi tổng đúng 100%.
- Mức nhận thức: `NHAN_BIET`, `THONG_HIEU`, `VAN_DUNG`. Loại câu hỏi luôn là trắc nghiệm, không cần gửi.

## 3. Luồng nghiệp vụ

Ma trận: `Nháp → Đã nộp → Đã duyệt → Đã lưu trữ`. PHT có thể **từ chối** ma trận đã nộp để đưa về `Nháp` (kèm nhận xét) cho Tổ trưởng làm lại. Muốn sửa ma trận đã duyệt thì **sao chép** thành bản Nháp mới.

- **PHT tự tạo:** tạo Nháp, bấm Xác nhận là thành Đã duyệt.
- **Nháp của Tổ trưởng là riêng tư:** ma trận gắn nhiệm vụ đang ở `Nháp` (kể cả sau khi bị từ chối) **không** hiện trong danh sách của PHT/Hiệu trưởng, và xem/sửa/xóa trực tiếp trả `403` ("Ma trận đang được Tổ trưởng soạn, chỉ xem được sau khi nộp."). PHT thấy lại ngay khi Tổ trưởng nộp (`Đã nộp`).
- **PHT giao Tổ trưởng:** PHT giao nhiệm vụ, Tổ trưởng tạo đúng một ma trận cho nhiệm vụ đó, nộp, PHT duyệt.
- Nhiệm vụ: `Đã giao → Đã nộp → Hoàn thành`. PHT từ chối ma trận thì nhiệm vụ về `Đã giao` để Tổ trưởng làm lại.
- **Từ chối:** chỉ PHT/Hiệu trưởng, chỉ với ma trận `Đã nộp`. Ma trận về `Nháp`, response có `rejectComment`, `rejectedAt`, `rejectedByUserId`. Tổ trưởng sửa rồi nộp lại (nhận xét cũ bị xóa khi nộp lại). Tổ trưởng **không** thu hồi được ma trận đã nộp; PHT vẫn sửa trực tiếp được trước khi duyệt hoặc từ chối.
- Ma trận không có dòng chi tiết thì không nộp hoặc xác nhận được.

## 4. Danh sách API

| API | Tác dụng |
|---|---|
| `GET /api/matrices` | Danh sách (phân trang; lọc từ khóa, ngữ cảnh, học kỳ, trạng thái) |
| `GET /api/matrices/{id}` | Chi tiết, tổng, `allowedActions` |
| `POST /api/matrices` | Tạo Nháp (`taskId: null` là tạo trực tiếp) |
| `PUT /api/matrices/{id}` | Sửa tên và chi tiết |
| `DELETE /api/matrices/{id}` | Xóa (chỉ khi Nháp) |
| `POST /api/matrices/{id}/submit` | Nộp |
| `POST /api/matrices/{id}/reject` | Từ chối (PHT): về Nháp, kèm nhận xét tùy chọn (`{"comment": "..."}`, tối đa 1000 ký tự) |
| `POST /api/matrices/{id}/approve` | Duyệt (PHT) |
| `POST /api/matrices/{id}/confirm` | Xác nhận nhanh ma trận tự tạo (PHT) |
| `POST /api/matrices/{id}/archive` | Lưu trữ (PHT) |
| `POST /api/matrices/{id}/clone` | Sao chép thành Nháp mới (PHT) |
| `GET /api/matrices/{id}/export.xlsx` | Xuất Excel (đã duyệt hoặc đã lưu trữ) |
| `POST /api/matrix-tasks` | Giao nhiệm vụ cho Tổ trưởng |
| `GET /api/matrix-tasks` | Nhiệm vụ đã giao (PHT, Hiệu trưởng). Lọc: `page`, `pageSize`, `status`, `dueBefore`, `assignedToUserId`, `keyword`, `academicContextId` |
| `GET /api/my/matrix-tasks` | Nhiệm vụ của tôi (Tổ trưởng). Cùng bộ lọc, trừ `assignedToUserId` |
| `GET /api/matrix-tasks/{id}` | Chi tiết nhiệm vụ |
| `GET /api/matrix-reference-data` | Dữ liệu dựng form: ngữ cảnh, học kỳ, bài học, Tổ trưởng, mức nhận thức |

Body tạo ma trận mẫu:

```json
{
  "name": "Ma trận Toán 5 - HK1",
  "academicContextId": 1,
  "semesterId": 1,
  "taskId": null,
  "totalScore": 10,
  "details": [
    { "lessonId": 1, "cognitiveLevel": "NHAN_BIET", "questionCount": 4, "percentage": 100 }
  ]
}
```

## 5. Cấu hình

| Mục | Ở đâu | Ghi chú |
|---|---|---|
| Origin frontend | `Cors:AllowedOrigins` trong `appsettings.json` | Mặc định `http://localhost:5173` |
| Mã vai trò | `MatrixAuth:PrincipalRoleCodes`, `PhtRoleCodes`, `TeamLeadRoleCodes` | Mặc định `HIEU_TRUONG`, `PHT`, `TEAM_LEAD` / `TO_TRUONG` |
| Khóa ký token | biến `Jwt__SigningKey` | Tối thiểu 32 ký tự, không commit |

Frontend đặt `VITE_API_BASE_URL` trỏ đúng cổng backend (ví dụ `http://localhost:5035/api`).
