# Dữ liệu thử online cho phucvm — 15/10/2026

## 1. Phạm vi và nơi lưu

Dữ liệu đã được ghi trực tiếp vào **TiDB `sep_490`**, theo yêu cầu cho phép thêm dữ liệu thử của phucvm. Đây là dữ liệu dùng chung trên server, không phải MySQL localhost. Không chạy migration, không thêm/sửa bảng hoặc cột, không tạo file seed, không commit hoặc push.

Các bản ghi phục vụ Academic Year, Role Management, Module Management, Question và Exam Package. Trường/phân hiệu, giáo viên, chương/bài, ma trận và một số liên kết kỳ thi/kết quả là dữ liệu phụ thuộc để các phần này chạy được. Không triển khai hoặc sửa nghiệp vụ kỳ thi, giáo viên, email hay import của thành viên khác.

- Batch: `PHUCVM_TEST_20261015`.
- Tên dữ liệu thường có `[TEST PHUCVM]`; tài khoản có tiền tố `phucvm_test_`; mã danh mục thường có `PVM_QA_`.
- [manifest.json](manifest.json) lưu ID chính xác, trạng thái, kịch bản và kết quả kiểm tra. File này **không phải script tạo dữ liệu**, không có mật khẩu, hash đăng nhập hay nội dung câu hỏi/snapshot.
- Các bảng nghiệp vụ trống tại thời điểm kiểm tra ban đầu; bảng lịch sử migration đã có một dòng. Baseline nằm trong manifest. Không dựa vào baseline để xóa toàn bộ bảng vì thành viên khác có thể thêm dữ liệu sau đó.
- Không bật worker tự chuyển câu sang ôn tập. `QuestionPractice:Enabled=false` được cấu hình cục bộ bằng User Secrets. Email và nguồn lịch sử import giáo viên theo năm vẫn hoãn.

## 2. Chạy ứng dụng

Tại thư mục `BE-Vanh`, mở hai terminal:

```powershell
dotnet run --project .\TeLo-Backend-phucvm\WebAPI --launch-profile http
```

```powershell
npm --prefix .\TeLo-Frontend-phucvm run dev -- --host localhost --port 5173 --strictPort
```

Mở `http://localhost:5173`. API là `http://localhost:5035/api`. Kết nối TiDB đã nằm trong User Secrets của WebAPI; FE dùng `.env.local` bị gitignore. Không cần seed hoặc `database update` khi chạy. Xem quy định DB chung ở [DB_V2.md](../../DB_V2.md).

## 3. Tài khoản

Mật khẩu chung của các tài khoản thử đã được cung cấp trong hội thoại; không ghi mật khẩu vào tài liệu trong repo. Các tài khoản bị khóa/ngừng hoạt động dùng cùng mật khẩu, nhưng việc đăng nhập phải bị từ chối.

| Tài khoản | Vai trò / phạm vi | Cách dùng |
|---|---|---|
| `phucvm_test_admin` | Admin hệ thống, không gắn phân hiệu | Năm học, vai trò toàn hệ thống, module |
| `phucvm_test_pht` | PHT phân hiệu chính | Sinh đề, giao review, trả lại, chốt, copy ôn tập |
| `phucvm_test_pht_2` | PHT thứ hai cùng phân hiệu | Kiểm tra quyền PHT và hai phiên thao tác |
| `phucvm_test_to_truong` | Tổ trưởng phân hiệu chính | Giao/duyệt câu hỏi, soạn đề theo task |
| `phucvm_test_hieu_truong` | Hiệu trưởng trường chính | Kiểm tra quyền xem theo trường |
| `phucvm_test_gv_01` … `phucvm_test_gv_24` | Giáo viên ACTIVE phân hiệu chính | Soạn câu hỏi, nhận review; giáo viên 01–04 có nhiều mẫu đang xử lý |
| `phucvm_test_tt_ve_tinh` | Tổ trưởng phân hiệu vệ tinh, cùng trường | Kiểm tra phạm vi phân hiệu |
| `phucvm_test_pht_ve_tinh` | PHT phân hiệu vệ tinh | Kiểm tra phân tách ngân hàng/đề giữa phân hiệu |
| `phucvm_test_gv_25` … `phucvm_test_gv_28` | Giáo viên ACTIVE phân hiệu vệ tinh | Ngân hàng riêng, không nhận đề của phân hiệu chính |
| `phucvm_test_tt_khac_truong` | Tổ trưởng trường khác | Kiểm tra phân tách trường |
| `phucvm_test_pht_khac_truong` | PHT trường khác | Không xem/sửa đề của trường chính |
| `phucvm_test_gv_29` | Giáo viên ACTIVE trường khác | Có gói câu hỏi đã duyệt ở trường khác |
| `phucvm_test_gv_30` | User ACTIVE, phân hiệu INACTIVE | Đăng nhập bị chặn vì phân hiệu ngừng hoạt động |
| `phucvm_test_gv_31`, `phucvm_test_gv_33` | User LOCKED | Đăng nhập bị chặn |
| `phucvm_test_gv_32`, `phucvm_test_gv_34` | User INACTIVE | Đăng nhập bị chặn |
| `phucvm_test_hoc_sinh` | Học sinh phân hiệu chính | Kiểm tra không có quyền quản lý đề; có một bài nộp giả lập |

Có 44 user, 59 lượt gán vai trò và 42 hồ sơ giáo viên. Hồ sơ dùng thông tin giả, email miền `example.test`, mã định danh riêng, số điện thoại, ngày sinh, chuyên môn, giới tính và avatar. Không gửi email đến các địa chỉ này. `user` có 40 ACTIVE, 2 LOCKED, 2 INACTIVE; một user ACTIVE nằm ở phân hiệu INACTIVE nên cũng không đăng nhập được.

## 4. Số lượng và trạng thái đã tạo

Số liệu dưới đây là lúc hoàn tất batch; thao tác thử sau này sẽ làm số liệu thay đổi.

| Nhóm | Số lượng | Các trường hợp |
|---|---:|---|
| Năm học | 26 | 1 ACTIVE, 10 CLOSED, 15 DRAFT |
| Vai trò | 36 | 6 nhóm bắt buộc và 30 vai trò thử; tổng 26 ACTIVE, 10 INACTIVE |
| Module | 35 | 23 ACTIVE, 12 INACTIVE; 3 module có navbar, còn lại có mẫu được xóa |
| Navbar | 6 | 3 mục cha, 3 mục con, gắn module 1–3 |
| Trường / phân hiệu | 3 / 5 | Cùng trường, khác trường, trường/phân hiệu ngừng hoạt động |
| Môn / khối | 5 / 6 | Môn Toán, Tiếng Việt, Tiếng Anh, Khoa học và môn INACTIVE; khối 1–5 ACTIVE, khối 6 INACTIVE |
| Chương / bài | 22 / 66 | Ngân hàng chính ở Toán/Tiếng Việt/Tiếng Anh khối 3–5; có dữ liệu phân hiệu khác |
| Ma trận / ô | 19 / 111 | 10 APPROVED, 3 DRAFT, 3 SUBMITTED, 3 ARCHIVED |
| Task câu hỏi | 34 | 6 ASSIGNED, 6 IN_PROGRESS, 5 PENDING_REVIEW, 4 NEEDS_REVISION, 13 COMPLETED |
| Nhóm bài / chỉ tiêu câu hỏi | 110 / 330 | Ba mức nhận thức cho mỗi bài được giao |
| Câu hỏi / dòng đáp án | 1.596 / 6.384 | 1.452 APPROVED, 60 SUBMITTED, 84 DRAFT |
| Bộ đề | 50 | 14 DRAFT, 10 IN_REVIEW, 7 REVIEWED, 19 APPROVED; gồm 5 bản ôn tập |
| Liên kết câu hỏi trong đề | 868 | Đề đủ có 18 câu; có mẫu đang soạn 4 câu và mẫu chưa tạo câu |
| Task tổng cộng / dòng history | 85 / 384 | Gồm task câu hỏi, biên soạn đề, review và lưu trạng thái bộ đề |
| Kỳ thi / liên kết đề | 5 / 6 | Nháp, sắp thi, đang thi, hoàn tất, hủy; kỳ hoàn tất có thêm đề dự phòng |
| Mã đề / bài nộp / chi tiết bài nộp | 2 / 1 / 18 | Mã chính đã dùng, mã dự phòng chưa có bài; bài nộp 16 đúng, 2 sai, tổng 9 điểm |

Kho đề trên UI có **49 đề có câu**: 13 nháp, 10 đang review, 7 chờ chốt, 19 đã chốt; lọc mục đích có 44 đề thi và 5 đề ôn tập. Một placeholder 0 câu chỉ hiện trong task biên soạn nên DB có 50 bộ đề. Không coi chênh lệch này là mất dữ liệu.

Ngân hàng PHT phân hiệu chính có **1.227 câu thi đã duyệt và 81 câu ôn tập**. Toàn DB có 1.371 câu thi APPROVED và 81 câu practice APPROVED; phần chênh là ngân hàng của phân hiệu khác. Câu SUBMITTED/DRAFT không nằm trong ngân hàng đã duyệt. Câu nháp có thể có dòng đáp án rỗng; 18 câu nháp cố ý chưa chọn đáp án đúng.

## 5. Năm học, vai trò, module

### Năm học

- Năm đang áp dụng: **2026-2027**. Năm 2016-2017 đến 2025-2026 đã đóng; 2027-2028 đến 2041-2042 là nháp.
- Dùng Admin tại `/academic-years` để thử lọc, tìm kiếm, phân trang, thêm/sửa/xóa nháp và chặn xóa năm có liên kết.
- Giao diện quản lý chỉ dùng năm và trạng thái. Có **một dòng semester phụ thuộc** vì schema `exam.semester_id` bắt buộc cho 5 kỳ thi giả lập; dòng này không khôi phục chức năng quản lý học kỳ.
- Đổi năm ACTIVE sẽ ảnh hưởng cả nhóm vì đây là DB chung. Sau khi thử, chọn lại 2026-2027 nếu muốn giữ bối cảnh ban đầu.

### Vai trò

- Mã 6 nhóm bắt buộc: `ADMIN`, `TEACHER`, `TEAM_LEAD`, `HIEU_TRUONG`, `STUDENT`, `PHT`. Tất cả ACTIVE và không được xóa.
- Vai trò thử: `PVM_QA_ROLE_01` đến `PVM_QA_ROLE_30`; có vai trò đã gán và chưa gán để thử chặn xóa/cho phép xóa. Vai trò là toàn hệ thống, không theo phân hiệu.
- Vào `/roles` bằng Admin. Thử gán/bỏ gán trên tài khoản thử; thay trạng thái role có thể làm phiên đăng nhập hiện tại bị thu hồi theo security version.
- Không có ô mô tả vai trò; không dùng cột khác để cất mô tả.

### Module

- Vào `/modules` bằng Admin. Module ID 1–3 có navbar nên xóa phải bị chặn. Các module khác có mẫu ACTIVE/INACTIVE để thử sửa trạng thái và xóa.
- Module có tên, mô tả, ảnh và trạng thái; form không có ô mã module.
- 35 dòng hiện có là dữ liệu giả lập để thử CRUD/phân trang, không phải danh mục 35 phân hệ thật của sản phẩm. Module thực tế là một phân hệ, ví dụ Thi; không coi mỗi task/câu hỏi/bộ đề là một module. Màn chọn module sau đăng nhập do người khác làm. Lượt hiệu chỉnh này không đổi danh mục online.

## 6. Câu hỏi — các mẫu để thao tác ngay

Vào `/matrix-tasks` để mở task; URL trực tiếp gói câu hỏi là `/matrix-tasks/{taskId}/questions`. Người duyệt là tổ trưởng đã giao; người soạn là giáo viên được giao. ID task và ID câu hỏi/bộ đề là các loại ID riêng, không dùng lẫn.

| Task ID | Tình huống | Tài khoản soạn / tài khoản xem duyệt |
|---|---|---|
| 1–9 | Ngân hàng chính đã duyệt, 144 câu/task, nhiều câu thay thế mỗi ô | `gv_01` đến `gv_09` / tổ trưởng chính |
| 10 | Ngân hàng Toán khối 5 ở phân hiệu vệ tinh, 72 câu | `gv_25` / `tt_ve_tinh` |
| 11 | Ngân hàng Tiếng Anh khối 5 ở trường khác, 72 câu | `gv_29` / `tt_khac_truong` |
| 12–17 | Đã giao, chưa có câu; còn sửa task được | `gv_01` đến `gv_06` / tổ trưởng chính |
| 18–23 | Đang soạn, chưa đủ quota; có nháp chưa đủ đáp án | `gv_01` đến `gv_06` / tổ trưởng chính |
| 24–27 | Đã gửi, chờ tổ trưởng duyệt | `gv_01` đến `gv_04` / tổ trưởng chính |
| 28–31 | Đã bị trả lại, có nhận xét chung và nhận xét từng câu | `gv_01` đến `gv_04` / tổ trưởng chính |
| 32 | Đã trả lại rồi gửi lại; có 2 bản nộp để đối chiếu snapshot | `gv_01` / tổ trưởng chính |
| 33 | Câu đã duyệt theo quy tắc 2 lựa chọn | `gv_01` / tổ trưởng chính |
| 34 | Câu đã duyệt theo quy tắc 3 lựa chọn | `gv_02` / tổ trưởng chính |

`gv_01` trong bảng là `phucvm_test_gv_01`; các tên rút gọn còn lại cũng có tiền tố `phucvm_test_`.

Các mẫu task 33–34 vẫn xem được trong ngân hàng theo quy tắc soạn/duyệt cũ 2–4 lựa chọn; **picker đề trắc nghiệm 4 lựa chọn phải loại các câu này**. Dòng đáp án chưa dùng được để rỗng, không được coi là phương án hợp lệ.

Luồng thử đề xuất:

1. Dùng giáo viên 01 mở task 18, thử gửi khi thiếu quota; hệ thống phải báo lỗi. Soạn tiếp hoặc thử import Excel trên task chưa có câu. Không dùng task đã APPROVED để thử nhập mới.
2. Dùng tổ trưởng mở task 24 để duyệt hoặc trả lại có nhận xét. Mở task 32 để so bản nộp trước/sau khi gửi lại.
3. Dùng giáo viên 01 mở task 28 để sửa và gửi lại; sau đó tổ trưởng duyệt. Snapshot bản nộp cũ phải giữ nguyên.
4. Vào `/question-bank`, thử ngân hàng thi/ôn tập, môn, khối, chương, bài, mức nhận thức, tìm kiếm và phân trang. 81 câu ôn tập đã có lý do/thời điểm chuyển bằng API thật.
5. Tổ trưởng chính có **4 task câu hỏi quá hạn** lúc tạo dữ liệu. ID cụ thể ở `overdueQuestionTasks` trong manifest; thời gian quá hạn thay đổi theo ngày test.

Ma trận 1–9 là ma trận APPROVED đủ câu, 18 câu/đề, 10 điểm và 9 ô (3 bài × 3 mức nhận thức). Ma trận **19** cố ý thiếu câu so với quota 20/ô để thử rollback khi sinh đề. DRAFT/SUBMITTED/ARCHIVED có ID và tên trong `lookup.matrix` của manifest; ma trận nháp cố ý có cấu hình chưa hoàn tất.

## 7. Bộ đề — review, chốt, bản ôn tập

Vào `/exam-packages`; chi tiết là `/exam-packages/{id}`, xem/in là `/exam-packages/{id}/preview`. Có thể tìm theo tên `[TEST PHUCVM]` và lọc trạng thái/mục đích/môn/khối/năm.

| Bộ đề ID | Tình huống | Thao tác thử |
|---|---|---|
| 2–13 | Nháp tạo tự động; có mẫu đã đổi tên | PHT xem/in, đổi tên, giao review; xóa nháp chưa sử dụng |
| 14–19 | Đang review | Giáo viên được giao thay câu đúng ô rồi gửi PHT |
| 14 | Đã giao lại từ `gv_03` sang `gv_01` | `gv_03` không còn xem được; `gv_01` có quyền thay câu |
| 20–22 | PHT đã trả lại cho reviewer | Xem lý do, sửa, gửi lại; native bộ đề vẫn IN_REVIEW, task là NEEDS_REVISION |
| 23–28 | Giáo viên đã gửi, chờ PHT chốt | PHT trả lại hoặc chốt; giáo viên không tự chốt được |
| 1, 29–37 | Đã được PHT chốt | Nội dung bị khóa; xem snapshot, preview/in, thử điều kiện copy ôn tập |
| 38–42 | 5 bản sao ôn tập từ đề 1, 29, 30, 31, 32 | Đối chiếu câu hỏi/đáp án/thời lượng; nguồn giữ nguyên và không copy lần hai |
| 30008 | Đề dự phòng đã chốt của kỳ hoàn tất | Có mã đề nhưng chưa có bài nộp |

Reviewer cụ thể của từng đề nằm trong `packageJobs[].reviewer`. Đề 20 là `gv_01`, đề 21 là `gv_02`, đề 22 là `gv_03`. Có 2 task review quá hạn trong `overdueReviewTasks`.

Luồng thử đầy đủ:

1. Đăng nhập PHT, chọn một ma trận đủ câu ở màn tạo tự động. Kiểm tra 18 câu, đúng 9 ô và tổng 10 điểm. Dùng ma trận 19 phải báo thiếu và không để lại đề/task rác.
2. PHT giao một giáo viên cùng phân hiệu, khác người tạo. Đăng xuất và vào đúng tài khoản reviewer.
3. Reviewer chọn thay câu. Danh sách phải lấy từ ngân hàng thi, đúng bài, mức nhận thức, loại câu và phạm vi của ô; không lấy câu practice, chưa duyệt, khác ô hoặc câu đã có trong đề.
4. Reviewer gửi review; PHT có thể trả lại với lý do bắt buộc hoặc chốt cuối. Giáo viên chỉ gửi kết quả review. Sau chốt, không sửa câu hoặc giao review lại.
5. Mở cùng một đề ở hai tab; thao tác tab cũ sau khi tab kia đã lưu phải nhận conflict và tải lại phiên bản. Không dựa vào revision ghi trong manifest vì mỗi lần thao tác sẽ đổi revision.
6. Đề chưa gắn kỳ thi và chưa copy có thể tạo bản ôn tập sau chốt. Đề đã có bản ôn tập phải chặn tạo trùng.

### Task biên soạn đề trước khi chuyển qua review

Các task dưới đây là dữ liệu tương thích luồng cũ, đã có trước hiệu chỉnh. Không dùng làm mẫu tạo task đề mới. Với luồng hiện tại, PHT mở Tạo đề tự động rồi giao review từ chi tiết đề; URL form tạo task cũ chuyển đến màn tự động và API tạo task cũ trả 409 `EXAM_TASK_FLOW_CHANGED`. Xem [hiệu chỉnh](../PHUCVM_WORKFLOW_CORRECTION.md).

| Task ID | Các paper ID cố định | Trạng thái tổng hợp và mục đích |
|---|---|---|
| 30001 | 30001 đang review; 30002 đang chọn 4 câu; 30003 chưa có câu | IN_PROGRESS: thử soạn tiếp, placeholder và giữ đề trong task gốc |
| 30003 | 30004 đã chốt; 30005 chờ PHT chốt | PENDING_REVIEW: chỉ còn một đề chưa chốt |
| 30006 | 30006 và 30007 đều đã chốt | COMPLETED: tất cả sản phẩm đã hoàn tất |

PHT giao, tổ trưởng chính biên soạn bằng picker. Mở `/matrix-tasks/{taskId}`; đường dẫn chọn câu là `/matrix-tasks/{taskId}/papers/{paperId}/questions`. Paper ID vẫn giữ nguyên khi `exam_set.task_id` chuyển sang task review. Đã xác minh API đọc lại task gốc còn đủ danh sách, scope và trạng thái tổng hợp đúng.

### Đề đã gắn với kỳ thi

| Kỳ thi ID | Trạng thái | Bộ đề chính | Kỳ vọng |
|---|---|---|---|
| 1 | DRAFT | 33 | Chặn copy đề sang ôn tập vì kỳ chưa kết thúc |
| 2 | SCHEDULED | 34 | Chặn copy đề và chuyển câu đang được giữ sang ôn tập |
| 3 | ONGOING | 35 | Chặn copy đề và chuyển câu đang được giữ sang ôn tập |
| 4 | COMPLETED | 36; dự phòng 30008 | Đề chính có variant 1 và result 1; dự phòng có variant 2, không có result |
| 5 | CANCELLED | 37 | Không còn điều kiện giữ do kỳ thi chưa kết thúc |

Đây là fixture phụ thuộc để kiểm tra Question/Exam Package; endpoint quản lý kỳ thi của nhánh DB v2 vẫn thuộc phạm vi thành viên khác và có thể đang khóa 501. Không cần mở chức năng đó để test kho đề.

Worker đang tắt nên việc kỳ thi 4 có kết quả **không tự chuyển câu** trong lượt này. Dữ liệu đã chuẩn bị để nghiệm thu reconciler sau: chỉ câu thật trong mã đã có bài làm được xét; câu chỉ có ở mã dự phòng không được tự chuyển; câu còn bị kỳ khác giữ cũng không chuyển. Không bật worker trên DB chung khi chưa thống nhất thao tác thử riêng.

## 8. Kiểm chứng đã thực hiện

- Đăng nhập thật qua API cho 8 tài khoản ACTIVE đại diện các vai trò/phạm vi; thử 5 tài khoản bị chặn do user/phân hiệu.
- Tạo/gửi/duyệt/chuyển ôn tập câu hỏi bằng service thật; có snapshot, nhận xét và revision trong `task_history.content` theo contract v2.
- Sinh đề, giao review, thay câu, gửi, trả lại, PHT chốt, copy ôn tập và đổi người review đã chạy với persistence TiDB thật.
- 29 mục kiểm tra runtime được lưu trong `runtimeChecks`, gồm bảo vệ 6 vai trò, module có navbar, quyền khác giáo viên/phân hiệu/trường, học sinh, revision cũ, thiếu câu rollback, sai ô, quyền chốt, khóa nội dung, chống copy trùng và kỳ thi đang sử dụng.
- List/filter/pagination API có dữ liệu ở mọi trạng thái nêu trên. Cảnh báo task câu hỏi quá hạn trả 4.
- Không có câu nào thiếu 4 dòng answer; câu SUBMITTED/APPROVED đều có đúng một cờ đáp án đúng. 18 câu nháp chưa chọn đáp án là chủ ý để test validation.
- Không có câu lệch bài so với task; không có câu lệch phạm vi ma trận trong bộ đề; không trùng question ID trong một bộ đề. Bài nộp có 18 dòng chi tiết, tổng điểm 9, mã dự phòng có 0 bài nộp.
- Schema giữ 53 bảng gồm lịch sử EF, một migration InitialCreate; không áp dụng migration mới. Sổ metadata lưu hash danh sách cột ở thời điểm chốt batch.
- Khi chạy ghi thật đã phát hiện và sửa 3 câu SQL khóa trong phần Question/Exam Package: dùng bảng/cột native `branch.branch_id` thay cho tên cũ `school_branch.school_branch_id`. Solution build đạt và Application.Tests 263 test đạt sau sửa. Không đổi schema hoặc logic của người khác.

Các kiểm tra trên là API và tính nhất quán dữ liệu. **Chưa nghiệm thu trực quan popup, dấu sao đỏ, toast, responsive hoặc bản in bằng browser thật** vì công cụ browser không khả dụng. Cần người dùng kiểm tra các màn này với dữ liệu mới; không suy từ API/build thành giao diện đã đạt.

## 9. Xóa dữ liệu sau khi thử

Chưa xóa bản ghi nào của batch. Không có file seed hoặc file SQL chèn dữ liệu. Để dọn sau này, dùng `tables`, `compositeKeys` và các trường kịch bản trong manifest làm danh sách giới hạn.

Trước khi xóa cần đọc lại FK, đối chiếu ID còn thuộc batch và kiểm tra bản ghi mới của nhóm đang tham chiếu. Tên có marker chỉ hỗ trợ nhận diện, **không đủ để xóa bằng LIKE hoặc TRUNCATE**. Các role bắt buộc và năm ACTIVE có thể đã được người khác dùng sau khi tạo; không xóa khi còn phụ thuộc ngoài batch. Nếu người dùng tạo thêm câu/đề/task trong lúc test, phải cập nhật danh sách con trước khi dọn; manifest không tự theo dõi thao tác mới.

Thứ tự phụ thuộc dự kiến, chỉ thực hiện khi có yêu cầu dọn riêng:

1. Chi tiết kết quả → kết quả → variant; liên kết `exam_subject` → kỳ thi. Kiểm tra thêm incident/violation nếu nhóm đã tạo trong lúc thử.
2. Liên kết câu trong đề → bộ đề → chi tiết ma trận → ma trận. Xóa lịch sử chỉ sau khi không còn cần đọc journal để xác định quan hệ.
3. Answer → question → task_history → question_task_detail → question_task → task. Kiểm tra ma trận/bộ đề/task mới không còn giữ các ID này.
4. Student/teacher profile → user_roles → user. Kiểm tra actor/reviewer/creator trong các bảng khác trước khi xóa user.
5. Navbar con → navbar cha → module; lesson → chapter → branch → school. Kiểm tra permission/teacher_assignment/class và mọi dữ liệu mới của nhóm.
6. Semester phụ thuộc → academic_year; các subject/grade/role thử chỉ xóa khi không còn tham chiếu.

Không DROP database, không tắt FK, không xóa migration history. Mọi bước dọn cần giới hạn bằng exact ID, kiểm tra số dòng trước/sau và ghi lại kết quả. Các câu lệnh xóa chưa được tạo/chạy trong lượt thêm dữ liệu này.
