# Quản lý câu hỏi trắc nghiệm

Đã triển khai backend và frontend cho biên soạn thủ công, import Excel, nộp/duyệt toàn bộ gói, ngân hàng thi/ôn tập, cảnh báo quá hạn, xem câu theo ma trận và chọn câu vào từng đề được giao. Biên soạn luôn có bốn ô A–D và một đáp án đúng. Giáo viên điền ít nhất hai lựa chọn khác nhau, bỏ trống bất kỳ ô không dùng nào; khi xem, ô trống hiển thị “—”. AI để sau.

## Quy tắc lựa chọn và ma trận

Ma trận hiện tại chỉ hỗ trợ `MULTIPLE_CHOICE`, quy định bài học, mức nhận thức, số câu và tỷ lệ điểm. Ma trận chưa có thuộc tính quy định số phương án của câu hỏi. Quy tắc cũ bắt đủ nội dung cả bốn ô nằm riêng ở `QuestionRules.IsComplete`, không suy ra từ số câu trong ma trận.

Giữ hợp đồng API gồm bốn phần tử theo đúng thứ tự A, B, C, D; ô không dùng là chuỗi rỗng. Không thay ô trống bằng dấu gạch ngang trong dữ liệu. Đáp án đúng phải trỏ đến một lựa chọn có nội dung. So sánh lựa chọn phân biệt chữ hoa/thường để hỗ trợ câu hỏi chính tả. Câu chưa hoàn chỉnh vẫn lưu nháp được, nhưng không được gửi hoặc nhập Excel như câu hợp lệ.

Số lựa chọn phụ thuộc dạng đề: phần trắc nghiệm nhiều lựa chọn của đề tốt nghiệp THPT theo định dạng từ 2025 dùng bốn phương án; đúng/sai nhiều ý và trả lời ngắn là các dạng khác. Tùy chọn bỏ trống phục vụ yêu cầu biên soạn hiện tại của hệ thống, không thay thế yêu cầu đủ A–D của một mẫu đề cụ thể. Tham khảo [cấu trúc đề thi của Bộ GDĐT](https://vqa.moet.gov.vn/vi/news/thong-bao/cau-truc-dinh-dang-de-thi-tot-nghiep-thpt-tu-nam-2025-74.html) và [hướng dẫn môn Hóa học lớp 11 của Bộ GDĐT](https://moet.gov.vn/content/vanban/Lists/VBDH/Attachments/3713/7mon-hoa-hoc-lop-11-ban-chinh-thuc-chot-ngay-392024signed.pdf).

## Excel dành cho giáo viên

Trong **Nhập Excel** có hai file tải xuống:

- **Tải file mẫu**: một trang **Câu hỏi**, bảy cột nhìn thấy: nội dung, A, B, C, D, đáp án đúng, giải thích. Hệ thống tạo sẵn nhóm bài học/mức nhận thức và đúng số dòng còn thiếu. Giáo viên chỉ điền nội dung dưới mỗi nhóm; không nhập mã bài hoặc mã yêu cầu. Ô lựa chọn không dùng và dòng chưa làm để trống. Giải thích không bắt buộc.
- **Tải hướng dẫn và ví dụ**: file riêng có trang **Hướng dẫn** với bốn bước thực hiện và trang **Ví dụ** với các câu đã điền, gồm câu có hai, ba, bốn lựa chọn và câu bỏ trống lựa chọn B/D. File này chỉ để xem, không dùng để nhập.

Chọn file `.xlsx` đã điền để kiểm tra. Lỗi chỉ rõ dòng và cột bằng tiếng Việt, kể cả đáp án đúng trỏ vào ô trống. Chưa xác nhận thì chưa lưu câu hỏi. Khi hợp lệ, **Xác nhận nhập** bổ sung câu vào gói, giữ các câu đã lưu trước đó. Có thể nhập một phần và tải mẫu mới cho số câu còn thiếu. Khi đủ theo từng bài/mức, **Gửi gói câu hỏi** để tổ trưởng kiểm duyệt.

Tệp tối đa 5 MB và 5.000 câu. Không dùng công thức hoặc đổi cấu trúc nhóm. Mẫu cũ có cột bài học/mức nhận thức vẫn được hỗ trợ để giáo viên không mất công nhập lại file đang làm.

## Chạy thử local

Frontend: `http://localhost:5173/`. API: `http://localhost:5035/api`. Các tiến trình thử nghiệm được chạy ẩn; PID/log nằm ở `.tmp/question-management/` của workspace. Worker gửi email được tắt cho tiến trình thử nghiệm này.

Tài khoản mẫu:

| Tài khoản | Vai trò |
|---|---|
| `question_demo_gv` | Giáo viên |
| `question_demo_tl` | Tổ trưởng |
| `question_demo_pht` | Phó Hiệu trưởng |
| `question_demo_ht` | Hiệu trưởng |

Mật khẩu thử mặc định: `Question-demo-123!`. Chỉ dùng các tài khoản này cho môi trường phát triển local. Khi seed bằng `TELO_QUESTION_DEMO_PASSWORD`, dùng giá trị đã đặt thay cho mật khẩu mặc định. Seeder không đổi mật khẩu hoặc reset nhiệm vụ mẫu đã có.

Database local `sep` đã áp hai migration:

- `20261013162424_AddQuestionPackages`.
- `20261013170332_AddQuestionPaperLinks`.

Bản sao lưu trước khi cập nhật nằm ở `.tmp/question-management/before-question-migrations.sql`, ngoài hai repository. Không commit bản sao lưu này.

Tái tạo dữ liệu mẫu trong database local đã cấu hình:

```powershell
# Chạy từ TeLo-Backend; lấy kết nối từ environment hoặc user-secrets hiện có.
dotnet run --project Tools/QuestionDemo/QuestionDemo.csproj -- --seed
```

Seeder chỉ hỗ trợ MySQL trên localhost/127.0.0.1/::1. Thêm dữ liệu riêng với tiền tố `QM DEMO`, dùng chương `QM_DEMO`; giữ nguyên dữ liệu và thao tác thử trước đó khi chạy lại.

## Các luồng thử

1. Đăng nhập Giáo viên, vào **Nhiệm vụ**, mở `QM DEMO · Biên soạn câu hỏi Toán 5`, chọn **Biên soạn câu hỏi**. Có hai câu đã lưu và các ô bài/mức còn thiếu. Thử sửa, lưu, thêm câu tiếp và tiếp tục sau khi tải lại trang.
2. Dùng **Nhập Excel → Tải hướng dẫn và ví dụ** để xem cách điền, rồi **Tải file mẫu**. Mẫu chia sẵn các nhóm bài/mức và số dòng còn thiếu, tối đa 5.000 câu mỗi tệp. Điền nội dung, ít nhất hai lựa chọn trong A–D và đáp án đúng; lựa chọn không dùng và giải thích có thể để trống. Kết quả kiểm tra chưa lưu dữ liệu; xác nhận nhập bổ sung vào gói, không thay câu đã soạn thủ công.
3. Gửi gói khi mọi câu hoàn chỉnh và đúng chỉ tiêu ở mỗi ô. Gói chờ duyệt được khóa sửa.
4. Đăng nhập Tổ trưởng, mở `QM DEMO · Gói câu hỏi chờ kiểm duyệt`. Chọn câu để xem đáp án/giải thích và ghi nhận xét; **Yêu cầu chỉnh sửa** cần nhận xét tổng, hoặc **Chốt gói** để đưa toàn bộ câu vào ngân hàng thi.
5. Dùng `QM DEMO · Gói cần chỉnh sửa` để thử Giáo viên sửa và nộp lại. Lịch sử lưu nội dung từng lần nộp cùng nhận xét; chỉnh sửa lần sau không đổi snapshot trước.
6. Tổ trưởng có cảnh báo `QM DEMO · Nhiệm vụ quá hạn`; bấm cảnh báo để lọc các nhiệm vụ câu hỏi quá hạn.
7. Bốn role mở **Ngân hàng câu hỏi**, dùng hai tab **Câu hỏi thi / Câu hỏi ôn tập**, tìm kiếm và lọc. PHT có thao tác **Chuyển sang ôn tập**. Hiệu trưởng, Tổ trưởng và Giáo viên có quyền xem, không có quyền chuyển thủ công.
8. Tổ trưởng mở nhiệm vụ `QM DEMO · Chọn câu hỏi cho hai đề`, chọn **Xem và chọn câu hỏi** ở từng đề. Mỗi ô tự lọc ứng viên đúng bài/mức từ ma trận. Chọn nhiều câu, thêm vào đề, bỏ câu hoặc mở lại để xem dữ liệu đã lưu. Số câu không vượt chỉ tiêu và một câu không được thêm ở ô sai.
9. Mở ma trận `QM DEMO · Ma trận Toán 5` để xem số câu trong ngân hàng tại từng ô và danh sách câu phù hợp ngay tại đó.

Dữ liệu mẫu có một kỳ thi đã kết thúc với lượt làm bài ghi nhận hai câu đã dùng. Hai câu này đã chuyển sang ôn tập; câu thuộc đề dự phòng chưa dùng vẫn ở ngân hàng thi.

## Quy tắc backend

- `WorkTask` loại `QUESTION` là đầu mối gói. Trạng thái: `ASSIGNED → IN_PROGRESS → PENDING_REVIEW → COMPLETED`; trả lại là `NEEDS_REVISION`, nộp lại về `PENDING_REVIEW`.
- Chỉ Giáo viên được giao biên soạn/nộp; chỉ Tổ trưởng giao việc duyệt. Backend kiểm tra người dùng còn hoạt động, quan hệ với nhiệm vụ, phân hiệu và trạng thái.
- Nháp chưa đủ dữ liệu được lưu, nhưng không tính là câu hợp lệ để nộp. Khi nộp, kiểm tra cả phân bố bài/mức và nội dung/đáp án; không nộp một phần.
- Chốt gói và tạo/tìm ngân hàng đúng phân hiệu/môn/khối nằm trong một transaction. Khóa phân hiệu tuần tự hóa tạo ngân hàng cùng scope.
- Câu đã chốt không sửa/xóa qua API soạn câu. Đề đã chốt không sửa/bỏ câu qua API bộ chọn.
- `question_version` và khóa dòng nhiệm vụ bảo vệ ghi đồng thời. Request cũ trả `409`, không ghi đè phiên bản mới. Retry import không nhân đôi dữ liệu.
- Ngân hàng chỉ trả câu `APPROVED`. Role có phân hiệu đọc trong scope đó; Hiệu trưởng có phân hiệu đọc trong trường tương ứng. Hiệu trưởng không gắn phân hiệu dùng phạm vi đọc toàn hệ thống sẵn có của module ma trận.
- Múi giờ ngày hạn là `Asia/Ho_Chi_Minh` (UTC+7). Timestamp lịch sử nộp/duyệt/chuyển được trả dưới dạng UTC có hậu tố `Z`; ngày hạn giữ ngày địa phương.

## Tự chuyển sang ôn tập

`QuestionPracticeWorker` kiểm tra mỗi 30 giây, độc lập với việc mở trang ngân hàng. Nguồn sử dụng là `ExamAttempt → ExamVariant → ExamVariantQuestion → ExamSetQuestion`, thuộc kỳ thi `COMPLETED`.

- Chỉ câu xuất hiện trong lượt làm bài được ghi nhận mới tự chuyển. Việc cấu hình đề chính/dự phòng không tự tạo bằng chứng đã thi.
- Kỳ thi hủy và đề dự phòng chưa dùng không được tính là sử dụng.
- Câu còn trong đề chính/dự phòng của kỳ thi `SCHEDULED` hoặc `ONGOING` được giữ lại. Worker tự kiểm tra lại khi hết ràng buộc, kể cả lượt làm bài được ghi nhận muộn.
- Chuyển giữ nguyên ID, nội dung, đáp án, option ID và liên kết đề/lượt làm bài. Chỉ đổi ngân hàng và metadata chuyển; gọi lại không chuyển trùng.
- PHT chuyển thủ công qua cùng kiểm tra bảo vệ đề. Quyền này kiểm tra role PHT cụ thể, không dùng helper gộp Hiệu trưởng.

Việc chuyển trạng thái kỳ thi sang `COMPLETED` thuộc module thi. Worker không tự kết thúc kỳ thi chỉ vì đã qua lịch. Nếu triển khai thi giấy, nguồn ghi nhận cấp phát/sử dụng đề phải được nối thêm; không suy đoán sử dụng từ đề dự phòng đã chọn.

## API

Các API trả `ApiResponse`; lỗi `403` sai quyền, `404` tài nguyên không truy cập được/không tồn tại, `409` xung đột trạng thái/phiên bản, `422` dữ liệu không hợp lệ.

| Route | Chức năng |
|---|---|
| `GET /api/tasks/{id}/question-package` | Gói, tiến độ, câu nhìn được, lịch sử, version, quyền |
| `POST /api/tasks/{id}/questions` | Lưu câu nháp |
| `PUT /api/tasks/{id}/questions/{questionId}` | Sửa câu nháp |
| `DELETE /api/tasks/{id}/questions/{questionId}?version=...` | Xóa câu nháp |
| `POST /api/tasks/{id}/question-package/submit` | Gửi cả gói |
| `POST /api/tasks/{id}/question-package/approve` | Chốt cả gói |
| `POST /api/tasks/{id}/question-package/reject` | Trả gói và nhận xét |
| `GET /api/tasks/{id}/questions/import/template.xlsx` | Mẫu còn thiếu theo nhiệm vụ |
| `POST /api/tasks/{id}/questions/import/preview?version=...` | Kiểm tra multipart file, chưa lưu |
| `POST /api/tasks/{id}/questions/import?version=...` | Xác nhận nhập nguyên vẹn |
| `GET /api/question-banks/questions` | Danh sách exam/practice, lọc và phân trang |
| `GET /api/question-banks/questions/{id}` | Chi tiết câu đã chốt |
| `GET /api/question-banks/options` | Danh mục bộ lọc trong scope |
| `POST /api/question-banks/questions/{id}/move-to-practice` | PHT chuyển thủ công |
| `GET /api/question-banks/overdue-count` | Số nhiệm vụ câu hỏi quá hạn của Tổ trưởng |
| `GET /api/question-banks/matrices/{id}/availability` | Số câu phù hợp từng ô ma trận |
| `GET/POST /api/tasks/{id}/papers/{paperId}/questions` | Đọc/thêm câu vào đề đang soạn |
| `DELETE /api/tasks/{id}/papers/{paperId}/questions/{questionId}?version=...` | Bỏ câu khỏi đề đang soạn |

Chọn câu tạo/lưu `ExamSet` nháp gắn `ExamTaskPaper.ExamSetId`; chưa thay quy trình duyệt bộ đề, tạo biến thể hay tổ chức thi của module khác. Ma trận tiếp tục quản lý cấu trúc/số lượng, không lưu danh sách câu được chọn.

## Kiểm tra

```powershell
# Backend
dotnet build TeLoSchoolManagement.sln --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet test TeLoSchoolManagement.sln --disable-build-servers -m:1 -p:UseSharedCompilation=false
# Đặt TELO_TEST_MYSQL bằng kết nối MySQL local có quyền tạo database kiểm thử
# để các test tích hợp thực sự chạy, không bị skip.

# Frontend, trong TeLo-FrontEnd/TeLo-Frontend
npm run build
npx eslint src/features/questions src/features/tasks/pages/TaskDetailPage.tsx src/features/tasks/pages/TaskListPage.tsx src/features/matrix/components/MatrixGrid.tsx src/features/matrix/pages/MatrixDetailPage.tsx src/routes/AppRoutes.tsx src/layouts/AppShell.tsx
```

Build Vite có cảnh báo kích thước bundle đã tồn tại từ baseline. Lint toàn repository có 23 lỗi cũ ngoài phần câu hỏi; không thêm suppression hoặc sửa công việc không liên quan để che lỗi. Các file của tính năng đạt lint riêng.

Browser/DevTools không được kết nối trong phiên triển khai, nên chưa kiểm tra trực quan hoặc chụp screenshot. Các luồng được kiểm chứng bằng HTTP/MySQL thật và build/typecheck; cần kiểm tra bằng trình duyệt tại các kích thước màn hình trong checklist QA trước khi phát hành.
