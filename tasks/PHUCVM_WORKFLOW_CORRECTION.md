# Hiệu chỉnh luồng đề thi, module và thông báo — 15/10/2026

Yêu cầu mới của phucvm thay cho đường giao tổ trưởng tự biên soạn đề trước đây. Chỉ sửa phần phucvm trên hai worktree nhánh `phucvm`; không commit/push, không migration/schema update, không tạo seed. Lượt sửa code này không ghi dữ liệu online.

## Phase 1 — PHT sinh đề rồi mới giao review

- PHT chọn ma trận APPROVED, hệ thống tự lấy đủ câu hợp lệ theo từng ô từ ngân hàng thi và tạo đề nháp có snapshot.
- Đường vào từ Nhiệm vụ và Kho đề đều mở màn Tạo đề tự động. URL tạo task đề cũ chuyển đến màn này; API tạo task biên soạn cũ trả conflict có hướng dẫn thay vì tạo placeholder mới.
- Sau khi xem đề đã sinh, PHT giao một giáo viên hoặc tổ trưởng ACTIVE cùng phân hiệu, khác người tạo; chọn hạn review.
- Người nhận chỉnh sửa bằng cách thay câu đúng ô ma trận rồi gửi kết quả. PHT review cuối, trả lại có lý do hoặc chốt và khóa.
- Chỉ PHT tạo/giao đề trong luồng mới. Tổ trưởng là một lựa chọn reviewer, có quyền thay câu và gửi giống giáo viên được giao; không tự chốt.
- Dữ liệu/task biên soạn cũ được giữ để đọc và tiếp tục xử lý tương thích; không xóa lịch sử hoặc sửa schema. Không giới thiệu luồng cũ như cách tạo đề mới.
- Kiểm chứng quyền HTTP bằng test không kết nối DB: non-PHT không sinh đề; create task cũ không tạo dữ liệu và trả đúng conflict. Giữ kiểm tra workflow/version/đúng ô hiện có.

## Phase 2 — Module là phân hệ của ứng dụng

- Module đại diện một phân hệ, ví dụ Thi hoặc Quản lý học tập. Những chức năng như ma trận, ngân hàng câu hỏi, bộ đề, kỳ thi là điều hướng bên trong module phù hợp.
- CRUD danh mục module tiếp tục dùng bảng `module`, quan hệ `module` → `navbar`, trạng thái ACTIVE/INACTIVE và bảo vệ xóa khi còn điều hướng. Không biến một module thành một task, bộ đề hoặc quyền riêng theo trường.
- Làm rõ mô tả/placeholder ở màn Admin. Không xây màn chọn module sau đăng nhập hoặc sửa cơ chế đăng nhập/điều hướng của người khác. Không tự tạo danh mục module thật hoặc thay dữ liệu online trong lượt này.

## Phase 3 — Thông báo và nghiệm thu

- Kết quả thành công/thất bại của thao tác dùng `displayToast` giống đăng nhập: góc phải, tiêu đề Thành công/Lỗi, icon, tiến trình và nút đóng.
- Xác nhận xóa/chốt và form nhập vẫn dùng dialog; lỗi từng trường vẫn nằm tại trường. Không dùng banner tĩnh thay toast kết quả sau lưu/xóa/giao/gửi.
- Rà thao tác tạo đề, giao/đổi reviewer, thay câu, gửi, trả lại, chốt, copy, xóa; thao tác câu hỏi và task câu hỏi thuộc phần phucvm. Không sửa luồng task ma trận của thành viên khác.
- Build BE/FE, test phù hợp không tạo/xóa DB, lint các file sửa, kiểm tra diff/schema/Git. Browser kiểm tra nếu công cụ có sẵn; ghi rõ nếu không thực hiện được.

## Kết quả

Đã thực hiện đủ ba phase trên code, giữ dữ liệu/task cũ và không thay schema hoặc ghi dữ liệu online trong lượt hiệu chỉnh.

| Kiểm chứng | Kết quả |
|---|---|
| Regression trước sửa | 3 case tái hiện lỗi: TEAM_LEAD/TO_TRUONG còn sinh đề và route cũ còn đi vào tạo task; 2 role bị chặn như dự kiến |
| Regression sau sửa và các HTTP contract liên quan | 95 passed, 0 failed/skipped trong filter an toàn đã chạy; gồm 5 case mới |
| Application.Tests | 266 passed, 0 failed/skipped; thêm 3 regression policy để tổ trưởng không giữ quyền quản lý cũ |
| BE solution build | 0 warnings/errors |
| FE build / ESLint 14 file sửa | Đạt; giữ cảnh báo bundle >500 kB, không tăng ngưỡng |
| GET options thật trên TiDB | PHT canCreate=true, tổ trưởng/giáo viên=false; PHT thấy 25 reviewer, có cả tổ trưởng và giáo viên, không gồm chính người tạo |
| Request bị chặn ở runtime | Tổ trưởng/giáo viên POST sinh đề → 403; PHT POST task đề cũ → 409 EXAM_TASK_FLOW_CHANGED |
| Task biên soạn cũ | 3 task mẫu còn nguyên danh sách paper khi GET bằng PHT |
| DB / Git | Không sửa model/mapping/migration hoặc ghi DB trong lượt; không commit/push, giữ nhánh phucvm |

Kết quả thao tác đề được gọi trực tiếp bằng `displayToast`, nên không phụ thuộc việc màn chi tiết còn render khi tải lại sau conflict. Toast tạo đề gọi trước khi chuyển trang như đăng nhập. Các màn Question vẫn dùng cầu nối NoticeDialog → toast hiện có; đã bổ sung message còn thiếu khi lưu câu và chọn/bỏ câu ở task cũ. Task list không còn dùng banner để báo kết quả xóa; task câu hỏi có toast tạo/sửa; Excel có toast tải mẫu, kiểm tra, nhập thành công và lỗi.

Module chỉ thay nội dung giải thích/placeholder trong màn Admin cho đúng phân hệ. Giữ CRUD, trạng thái, navbar count và delete gate hiện có. Không sửa `ModuleService`, bảng/cột, API auth/login hoặc code màn chọn module của nhóm. API quản trị `/api/modules` vẫn theo quyền Admin; người làm màn chọn module cần contract đọc phù hợp cho người dùng đăng nhập, không mở toàn bộ quyền quản trị.

Browser tool trả `apps=[]`, `browsers=[]`: chưa nghiệm thu trực quan toast/modal, responsive hoặc in đề. Cần thử UI với PHT: **Nhiệm vụ → Thêm nhiệm vụ → Tạo đề tự động**, hoặc **Kho đề → Tạo đề tự động**; chọn ma trận, sinh đề, xem lại, giao review cho giáo viên/tổ trưởng, gửi lại, rồi PHT trả lại/chốt. Luồng mutation hoàn chỉnh đã được kiểm chứng ở lượt tạo dataset trước; lượt này kiểm tra lại quyền/options và từ chối đường cũ, không chạy lại các mutation trên TiDB.
