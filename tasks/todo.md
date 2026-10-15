# Phucvm DB v2 — theo dõi

Theo phase và acceptance tại [plan](plan.md). Không commit/push hoặc migration. Dữ liệu online đã thêm khi người dùng cho phép ở lượt riêng; lượt hiệu chỉnh code hiện tại không ghi DB.

- [x] Main mới đã có DB v2 và TiDB transaction compatibility.
- [x] Dùng nhánh phucvm có sẵn cho cả BE/FE; giữ nguyên hai working tree cũ.
- [x] Người dùng đồng ý journal trong task_history.content; DB sep_490.
- [x] Test RED/GREEN năm học không học kỳ; domain/service và FE năm-only.
- [x] Role toàn hệ thống và bảo vệ vai trò bắt buộc; module không mã; bỏ mô tả role theo xác nhận.
- [x] Journal schema 1, nén/chia mảnh/checksum/revision và snapshot; test hồi quy thuần.
- [x] Port Question workflow/import/bank/picker; recheck quyền sau khóa; worker ghi DB mặc định tắt.
- [x] Port Exam Package workflow, rule/policy/contract test; task review tách riêng, đồng bộ task biên soạn cũ.
- Hoãn lọc giáo viên theo import năm học theo yêu cầu người dùng; chờ dữ liệu lịch sử từ nhóm.
- [x] Đối chiếu UI source cũ; build BE/FE; lint các file TS/TSX đã đổi; self-check liên quan.
- [x] Unit/model/HTTP contract an toàn; không chạy test tự tạo/xóa database.
- [x] Tài liệu triển khai chi tiết và checklist nghiệm thu: [triển khai DB v2](implementation.md).
- [x] Dữ liệu thử và workflow persistence TiDB sau khi được phép: [hướng dẫn](online-test-data-20261015/README.md).
- [x] Hiệu chỉnh PHT sinh đề trước/giao review sau; module là phân hệ; bổ sung toast thao tác: [kết quả](PHUCVM_WORKFLOW_CORRECTION.md).
- [ ] Nghiệm thu concurrency/race với các module khác trên môi trường được chấp thuận.
- [ ] Khi browser khả dụng: nghiệm thu popup/toast/dấu * đỏ, responsive, preview/in và smoke-test component chung.
- Email xử lý riêng sau; không tự mở khóa.
