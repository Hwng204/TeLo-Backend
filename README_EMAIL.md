# Quản lý email và thông báo theo trường

## Điều chỉnh phạm vi theo yêu cầu người dùng

Đã bỏ nút **Thông báo** trên các layout và hộp thông báo cá nhân `/notifications`, cùng API/DTO riêng của inbox. Phần này không cần thiết cho phạm vi quản lý email hiện tại. Các mô tả inbox ở mốc triển khai bên dưới chỉ còn là lịch sử, không áp dụng cho bản hiện tại.

Kiểm tra sau khi gỡ: build FE và lint phạm vi sửa đạt; 9/9 kiểm thử tích hợp email đạt, không skip, dùng database kiểm thử riêng và SMTP giả lập.

Giữ nguyên quản lý sự kiện, mẫu email, cấu hình người nhận, gửi ngay/đặt lịch và lịch sử gửi theo trường. Không xóa dữ liệu lịch sử hay thay đổi schema DB. Email thủ công mới không tự chèn đường dẫn đến inbox đã bỏ; các liên kết nghiệp vụ ma trận vẫn được giữ. Nội dung các email đã xếp hàng trước thay đổi này là bản chụp lịch sử nên không bị viết lại.

## Bản triển khai cập nhật — 07/10/2026

Mục này mô tả code hiện tại và thay thế các nhận định về phần chưa triển khai trong những mốc review/lịch sử bên dưới. Giữ cấu trúc Controller → Application service/DTO → EF/entities hiện có; FE tiếp tục dùng `features/emails`, PCB và API tập trung tại `src/services/api.ts`. Không commit/push.

| Phase | Kết quả triển khai |
| --- | --- |
| 1. Cấu hình và quyền | Có danh sách cấu hình phân trang, tìm theo sự kiện, lọc loại sự kiện và Chưa cấu hình/Đang bật/Đang tắt. Quyền quản lý toàn trường không được suy ra từ role chỉ có phạm vi phân hiệu. |
| 2. Khôi phục giao diện | URL được ưu tiên; nhớ trường/tab/sự kiện theo tài khoản và riêng từng trường trong phiên trình duyệt. Đăng xuất dọn ngữ cảnh. PUT thành công giữ response đã lưu trong lúc GET tải lại hoặc gặp lỗi, không biến thành form mới. |
| 3. Sự kiện, mẫu, gửi | Sự kiện MANUAL theo trường; từ dòng sự kiện mở đúng mẫu/cấu hình. Tạo mẫu kế thừa sự kiện đang chọn. Mẫu có phiên bản; cấu hình ghim phiên bản. Gửi ngay/đặt lịch một lần, xem trước, lưu lịch sử và hủy trước khi worker bắt đầu gửi. |
| 4. Thông báo cá nhân | `/notifications`: tìm kiếm, lọc đã đọc/chưa đọc, phân trang, chi tiết và đánh dấu đã đọc. Chỉ người nhận xem được; lịch tương lai, bản hủy và email thử không xuất hiện. Admin không tự có quyền đọc hộp thông báo của người khác. |
| 5. Kiểm thử | MySQL riêng + SMTP giả lập; chi tiết kết quả và phần chưa nghiệm thu trực quan bên dưới. |

### Cách sử dụng bản cập nhật

1. Admin mở **Quản lý email**, chọn trường. Nếu có nhiều trường thì phải chọn rõ; không tự mở cấu hình của trường đầu danh sách.
2. Với thông báo riêng như họp chuyên môn: vào **Sự kiện → Thêm sự kiện**, khai báo biến cần nhập (TEXT/DATE/NUMBER/URL). Bấm **Mẫu email** tại dòng sự kiện để tạo nội dung dùng đúng bộ biến.
3. Bấm **Cấu hình**: chọn phiên bản mẫu; chọn nhóm vai trò + cá nhân thêm − nhóm/cá nhân loại trừ, xem trước và lưu. Mở lại từ danh sách hoặc sidebar sẽ đọc bản đã lưu từ BE. Trường có nhiều phân hiệu dùng chung cấu hình cấp trường.
4. Vào **Gửi thông báo** với sự kiện MANUAL đang áp dụng: nhập giá trị biến, chọn gửi ngay hoặc lịch giờ Việt Nam, xem trước rồi xác nhận. Theo dõi tại **Lịch sử gửi**. Sự kiện SYSTEM tự phát sinh khi nghiệp vụ đã được tích hợp; Admin tạo sự kiện không tự tạo thêm trigger nghiệp vụ trong code.
5. Người nhận mở **Thông báo** trong khung giao diện đang dùng, đọc nội dung và bấm **Đánh dấu đã đọc**. Trạng thái đọc độc lập với kết quả SMTP; email lỗi không làm mất thông báo cá nhân đã đến hạn.

### Contract và các quyết định triển khai

- `GET /api/schools/{schoolId}/emails/configurations`: nhận `page`, `pageSize`, `search`, `configurationState` và `triggerKind`; số nhóm/cá nhân đã chọn khác với số địa chỉ thực nhận. Mỗi trang tối đa 100 dòng.
- Hộp cá nhân: `GET /api/notifications`, `GET /api/notifications/unread-count`, `GET /api/notifications/{id}`, `PUT /api/notifications/{id}/read`. `id` là ID bản ghi người nhận, không phải ID lần gửi. GET chi tiết không tự đánh dấu đã đọc; PUT lặp giữ thời điểm đọc đầu tiên. ID không thuộc người gọi trả 404.
- Dùng lại `NotificationRecipient.ReadAt`, không thêm bảng inbox. Mỗi tài khoản đủ điều kiện có bản thông báo riêng; địa chỉ email trùng chỉ gửi một lần và ghi lý do `DUPLICATE_EMAIL` cho bản bỏ qua. Không tái dựng người nhận đã bị bỏ qua trong lịch sử cũ.
- Hộp thông báo hiện dùng cùng nội dung/người nhận với lần gửi email; chưa có công tắc kênh độc lập. Gửi thủ công yêu cầu dịch vụ email được bật. Sự kiện tự động vẫn lưu hàng đợi khi SMTP tắt; worker xử lý khi được bật lại. Ngừng mẫu/sự kiện ngăn enqueue mới, không tự hủy lịch đã tạo.
- Khóa sự kiện ma trận gắn với transaction nghiệp vụ: gọi queue lặp trong cùng transaction không tạo bản trùng; rollback hủy cả queue. Retry API duyệt sau khi đã duyệt bị chặn bởi trạng thái. Không tuyên bố đây là ID nghiệp vụ bền vững cho cơ chế phát lại sự kiện từ hệ thống ngoài; nếu bổ sung cơ chế đó cần ID lần chuyển trạng thái được lưu trong DB. SMTP vẫn có giới hạn at-least-once đã mô tả bên dưới.
- Để chặn tab/sidebar/Back khi cấu hình hoặc nội dung gửi còn sửa, điểm khởi tạo router chuyển sang `createBrowserRouter`/`RouterProvider`, giữ cây route và guard hiện có. Không vá `window.history`. Nút tải lại nội dung gửi có xác nhận; gửi thành công xóa trạng thái chưa lưu trước khi mở lịch sử.
- Hai component mới cho inbox nằm đúng `features/emails/pages` và `features/emails/components`; các layout chỉ gắn điểm truy cập dùng chung. Học sinh mở inbox bằng StudentLayout, Admin bằng AdminLayout, các vai trò còn lại bằng AppShell.

### Kết quả kiểm tra và phần cần nghiệm thu trên máy chạy

- Hồi quy solution BE: **133 Application + 21 Infrastructure + 24 WebAPI = 178 test đạt, không skip**. Sau đó bổ sung một kiểm thử HTTP duyệt ma trận, rollback queue và chống lặp: **1/1 đạt**.
- Sau khi đổi liên kết thông báo thủ công sang `/notifications`, chạy lại kiểm thử gửi/đặt lịch/hủy và đọc inbox người nhận: **1/1 đạt**. Email thủ công không dẫn người nhận vào trang quản trị email.
- FE: **27 self-check đạt**, gồm ưu tiên URL, nhớ ngữ cảnh theo trường/tài khoản, validate mẫu/biến, lịch giờ Việt Nam và liên kết an toàn. ESLint phạm vi email/router/layout đã sửa và production build đạt.
- EF `has-pending-model-changes`: không có thay đổi model chưa có migration. Đợt hoàn thiện này không cần migration mới. Kiểm thử dùng DB ngẫu nhiên riêng, không thay dữ liệu `sep`; không bật/gửi SMTP thật.
- Còn cảnh báo bundle FE lớn hơn 500 kB. Chưa thực hiện chia bundle toàn ứng dụng vì nằm ngoài thay đổi email.
- **Chưa nghiệm thu trực quan:** công cụ Computer Use dừng vì không xác định chắc chắn URL Chrome. Cần kiểm tra trình duyệt thực tế: lưu → sidebar → quay lại/F5; đổi trường; Back/Forward và ở lại form; layout mobile; route login/profile/ma trận sau đổi router; hộp thông báo theo từng vai trò. Build/test không thay thế các bước này.
- **Chưa thử Gmail thật:** thực hiện theo hướng dẫn SMTP trong tài liệu bằng tài khoản/địa chỉ người nhận do người vận hành chỉ định. Khởi động lại BE để dùng code/cấu hình mới; chạy lại FE cùng contract. Không dùng kết quả của server cũ để đánh giá thay đổi mới.

### Checklist nghiệm thu còn lại

- [ ] Kiểm tra giao diện desktop/mobile và điều hướng bằng trình duyệt thực tế.
- [ ] Kiểm tra gửi tới địa chỉ thử được chỉ định sau khi cấu hình Gmail/Workspace trên máy chủ.
- [ ] Khi nhóm cập nhật DB, chạy các migration đã có bằng connection string đúng môi trường; xác nhận server đang chạy đúng build. Không sửa/xóa lịch sử migration để ép đồng bộ.

Tài liệu dành riêng cho phần quản lý email: tính năng hiện có, quy tắc người nhận, migration, hướng dẫn Gmail SMTP và kế hoạch mở rộng danh mục sự kiện, gửi thủ công, đặt lịch. Kế hoạch điều chỉnh ưu tiên nằm ngay bên dưới; phần tiến độ và các mốc triển khai phía sau là lịch sử công việc, không thay thế nghiệm thu kế hoạch mới.

Hướng dẫn chạy dự án chung nằm trong [README.md](README.md).

## Kết quả review tính thực tế và phạm vi — 07/10/2026

**Kết luận: hướng thiết kế phù hợp, nhưng chưa đủ cơ sở nói đã khớp toàn bộ backlog hoặc sẵn sàng triển khai nguyên trạng.** Tiếp tục tận dụng code email đã có; ưu tiên khôi phục cấu hình và xác định đúng quyền trước khi mở rộng luồng gửi. Lần review này chỉ đọc source và sửa kế hoạch, không chạy lại ứng dụng, kiểm thử, migration hoặc sửa code.

### Các điểm bắt buộc điều chỉnh trước khi triển khai

| Mức | Phát hiện và bằng chứng | Điều chỉnh kế hoạch |
| --- | --- | --- |
| P1 — Phạm vi nghiệp vụ | Backlog có **View Email & Notification**, nhưng kế hoạch đang tự loại hộp thông báo cá nhân khỏi phạm vi. Code có `NotificationRecipient.ReadAt`, còn các endpoint email đang phục vụ người quản lý trường; có cột ReadAt không chứng minh đã có thông báo trong ứng dụng. | Ghi rõ phần đã chắc chắn là quản lý email/lịch sử gửi của trường. Chưa kết luận hoàn thành backlog “Email & Notification” cho đến khi xác định có cần danh sách thông báo cá nhân, số chưa đọc, xem/đánh dấu đã đọc hay không. Nếu cần, bổ sung slice theo người nhận vào đúng module hiện có, không biến lịch sử gửi của cả trường thành hộp thư cá nhân. |
| P1 — Phạm vi quyền | `EmailManagementService.AccessAsync` suy ra trường từ phân hiệu của tài khoản; quyền PHT được kiểm tra qua role hiệu lực. Điều này chưa chứng minh role chỉ có phạm vi một phân hiệu được phép sửa cấu hình/người nhận của cả trường. | Task 0B phải đối chiếu quyền gán thực tế và policy dùng chung. Không tự nâng quyền từ phân hiệu lên toàn trường, không suy ra quyền từ sidebar. Admin trường và PHT có quyền cấp trường mới sửa cấu hình chung; role chỉ cấp phân hiệu không mặc nhiên được quyền đó. Không tự thêm mô hình người dùng nhiều trường khi source hiện tại chưa hỗ trợ. |
| P1 — Chống gửi trùng tự động | `QueueMatrixAsync` tạo eventKey bằng `Guid.NewGuid()` mỗi lần gọi. Điều này không cung cấp khóa ổn định nếu cùng một lần chuyển trạng thái được phát lại, dù transaction và kiểm tra trạng thái ở producer có thể chặn một số lần gọi lặp. | Task 3C cần xác minh end-to-end producer và retry, không chỉ test enqueue bằng key tự đặt. Dùng ID lần chuyển trạng thái/phiên bản nghiệp vụ bền vững nếu mô hình có; phải phân biệt retry với nộp lại/duyệt lại hợp lệ. Không thay bằng khóa chỉ gồm matrixId + eventCode vì sẽ làm mất thông báo ở lần phát sinh sau. |
| P1 — Bảo vệ form chưa lưu | `AppRoutes.tsx` dùng BrowserRouter. Bản `react-router` đang cài gọi data-router context bên trong `useBlocker`, nên không thể chỉ thêm hook này vào ba page hiện có. `beforeunload` cũng không giải quyết đầy đủ điều hướng nội bộ. | Tách task 1C khảo sát/thử nghiệm điều hướng. Kiểm tra phương án phù hợp router hiện tại trước khi cam kết chặn sidebar và Back/Forward. Không vá trực tiếp history toàn ứng dụng hoặc tự chuyển toàn bộ router chỉ cho email. Nếu cần thay router dùng chung, lập thay đổi riêng có phạm vi và regression rõ ràng; chưa xử lý thì giữ tiêu chí này ở trạng thái chưa đạt. |
| P2 — Trạng thái sau lưu | `ConfigurationForm` gọi PUT nhưng bỏ response; `onSaved` gọi `useAsync.reload()`. Hook tạm trả data undefined trong khi tải và khi GET lỗi. Kế hoạch phải xử lý cả khoảng trống này, không chỉ ghi nhớ eventCode. | Task 1B giữ bản PUT đã xác nhận để hiển thị; kiểm tra lại bằng GET. Kết quả của trường/sự kiện khác không được ghi đè bản hiện tại. Không sửa hook useAsync toàn hệ thống chỉ để giải quyết trạng thái riêng email. |
| P2 — Nhóm người nhận | `EmailRecipientResolver` hỗ trợ nhóm **vai trò** và tài khoản thuộc trường, không có nhóm tùy ý như “Ban tổ chức A”, không có người nhận email bên ngoài trường. | Ghi đây là phạm vi có bằng chứng từ code, không gọi chung là hỗ trợ mọi loại nhóm. Ví dụ “nhóm giáo viên + người A − người B” đáp ứng được khi A/B là tài khoản đủ điều kiện trong trường. Nhóm tự tạo hoặc địa chỉ ngoài hệ thống là yêu cầu bổ sung cần xác định, không âm thầm tạo bảng/chức năng mới. |
| P2 — Đang bật chưa chắc gửi được | Cấu hình có thể đang bật nhưng mẫu/sự kiện đã ngừng áp dụng, nhóm không còn thành viên hợp lệ hoặc SMTP chưa sẵn sàng. | Danh sách giữ trạng thái cấu hình riêng, kèm lý do chưa thể gửi khi có thể xác định. Trạng thái sự kiện và mẫu không gộp vào cờ bật/tắt. Không resolve toàn bộ người nhận cho từng dòng danh sách; chỉ preview khi mở chi tiết/xác nhận. |
| P2 — Khối lượng và tính khả thi | Task 1A mới liệt kê file email nhưng yêu cầu dọn trạng thái khi logout; `utils/storage.ts` mới dọn prefix matrix-draft. Task 3C có thể cần sửa producer ma trận, ngoài ba file đã liệt kê. | Danh sách file là dự kiến, phải bổ sung điểm tích hợp thực tế trước khi code. Tách task khi vượt phạm vi, không ép giới hạn số file bằng cách đặt logic sai tầng. Không thêm framework lịch/queue mới vì Notification và worker hiện có đã đáp ứng lịch một lần. |

P1 là điểm phải xử lý hoặc xác định rõ trước khi nghiệm thu; P2 phải đưa vào implementation/kiểm thử nhưng không yêu cầu viết lại kiến trúc. Tham khảo [React Router: useBlocker](https://reactrouter.com/api/hooks/useBlocker) về chặn điều hướng trong SPA; đánh giá tương thích dựa trên **bản đang cài trong dự án**, không nâng package theo tài liệu latest.

### Phạm vi chắc chắn và những điểm chưa được chốt

- **Đã được yêu cầu rõ:** email theo trường gồm nhiều phân hiệu; Admin quản lý mẫu; PHT cấu hình/xem thông báo; người nhận nhóm cộng cá nhân trừ người/nhóm; sự kiện mở rộng; gửi thủ công và đặt lịch; tự động khi có tích hợp nghiệp vụ; phân trang, validation, giữ lịch sử và cấu trúc source.
- **Thiết kế phù hợp để giữ:** danh mục sự kiện DB; phân biệt loại sự kiện với từng lần gửi; một cấu hình cấp trường cho mỗi sự kiện; phiên bản mẫu bất biến; lựa chọn UI lưu trong phiên nhưng cấu hình nằm ở DB; lịch một lần, UTC trong DB và UTC+7 trên giao diện.
- **Cần xác định ở 0B:** “View Email & Notification” có bao gồm hộp thông báo trong ứng dụng không; nhóm nhận chỉ là vai trò hay có nhóm tự tạo; role PHT/Admin cấp phân hiệu có quyền gì đối với cấu hình cấp trường. Không hỏi lại lựa chọn Gmail hoặc gửi thủ công/đặt lịch đã được người dùng trả lời.
- **Chưa tự đưa vào triển khai:** nhóm người nhận tùy ý, người ngoài hệ thống, SMTP riêng từng trường, lịch lặp, thông báo thời gian thực qua socket. Việc chưa đưa vào không được dùng để kết luận các phần này đã được người dùng loại khỏi backlog.

### Quy tắc vận hành cần ghi rõ trên luồng gửi

1. Với gửi thủ công/đặt lịch, snapshot nội dung và người nhận tại lúc xác nhận. Người vào nhóm sau đó không tự được thêm; người chỉ rời nhóm nhưng vẫn đủ điều kiện trong trường vẫn thuộc snapshot. Người chuyển trường/ngừng hoạt động/đổi email bị kiểm tra lại trước gửi theo quy tắc hiện có. Nếu muốn chọn nhóm tại thời điểm đến lịch, đó là một chính sách khác và cần thay đổi thiết kế có chủ đích.
2. Ngừng sự kiện/mẫu ngăn tạo lần gửi mới; không tự hủy thư đã xếp hàng. Hiển thị rõ điều này khi ngừng áp dụng và dẫn tới lịch sử để hủy bản chưa bắt đầu.
3. Lịch là thời điểm sớm nhất được xử lý, không cam kết đúng từng giây. Worker hiện giới hạn batch và SMTP có độ trễ; kiểm tra tải bằng quy mô người nhận thực tế trước deploy. SENT là SMTP chấp nhận, không xác nhận vào inbox hoặc đã đọc.
4. Không cam kết exactly-once qua SMTP. Khóa chống enqueue lặp và lease xử lý các trường hợp nội bộ; crash sau khi SMTP nhận nhưng trước lưu SENT vẫn có khả năng gửi lại. Không tự thêm nút “gửi lại tất cả” làm người đã nhận nhận thêm.
5. Phân biệt thời điểm gửi và ngày nghiệp vụ: biến `meetingDate` là ngày cuộc họp, không tự trở thành lịch gửi. Tiêu đề/nội dung là văn bản thuần, biến kiểm tra theo schema của phiên bản mẫu đang ghim; không cho chạy script/SQL/HTML tùy ý.

**Thứ tự ưu tiên sau review:** 0A + 0B → 1A + 1B (sửa trải nghiệm lưu/mở lại), 1C được khảo sát sớm → 2A–2C (danh sách cấu hình) → 3A–3C (hoàn thiện sự kiện ngoài ma trận và kiểm tra producer) → 4A–4B (gửi/lịch sử) → 5A (nghiệm thu). Các test trước đây chỉ là bằng chứng regression cho code tại thời điểm đó; không tự đánh dấu các task mới đã đạt.

## Kế hoạch điều chỉnh sau phản hồi — 06/10/2026

**Đây là kế hoạch ưu tiên cho lần triển khai tiếp theo.** Lần rà soát này chỉ cập nhật tài liệu, chưa sửa BE/FE hoặc DB. Các checklist đã hoàn thành bên dưới ghi nhận lần triển khai trước, không có nghĩa lỗi người dùng vừa phản ánh đã được nghiệm thu trên trình duyệt. Giữ các tầng, thư mục và bộ giao diện hiện có; API FE tiếp tục khai báo tập trung tại `src/services/api.ts`; không tự commit/push.

### 1. Hiện trạng và phần còn thiếu

| Vấn đề | Bằng chứng từ source hiện tại | Hướng xử lý |
| --- | --- | --- |
| Quay lại màn email thấy cấu hình mới/trống | `EmailManagementPage.tsx` đã giữ lựa chọn trong query URL, nhưng khi URL không có `schoolId/eventCode`, trang lấy trường đầu tiên và `events.data.items[0]`. Mở `/emails` từ điều hướng có thể mất lựa chọn trước đó. | Khôi phục ngữ cảnh theo tài khoản và trường; có danh sách cấu hình rõ trạng thái để mở lại đúng bản đã lưu. |
| Không phân biệt chưa cấu hình với đang tải/lỗi tải | `ReadConfigAsync` trả cấu hình mặc định với `id=null`, `version=0` khi chưa có bản ghi cho đúng trường/sự kiện. Đây không chứng minh bản ghi của sự kiện khác đã mất. | Phân biệt bằng kết quả API; lỗi mạng/quyền phải hiển thị lỗi, không biến thành form tạo mới. |
| Cảm giác chỉ hỗ trợ ma trận | Source đã có `email_events`, CRUD sự kiện MANUAL và gửi/đặt lịch; dữ liệu SYSTEM ban đầu gồm bốn sự kiện ma trận đã tích hợp. | Làm rõ luồng tạo sự kiện riêng và nguồn kích hoạt; xác minh code/DB/API đang chạy đúng phiên bản trước khi thêm tính năng trùng lặp. |
| Chưa đủ bằng chứng nghiệm thu giao diện | Lần trước có kiểm thử tự động nhưng chưa kiểm tra trình duyệt thực tế. | Tái hiện đúng thao tác qua sidebar, đổi tab, F5, Back/Forward; ghi nhận request GET/PUT và trường/sự kiện tương ứng. |

Nhận định về mất ngữ cảnh dựa trên source; chưa kết luận đây là nguyên nhân duy nhất trên máy đang chạy. Phase 0 phải phân biệt: PUT chưa thành công, GET sai trường/sự kiện, response đúng nhưng FE hiển thị sai, hoặc server vẫn chạy code cũ. Không dùng các kết quả kiểm tra DB trước đây để khẳng định dữ liệu hiện tại.

### 2. Luồng sử dụng cần đạt

**Mở lại cấu hình đã lưu**

1. Người dùng mở Quản lý email. URL chỉ rõ trường/sự kiện thì ưu tiên URL; nếu URL không chỉ rõ, khôi phục lựa chọn hợp lệ gần nhất của chính tài khoản trong phiên trình duyệt.
2. Khi chưa có lựa chọn hợp lệ, hiển thị danh sách cấu hình của trường được phép. Nếu có nhiều trường và chưa chọn trường, yêu cầu chọn trường rõ ràng; không âm thầm chọn trường đầu tiên để mở form.
3. Tab **Cấu hình thông báo** hiển thị danh sách sự kiện với trạng thái **Chưa cấu hình / Đang bật / Đang tắt**, tên mẫu, phiên bản đã ghim và số lựa chọn nhóm/cá nhân. Số lựa chọn không được gọi là số người nhận; số người nhận thực tế lấy từ chức năng xem trước.
4. Bấm **Chỉnh sửa** mở cấu hình đã lưu; **Thiết lập** chỉ dành cho sự kiện chưa có cấu hình. Sau lưu thành công, hiển thị bản đọc lại từ DB và trạng thái mới trên danh sách.
5. Rời sang phần khác rồi bấm sidebar trở lại, đổi tab hoặc F5 phải mở lại đúng ngữ cảnh còn hợp lệ. Có thay đổi chưa lưu thì cảnh báo trước điều hướng trong ứng dụng; khi hủy điều hướng phải giữ nguyên form.

**Sự kiện mở rộng**

| Loại | Ai tạo và quản lý | Cách phát sinh gửi |
| --- | --- | --- |
| Sự kiện riêng của trường (MANUAL) | Admin được phép trong trường tạo tên, mã, mô tả, biến và trạng thái | Admin/PHT chọn gửi ngay hoặc đặt lịch sau khi có mẫu và cấu hình người nhận. Ví dụ: họp chuyên môn, nhắc nộp hồ sơ, thông báo lịch công tác. |
| Sự kiện nghiệp vụ (SYSTEM) | Nhóm phát triển đăng ký danh mục và tích hợp nghiệp vụ; Admin cấu hình mẫu/người nhận theo trường | Nghiệp vụ phát sự kiện tự động trong giao dịch tương ứng. Ví dụ đề thi được duyệt chỉ khả dụng tự động sau khi luồng duyệt đề đã tích hợp. |

Sự kiện là **loại thông báo**, còn mỗi lần gửi là một bản ghi riêng. Ví dụ tạo `STAFF_MEETING` một lần, mỗi cuộc họp nhập nội dung/ngày họp và tạo một lần gửi; không tạo thêm mã sự kiện cho từng ngày họp. Các lựa chọn đã chốt vẫn là **gửi thủ công + đặt lịch**, tự động khi đã tích hợp nghiệp vụ; không cần hỏi lại.

### 3. Quyết định về trạng thái và dữ liệu

- DB là nguồn dữ liệu của cấu hình. Một cấu hình cấp trường được xác định bởi trường + sự kiện; phiên bản mẫu và nhóm/cá nhân cộng/trừ được lưu cùng giao dịch. Một trường bao gồm các phân hiệu; không tự tạo bản cấu hình riêng cho mỗi phân hiệu trong đợt này.
- Lưu lựa chọn giao diện bằng `sessionStorage` theo định danh tài khoản, giữ trường/tab và sự kiện gần nhất theo từng trường. Chỉ lưu định danh lựa chọn; không lưu nội dung thư, danh sách người nhận hay secret. Nếu storage bị chặn, URL và danh sách vẫn dùng được. Đóng phiên trình duyệt có thể mất lựa chọn nhưng cấu hình DB vẫn còn và tra cứu được.
- BE phải xác minh lại quyền/trường trên mọi API. Ngữ cảnh nhớ lại không cấp quyền; xóa hoặc bỏ qua lựa chọn khi đăng xuất/đổi tài khoản, trường không còn được phép hoặc sự kiện đã xóa. URL không hợp lệ phải báo rõ, không tự chuyển sang một trường khác. Sự kiện ngừng áp dụng vẫn cho xem cấu hình/lịch sử, không cho tạo lần gửi mới.
- Không dùng `events[0]` làm lựa chọn thay thế cho cấu hình đã lưu. Không phụ thuộc 100 sự kiện đầu tiên để mở chi tiết; dùng GET theo mã chính xác và danh sách phân trang.
- API danh sách cấu hình dự kiến: `GET /api/schools/{schoolId}/emails/configurations?search=&configurationState=&triggerKind=&page=1&pageSize=20`. Trả `DirectoryPage` theo convention hiện có; trạng thái lọc `UNCONFIGURED/ENABLED/DISABLED`. Mỗi dòng gồm mã/tên/loại/trạng thái sự kiện, config ID/version, mẫu/phiên bản và số lựa chọn nhận. Truy vấn từ danh mục sự kiện của trường và SYSTEM, ghép cấu hình cấp trường; sự kiện chưa cấu hình vẫn xuất hiện. Không trả toàn bộ người nhận cho mỗi dòng.
- Giữ API GET/PUT chi tiết theo mã hiện có. `id=null` là chưa cấu hình; GET lỗi không được coi là chưa cấu hình. Sau PUT, dùng response thành công để cập nhật trạng thái, rồi đối chiếu GET; nếu GET lỗi phải thông báo “đã lưu nhưng chưa tải lại được”, không yêu cầu người dùng nhập lại từ đầu.
- Danh mục sự kiện nằm trong DB. FE và validation runtime dùng cùng định nghĩa; các giá trị SYSTEM seed ban đầu không phải danh sách giới hạn tính năng. Khi bổ sung mã SYSTEM mới, kiểm tra không trùng mã MANUAL đã có ở bất kỳ trường nào; nếu trùng chọn mã SYSTEM khác, không tự đổi mã sự kiện người dùng.
- Bảo toàn phiên bản mẫu và snapshot hợp đồng biến đã ghim. Thay biến sự kiện chỉ áp dụng khi tạo phiên bản mẫu mới; không làm hỏng thư/lịch cũ. Sự kiện/mẫu đã sử dụng chỉ ngừng áp dụng, không xóa lịch sử.
- Chọn người nhận theo công thức `(nhóm + cá nhân thêm) − (nhóm + cá nhân loại trừ)`; loại trừ ưu tiên, chỉ lấy người đủ điều kiện trong trường và không gửi trùng địa chỉ. Cho xem trước trước khi xác nhận.
- Cấu hình và mẫu vẫn lưu được khi SMTP chưa bật. Chỉ thao tác gửi phụ thuộc trạng thái dịch vụ; thông báo SMTP không được che mất hoặc đặt lại dữ liệu cấu hình.

### 4. Các phase và công việc cụ thể

Các đường dẫn BE dưới đây tương đối với `TeLo-Backend`; FE tương đối với `TeLo-FrontEnd/TeLo-Frontend`. Mỗi task thực hiện trong các file hiện có, tối đa khoảng 3–5 file; nếu lớn hơn phải tách contract, xử lý và giao diện. Các mục đang để trống là công việc cần thực hiện hoặc xác minh lại, kể cả khi đã có code nền.

| Task | Công việc, phụ thuộc | Vị trí dự kiến | Tiêu chí đạt và cách kiểm tra |
| --- | --- | --- | --- |
| [ ] 0A — Tái hiện | Không phụ thuộc. Ghi đúng URL/port BE, tài khoản, trường, sự kiện, PUT và GET trước/sau khi mở lại. Kiểm tra migration chỉ đọc. | `README_EMAIL.md`; đọc các service/page và migration hiện có | Phân biệt lỗi lưu DB, lỗi chọn ngữ cảnh và bản chạy cũ bằng request/response; không lưu token hoặc dữ liệu thư vào tài liệu. |
| [ ] 0B — Chốt phạm vi và quyền | Cùng giai đoạn 0A. Đối chiếu backlog Email & Notification, nhóm nhận, role cấp trường/phân hiệu và policy dùng chung; ghi phần chắc chắn/phần chưa chốt. | `README_EMAIL.md`; đọc `EmailManagementService.cs`, `EmailRecipientResolver.cs`, `RoleAssignmentQuery.cs` | Có bảng thao tác theo role/scope; không cấp quyền rộng hơn phạm vi được gán; không tự tuyên bố hộp thông báo cá nhân hoặc nhóm tùy ý đã ngoài backlog. Phần chưa rõ không ngăn sửa lỗi khôi phục cấu hình đã xác định. |
| [ ] 1A — Giữ ngữ cảnh | Sau 0A. URL ưu tiên, khôi phục lựa chọn gần nhất theo tài khoản/trường, bỏ chọn mặc định `events[0]`; xử lý ngữ cảnh mất quyền/xóa. | FE `features/emails/pages/EmailManagementPage.tsx`, `utils/email.ts`, `utils/email.selfcheck.ts`, điểm dọn prefix email tại `utils/storage.ts` | Lưu sự kiện B rồi đi trang khác, bấm lại `/emails`, F5, đổi trường A/B đều mở đúng lựa chọn hợp lệ; không dùng lựa chọn tài khoản khác; logout/hết phiên dọn trạng thái UI. Kiểm thử hàm chọn ngữ cảnh và browser. |
| [ ] 1B — Form đọc lại đúng | Sau 1A. Phân biệt tải/lỗi/chưa cấu hình/đã lưu; giữ response PUT đã xác nhận khi GET lại bị lỗi. | FE `EmailConfiguration.tsx`, `EmailManagementPage.tsx` trong `features/emails/pages` | Nhóm/cá nhân, loại trừ, trạng thái và phiên bản được giữ sau lưu/mở lại; không ghi đè form bởi response của sự kiện trước; không sửa hành vi hook useAsync của toàn hệ thống. |
| [ ] 1C — Bảo vệ điều hướng | Khảo sát sau 0A, trước nghiệm thu phase 1. Kiểm tra router hiện có và các điểm điều hướng tab/sidebar/Back; tách thử nghiệm tương thích trước implementation. | Đọc FE `routes/AppRoutes.tsx`; chỉnh các page email và điểm điều hướng chung thực sự cần thiết sau khi xác định phương án | Có kiểm chứng với BrowserRouter/bản package hiện tại, không dùng useBlocker sai context. Rời/hủy rời giữ đúng form và history; F5/đóng tab dùng beforeunload phù hợp. Nếu phải đổi router toàn ứng dụng, ghi task riêng và regression trước khi làm, không tự coi nằm trong ba page email. |
| [ ] 2A — API danh sách cấu hình | Sau checkpoint 1. Thêm projection có phân trang/lọc, ghép danh mục với cấu hình hiện có, kiểm tra quyền theo trường. | BE `Application/DTOs/EmailManagementDtos.cs`, interface/service EmailManagement hiện có, `WebAPI/Controllers/EmailsController.cs` | Tìm tên/mã, lọc ba trạng thái và nguồn kích hoạt; không trùng dòng, không N+1, không lộ trường khác; không đổi schema chỉ để nhớ lựa chọn UI. |
| [ ] 2B — Danh sách và mở chi tiết | Sau 2A. Bổ sung danh sách vào tab cấu hình; trạng thái, mẫu ghim, nút Thiết lập/Chỉnh sửa và đường quay về danh sách. | FE `services/api.ts`, `types/email.ts`, `features/emails/pages/EmailConfiguration.tsx`, `pages/emails.css` | Cấu hình cũ tìm lại được kể cả khi sessionStorage trống; tìm/lọc đổi về trang 1; bố cục dùng PCB và màu hiện có, nút thẳng hàng ở desktop/mobile. |
| [ ] 2C — Regression danh sách | Sau 2A/2B. Kiểm thử scope, paging và trạng thái trống/lỗi với cấu hình đã có. | BE `Tests/WebAPI.Tests/EmailManagementIntegrationTests.cs`; FE self-check hiện có khi phù hợp | Trên 100 sự kiện vẫn truy cập được đúng cấu hình; GET lỗi không hiện form mới; hai người sửa cùng version được phản hồi xung đột. |
| [ ] 3A — Rà contract sự kiện | Sau checkpoint 2. Kiểm tra runtime dùng danh mục DB, quyền CRUD, mã bất biến và snapshot biến; bổ sung regression thiếu. | BE `Application/Common/EmailTemplateRules.cs`, `Application/Services/Implementations/EmailManagementService.cs`, `Tests/Application.Tests/EmailTemplateRulesTests.cs` | Tạo sự kiện ngoài ma trận với biến hợp lệ; mã trùng trong trường/biến sai bị từ chối; sửa định nghĩa không phá mẫu đã ghim; SYSTEM không sửa như MANUAL. |
| [ ] 3B — Hoàn thiện luồng sự kiện riêng | Sau 3A. Làm rõ nguồn kích hoạt và dẫn tới tạo mẫu/cấu hình đúng sự kiện vừa tạo. | FE `features/emails/pages/EmailEvents.tsx`, `components/EmailEventEditor.tsx`, `pages/EmailManagementPage.tsx`, `pages/EmailTemplates.tsx` | Tạo “Họp chuyên môn” rồi tới mẫu/cấu hình không phải chọn lại sự kiện; PHT không có thao tác quản lý danh mục; picker tìm/phân trang, trạng thái ngừng áp dụng rõ ràng. |
| [ ] 3C — Quy ước tích hợp tự động | Sau 3A. Rà đường enqueue chung và producer, đặc biệt key GUID ở QueueMatrixAsync; xác định ID lần chuyển trạng thái bền vững trước sửa. | BE `EmailNotificationService.cs`, producer `MatrixApplicationService.cs` trong tầng service hiện có, `Tests/WebAPI.Tests/EmailManagementIntegrationTests.cs`, `README_EMAIL.md`; tách task nếu cần thay schema | Test rollback, phát lại cùng lần chuyển trạng thái và nộp/duyệt lại hợp lệ; không chỉ test service với key dựng sẵn. Lần hợp lệ mới vẫn có thông báo. Chỉ tích hợp nghiệp vụ mới khi luồng đó tồn tại. |
| [ ] 4A — Rà gửi và đặt lịch | Sau checkpoint 3. Kiểm tra lại gửi sự kiện MANUAL, cấu hình đã ghim, preview và thời gian. | BE `Application/Services/Implementations/EmailNotificationService.cs`, `EmailManagementService.cs`, `Tests/WebAPI.Tests/EmailManagementIntegrationTests.cs` | Request lặp không tạo thêm; config/nội dung/người nhận thay đổi sau preview phải xem lại; chưa đến hạn không gửi; hủy và worker không cùng thắng; đúng trường/người nhận. |
| [ ] 4B — Hoàn thiện thao tác gửi | Sau 4A. Thông báo SMTP rõ ràng, lỗi theo trường nhập, xác nhận lịch UTC+7, chuyển lịch sử đúng sự kiện. | FE `features/emails/pages/EmailSend.tsx`, `EmailHistory.tsx`, `EmailManagementPage.tsx` | Không mất dữ liệu nhập khi lỗi mạng; retry dùng cùng requestId; lịch và trạng thái hiển thị đúng; có đường về cấu hình khi chưa đủ điều kiện gửi. |
| [ ] 5A — Nghiệm thu | Sau checkpoint 4. Chạy kiểm thử tự động, browser và kiểm tra tương thích dữ liệu cũ. | Các test project hiện có, FE, `README_EMAIL.md` | Hoàn tất bảng tình huống bên dưới; ghi kết quả thực tế và phần chưa kiểm tra; không đánh dấu đạt chỉ dựa trên build. |

**Checkpoint 1:** tái hiện rồi kiểm tra lại đúng thao tác lưu → ra trang khác → bấm sidebar → mở cấu hình, gồm F5 và Back/Forward. Chưa qua checkpoint này thì chưa coi vấn đề “mất cấu hình” đã xử lý.

**Checkpoint 2:** API danh sách và màn mở lại cấu hình hoạt động với dữ liệu sẵn có, nhiều trang, lỗi tải và tài khoản khác trường; focused tests/build đạt.

**Checkpoint 3:** hoàn thành luồng ngoài ma trận từ tạo sự kiện → mẫu → cấu hình nhóm cộng/trừ; kiểm tra phân quyền và mẫu phiên bản cũ. Tận dụng phần đã có, chỉ sửa khoảng trống được tìm thấy.

**Checkpoint 4:** preview → gửi ngay/đặt lịch → lịch sử → hủy được kiểm tra bằng fake SMTP; worker đúng hạn và không gửi trùng do hai worker cùng claim. Không dùng Gmail thật cho kiểm thử tự động.

### 5. Tình huống nghiệm thu bắt buộc

| Tình huống | Kết quả cần đạt |
| --- | --- |
| Trường A, sự kiện B đã lưu; chuyển tab rồi quay lại | Đúng A/B, đúng mẫu/phiên bản, trạng thái, nhóm và người bị loại trừ. |
| Ra trang khác rồi bấm sidebar `/emails`; F5; Back/Forward | Khôi phục lựa chọn hợp lệ; cấu hình đọc từ BE. Không có lựa chọn thì hiện danh sách/chọn trường rõ ràng. |
| Có thay đổi chưa lưu, rời bằng tab/sidebar/Back | Hỏi trước khi bỏ; chọn ở lại giữ nguyên dữ liệu. |
| Storage bị chặn hoặc phiên trình duyệt mới | Tìm và mở cấu hình đã lưu từ danh sách, không yêu cầu tạo lại. |
| Đổi tài khoản, mất quyền trường, URL sai hoặc sự kiện đã xóa | Không truy cập dữ liệu trái quyền, không lẫn lựa chọn người khác; có thông báo và đường chọn lại. |
| PUT thành công nhưng GET sau đó lỗi mạng | Báo đã lưu/chưa tải lại được, có thử lại; không tự chuyển thành “Chưa cấu hình”. |
| PUT thất bại hoặc hai người cùng sửa | Giữ dữ liệu nhập, hiển thị lỗi; version cũ không ghi đè dữ liệu mới. |
| Danh mục có hơn 100 sự kiện | Phân trang/tìm kiếm và mở đúng mã ở ngoài trang đầu vẫn hoạt động. |
| Tạo sự kiện “Họp chuyên môn” với `topic`, `meetingDate` | Tạo mẫu, cấu hình rồi gửi/đặt lịch được; không cần thêm mã sự kiện đó vào source FE/BE. |
| Nhóm giáo viên + cá nhân A ngoài nhóm − cá nhân B trong nhóm | A được nhận nếu đủ điều kiện trong trường; B không nhận; người có nhiều vai trò chỉ nhận một thư. |
| Thay biến sự kiện hoặc tạo phiên bản mẫu mới | Cấu hình ghim phiên bản cũ và lịch đã tạo vẫn giữ hợp đồng/nội dung cũ. |
| SMTP tắt, bật lại; BE dừng rồi chạy lại | Lưu/xem cấu hình vẫn được; gửi có thông báo phù hợp; lịch được lưu DB và xử lý khi đến hạn sau khi worker chạy lại. |
| Gửi lặp, người nhận thay đổi sau preview, hủy đồng thời worker | Chống enqueue trùng; yêu cầu xem trước lại khi thay đổi; không báo hủy thành công bản đã bắt đầu gửi. |
| Desktop/mobile và bàn phím | Các nút/bộ lọc thẳng hàng; bảng cuộn trong vùng phù hợp; dialog có focus và nhãn; không tràn toàn trang. |

**Cách kiểm tra:** chạy các test project BE và `npm run build`; lint các file email thay đổi và ghi riêng lỗi lint sẵn có ngoài phạm vi. Browser kiểm tra Network/Console và luồng trong bảng bằng dữ liệu test theo trường. Gmail thật chỉ kiểm tra sau khi người dùng cấu hình tài khoản, dùng người nhận thật được chỉ định. Kết quả test của lần trước được giữ ở phần lịch sử bên dưới, không thay thế nghiệm thu các task mới.

**DB và triển khai:** trước khi làm kiểm tra migration/model hiện có; tái sử dụng `email_events`, `notification_configs`, `notification_targets`, các phiên bản mẫu và hàng đợi. Phase khôi phục ngữ cảnh/danh sách không cần thêm bảng. Nếu phát sinh thay đổi schema thật sự, dùng migration bổ sung, sao lưu và kiểm tra dữ liệu trước/sau; không sửa migration đã được cả nhóm áp dụng. Khi triển khai cần đồng bộ BE/FE cùng contract rồi restart BE; không kết luận thiếu tính năng chỉ từ một server còn chạy bản cũ.

**Giới hạn phạm vi:** đợt này đã xác định email theo trường và lịch một lần; chưa tự thêm SMTP riêng từng trường hoặc lịch lặp. Hộp thông báo cá nhân/đã đọc phải xác định ở task 0B theo backlog View Email & Notification, không mặc nhiên loại khỏi yêu cầu. Không tự mở rộng sidebar hoặc tái cấu trúc toàn hệ thống.

## Email IT3 — mốc triển khai ban đầu ngày 05/10/2026

Phần này ghi lại mốc ban đầu. Bản cập nhật ngày 06/10/2026 bên dưới đã bổ sung danh mục sự kiện, gửi thủ công và đặt lịch; giao diện hiện có năm tab. Hướng dẫn sử dụng bản mới nằm ở mục “Các bước sử dụng sau khi cập nhật”.

### Các phase và phạm vi

- [x] Contract và DB: tái sử dụng NotificationConfig/NotificationTarget/Notification/NotificationRecipient; thêm EmailTemplate và các phiên bản bất biến trong các tầng hiện có.
- [x] BE: API theo trường, kiểm tra quyền từ DB, validation, phân trang, optimistic concurrency, audit, hàng đợi gửi và thông báo ma trận.
- [x] FE: `/emails` nối sidebar Admin/PHT; ba phần Mẫu email / Cấu hình thông báo / Lịch sử gửi. API khai báo tại `src/services/api.ts`; sử dụng lại PCB, hooks, phân trang và màu hiện có.
- [x] Kiểm thử tự động: 123 Application + 21 Infrastructure + 18 WebAPI tests đạt, không skip. Kiểm thử email dùng MySQL database ngẫu nhiên riêng và fake SMTP, bao gồm migration lên/xuống, phân quyền, phiên bản, nhóm cộng/trừ cá nhân, idempotency và rollback hàng đợi cùng giao dịch ma trận.
- [ ] Kiểm tra trực quan trong trình duyệt và gửi qua SMTP thật: môi trường công cụ chưa kết nối trình duyệt; không gửi email thật trong phiên phát triển.

### Quy tắc sử dụng

Admin quản lý mẫu và gửi thử. PHT của trường được cấu hình thông báo và xem mẫu/lịch sử của trường đó. Admin toàn hệ thống chọn trường trước; toàn bộ truy vấn và thao tác đều giới hạn theo trường, không dựa vào sidebar để cấp quyền.

Nhóm nhận hiện là **nhóm vai trò**: lấy người dùng đang hoạt động trong các phân hiệu đang hoạt động của trường. Danh sách cuối = (nhóm được chọn + cá nhân được thêm, hoặc toàn trường) trừ nhóm/cá nhân bị loại trừ. Loại trừ được ưu tiên; một địa chỉ hợp lệ chỉ có một bản gửi trong mỗi thông báo. Danh sách lựa chọn, xem trước, mẫu, phiên bản, lịch sử và chi tiết người nhận đều phân trang trên server (tối đa 100 bản ghi/trang). Cấu hình tối đa 200 lựa chọn nhóm/cá nhân.

Mỗi lần sửa mẫu tạo phiên bản nội dung mới. Cấu hình ghim phiên bản đã chọn; cần chủ động chọn phiên bản khác để đổi luồng. Email đã xếp hàng giữ nguyên nội dung và người nhận. Mã mẫu và sự kiện không đổi sau tạo. Mẫu chưa dùng được xóa; mẫu đã tham chiếu chỉ ngừng áp dụng, giữ lịch sử. Ngừng áp dụng ngăn xếp hàng mới, không hủy email đã xếp hàng.

Bốn sự kiện đã nối với giao dịch nghiệp vụ: giao nhiệm vụ ma trận, nộp ma trận, duyệt/hoàn tất ma trận, yêu cầu chỉnh sửa ma trận. Các sự kiện khác chưa có luồng nghiệp vụ không được khai báo giả. Mẫu dùng văn bản thuần và các biến được liệt kê trên giao diện; không nhận HTML/script hoặc biến tùy ý.

Worker gửi tối đa 20 bản mỗi vòng, tự thử lại tối đa 3 lần, kiểm tra lại trạng thái/trường/email của người nhận trước gửi. Lease và token ngăn hai worker cùng nhận một bản. Đây là cơ chế **at-least-once**: nếu tiến trình chết sau khi SMTP chấp nhận nhưng trước khi lưu kết quả, email có thể bị gửi lại. Trạng thái SENT nghĩa là SMTP đã chấp nhận, không cam kết thư vào inbox. Gửi thử giới hạn 10 lần/người/giờ và có requestId chống xếp hàng trùng khi thử lại HTTP.

### Cấu hình chạy và đồng bộ DB

DB local `sep` đã sao lưu trước migration và áp dụng `20261004190017_AddSchoolEmailManagement`. EF xác nhận không còn thay đổi model chưa có migration. Nhóm dùng connection string riêng trong user-secrets hoặc biến môi trường, rồi chạy từ `TeLo-Backend`:

```powershell
dotnet tool restore
dotnet ef database update --project Infrastructure --startup-project WebAPI
dotnet run --project WebAPI
```

Migration lịch sử `20260926142739_GlobalAcademicYears` đã được sửa thành no-op vì `20260926120000_AcademicYearSystemScope` đã thực hiện thay đổi đó trước; tránh xóa FK lần hai hoặc xóa cột trường không liên quan. Giữ nguyên ID migration. Không tự sửa bảng lịch sử EF. Không chạy Down trên DB có dữ liệu email cần giữ; rollback migration chỉ được kiểm thử trên database test riêng.

Tại FE chạy `npm ci`, `npm run dev`, đăng nhập Admin/PHT và mở mục quản lý email. Cấu hình SMTP qua user-secrets hoặc environment, không ghi mật khẩu vào source:

| Khóa | Giá trị |
| --- | --- |
| `EmailSettings:Host` | Máy chủ SMTP của hệ thống; mặc định smtp.gmail.com |
| `EmailSettings:Port` | Cổng STARTTLS; mặc định 587 |
| `EmailSettings:Email` | Tài khoản gửi |
| `EmailSettings:Password` | Mật khẩu ứng dụng/SMTP secret |
| `EmailSettings:DisplayName` | Tên hiển thị người gửi |
| `NotificationEmail:FrontendOrigin` | Origin FE thật, ví dụ https://school.example |
| `NotificationEmail:Enabled` | true sau khi cấu hình SMTP xong |

Biến môi trường dùng `__` thay `:`. Worker mặc định tắt; API gửi thử trả thông báo chưa bật dịch vụ. Sự kiện nghiệp vụ vẫn lưu hàng đợi nếu cấu hình trong trường đã bật. Khởi động lại BE sau khi đổi cấu hình. Không ghi nội dung, OTP hoặc mật khẩu SMTP vào log.

Các thay đổi email giữ cấu trúc thư mục; các điểm chung cần chú ý khi nhóm merge là DI, Program, DbContext/snapshot, central API và routes. Không commit/push trong phiên này. Không thay nội dung chỉnh sửa sẵn của người dùng trong `Application.csproj`.

## Email — hướng dẫn SMTP và kế hoạch mở rộng ngày 06/10/2026

Phần này là kế hoạch tiếp theo của bản Email IT3 ở trên, đã được người dùng yêu cầu triển khai ngày 06/10/2026. Tài liệu riêng được tách theo yêu cầu người dùng; kế hoạch học kỳ còn dang dở ở `tasks/` giữ nguyên. Không tự commit/push hoặc bật SMTP thật.

### Bật Gmail SMTP trên máy phát triển

Thông báo “Chưa bật dịch vụ gửi email” được trả về khi `NotificationEmail:Enabled` chưa là `true`. Đây là cấu hình của BE, không phải giá trị cần thêm trong MySQL hay FE. Worker đọc cờ lúc khởi động, nên phải khởi động lại BE sau khi đổi.

1. Đăng nhập tài khoản Gmail dùng để gửi, bật xác minh 2 bước, mở [App passwords](https://myaccount.google.com/apppasswords) và tạo mật khẩu ứng dụng cho TeLo. Dùng mật khẩu ứng dụng thay mật khẩu đăng nhập Google. Nếu tài khoản Workspace không cho tạo, kiểm tra chính sách với quản trị viên của tổ chức. Tham khảo [hướng dẫn Google](https://support.google.com/accounts/answer/185833?hl=en).
2. Dừng BE đang chạy. Mở PowerShell ở thư mục `TeLo-Backend`. Dự án WebAPI đã có UserSecretsId `telo-school-management-webapi-local`, không cần chạy `user-secrets init`.
3. Chạy các lệnh dưới đây, thay email người gửi. Nhập mật khẩu ứng dụng khi được hỏi; không gửi mật khẩu cho người khác hoặc ghi vào Git.

```powershell
dotnet user-secrets set "EmailSettings:Host" "smtp.gmail.com" --project WebAPI
dotnet user-secrets set "EmailSettings:Port" "587" --project WebAPI
dotnet user-secrets set "EmailSettings:Email" "email-gui-cua-ban@gmail.com" --project WebAPI
dotnet user-secrets set "EmailSettings:DisplayName" "TeLo - Thong bao nha truong" --project WebAPI

$taskSmtpSecret = Read-Host "Nhap mat khau ung dung Gmail" -AsSecureString
$taskSmtpCredential = [System.Management.Automation.PSCredential]::new("smtp", $taskSmtpSecret)
dotnet user-secrets set "EmailSettings:Password" ($taskSmtpCredential.GetNetworkCredential().Password.Replace(" ", "")) --project WebAPI
Remove-Variable taskSmtpSecret, taskSmtpCredential

dotnet user-secrets set "NotificationEmail:FrontendOrigin" "http://localhost:5173" --project WebAPI
dotnet user-secrets set "NotificationEmail:Enabled" "true" --project WebAPI
dotnet run --project WebAPI --launch-profile http
```

Gmail dùng `smtp.gmail.com`, cổng 587 với STARTTLS theo [tài liệu SMTP của Google](https://support.google.com/mail/answer/7104828?hl=en). `EmailService` hiện đã bật TLS; các lệnh trên phù hợp với cách gửi đang có.

Sau khi bật, worker cũng xử lý các email nghiệp vụ đang PENDING, không chỉ email gửi thử. Mở **Mẫu email**, chọn mẫu của đúng trường, bấm **Gửi thử**, chọn một tài khoản trong trường có địa chỉ email thật bạn kiểm soát và xem **Lịch sử gửi**. Nếu email tài khoản chưa đúng, cập nhật tài khoản trước; màn gửi thử không nhập địa chỉ ngoài trường. Các tài khoản seed có email đuôi `.invalid` không nhận được thư. SENT nghĩa là SMTP đã chấp nhận thư; kiểm tra thêm Inbox/Spam của người nhận. Worker chạy mỗi 10 giây, nên không nhất thiết gửi ngay lúc bấm.

Nếu vẫn báo chưa bật: kiểm tra đã restart đúng BE/port FE đang gọi và đúng project WebAPI; biến môi trường `NotificationEmail__Enabled` có thể ghi đè user-secrets. Nếu thư ERROR: kiểm tra mật khẩu ứng dụng, địa chỉ người gửi và kết nối từ máy BE tới SMTP. Có thể kiểm tra kết nối TCP bằng `Test-NetConnection smtp.gmail.com -Port 587`; kết quả này không kiểm tra đăng nhập SMTP. Không bật POP/IMAP để gửi email.

Muốn dừng worker, đặt `NotificationEmail:Enabled` thành `false` rồi restart BE. Cờ này dành cho worker thông báo; luồng email xác thực hiện có sử dụng EmailService riêng.

### Khi triển khai lên máy chủ

User-secrets dành cho môi trường Development và không đi cùng bản deploy. Cấu hình trong secret store hoặc phần environment của dịch vụ/container/IIS đang chạy BE, rồi restart dịch vụ. Các tên biến tương ứng:

| Biến môi trường | Giá trị |
| --- | --- |
| `EmailSettings__Host` | `smtp.gmail.com` |
| `EmailSettings__Port` | `587` |
| `EmailSettings__Email` | Địa chỉ Gmail/Workspace gửi thư |
| `EmailSettings__Password` | Mật khẩu ứng dụng của tài khoản đó |
| `EmailSettings__DisplayName` | Tên hệ thống/trường hiển thị |
| `NotificationEmail__FrontendOrigin` | Origin FE đã deploy, ví dụ `https://school.example` |
| `NotificationEmail__Enabled` | `true` |

Phải gán vào môi trường của **tiến trình BE thực tế**; chỉ đặt `$env:...` ở một cửa sổ PowerShell khác không thay cấu hình của dịch vụ đã chạy. Tham khảo [Microsoft: user-secrets và biến môi trường](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-8.0). Hiện hệ thống dùng một cấu hình SMTP chung; mẫu, cấu hình, người nhận và lịch sử vẫn tách theo trường.

### Kết quả kiểm tra trước khi lập kế hoạch

- FE `EmailConfiguration.tsx` khởi tạo sự kiện bằng `events[0]`. `EmailManagementPage.tsx` unmount tab cấu hình khi chuyển tab, nên quay lại sẽ chọn sự kiện đầu tiên; reload trang cũng chọn lại trường đầu tiên. Cấu hình DB được lưu theo cặp trường + sự kiện, không dùng chung cho tất cả sự kiện.
- Kiểm tra đọc DB `sep` ngày 06/10/2026: trường `2` có cấu hình `MATRIX_APPROVED`, active, template version ID `1`, config version `2`, có `2` target. Không có dấu hiệu cấu hình này bị mất khi đổi tab. Nhãn FE chỉ ghi “Phiên bản đã lưu trong cấu hình”, chưa hiện tên mẫu/số phiên bản cụ thể.
- `EmailTemplateRules.Events` hiện cố định bốn sự kiện ma trận; cả validation và render đều phụ thuộc danh sách đó. Chỉ thêm option ở FE sẽ không tạo được sự kiện mới hợp lệ ở BE.
- `Notification.ScheduledFor` có sẵn, nhưng `DeliverPendingAsync` chưa lọc thời điểm này khi chọn/claim bản gửi. Chưa được coi là có tính năng đặt lịch.
- Lịch sử gửi hiện dành cho người quản lý trường; chưa có hộp thông báo cá nhân đầy đủ chỉ từ việc tồn tại `ReadAt`.

### Mô hình tính năng được chốt

Người dùng đã chọn **gửi thủ công và đặt lịch** cho sự kiện tự tạo; sự kiện nghiệp vụ gửi tự động khi đã được tích hợp. Đề xuất tách rõ ba khái niệm:

| Thành phần | Mục đích |
| --- | --- |
| Danh mục sự kiện | Mã, tên, mô tả, trạng thái, biến hợp lệ, phạm vi và khả năng kích hoạt |
| Mẫu email/cấu hình nhận | Nội dung theo phiên bản và quy tắc nhóm + cá nhân − loại trừ theo trường |
| Lần gửi thông báo | Một lần phát sinh thực tế, gồm nội dung, người nhận, người tạo, lịch gửi và kết quả |

**Sự kiện hệ thống** do các nghiệp vụ tích hợp phát ra; danh mục chỉ hiển thị “Tự động” khi có producer tương ứng. Bốn sự kiện ma trận hiện tại thuộc nhóm này. Sự kiện mới như đề thi được duyệt chỉ tự động gửi sau khi nhóm phát triển tích hợp đúng giao dịch nghiệp vụ.

**Sự kiện do Admin tạo** thuộc trường đó, có thể dùng để gửi ngay hoặc đặt lịch, ví dụ “Thông báo họp chuyên môn”. Tạo một mã sự kiện không tự tạo nghiệp vụ hay cơ chế theo dõi thay đổi DB. Giao diện phân biệt nguồn kích hoạt và khả năng gửi; không cho Admin nhập SQL/script để chạy tự động.

Sự kiện hệ thống có bộ biến do nghiệp vụ cung cấp. Sự kiện thủ công có biến chung do server cấp và biến nội dung do Admin khai báo với tên, kiểu, bắt buộc/không bắt buộc, giới hạn độ dài. Khi gửi phải nhập đủ biến bắt buộc và xem trước kết quả. Giữ nội dung văn bản thuần hiện có. Đổi hợp đồng biến tạo phiên bản mới; phiên bản mẫu cũ tiếp tục gắn với hợp đồng biến cũ để không làm hỏng cấu hình/lịch sử.

Quyền dự kiến: Admin quản lý danh mục riêng/mẫu/cấu hình/gửi/lịch sử trong trường được phép; PHT cấu hình/gửi/xem lịch sử trong trường được phép, không sửa hợp đồng sự kiện hệ thống; các role khác chỉ nhận thông báo. Tiếp tục kiểm tra quyền và trường từ DB ở BE. Quyền Hiệu trưởng phải khớp chính sách role của hệ thống, không suy ra quyền từ sidebar dùng chung. Hộp thông báo cá nhân là phần mở rộng riêng nếu đưa vào scope; không coi lịch sử toàn trường là hộp thư cá nhân.

### Các phase triển khai ban đầu và tiêu chí hoàn thành

Mỗi phase là luồng BE + FE có thể kiểm tra; giữ các tầng/folder hiện có. Những task trên 5 file phải tách thành contract/backend/frontend trước khi triển khai.

| Phase | Task và phụ thuộc | Tiêu chí nghiệm thu | Vị trí dự kiến |
| --- | --- | --- | --- |
| 1A | Giữ trường/tab/sự kiện trong URL; độc lập | Đổi tab, reload hoặc back/forward vẫn đọc đúng cấu hình đã lưu; URL trường không có quyền không tải dữ liệu | `features/emails/pages/EmailManagementPage.tsx`, `EmailConfiguration.tsx`, kiểm thử FE |
| 1B | Trả metadata mẫu đã ghim; sau 1A | Hiện đúng tên mẫu, số phiên bản, trạng thái; cảnh báo thay đổi chưa lưu khi đổi ngữ cảnh; không lấy dữ liệu cũ từ cache thay DB | DTO/service BE hiện có, `types/email.ts`, trang cấu hình |
| 1C | Xử lý tắt cấu hình; độc lập 1A | Cho phép tắt cấu hình đang có dù một target đã ngừng hoạt động; bật lại phải validate mẫu và người nhận; giữ version/audit | `EmailManagementService.cs`, `EmailRecipientResolver.cs`, form và test |
| 2A | Danh mục sự kiện trong DB; sau checkpoint 1 | Migration bổ sung giữ bốn mã ma trận, mẫu/cấu hình/lịch sử cũ; unique đúng phạm vi; phân biệt system/manual; hợp đồng biến có phiên bản | Entity/configuration dưới `Notification`, migration/DbContext hiện có, test migration |
| 2B | API danh mục và quyền; sau 2A | CRUD riêng trường, tìm/lọc/phân trang; code bất biến, tên/biến hợp lệ, chống trùng và stale version; đã sử dụng chỉ ngừng áp dụng | DTO/interfaces/services/controllers hiện có, test API |
| 2C | UI danh mục; sau 2B | Admin tạo và chọn được sự kiện “Họp chuyên môn”; hiển thị đúng nguồn kích hoạt; dropdown tìm/phân trang; PHT không thấy thao tác ngoài quyền | `features/emails` hiện có, `services/api.ts`, `types/email.ts` |
| 3A | Mẫu/cấu hình dùng danh mục; sau checkpoint 2 | Không còn validate theo danh sách bốn mã; FE/BE dùng cùng hợp đồng biến; ghim phiên bản mẫu/hợp đồng; template cũ vẫn render đúng | `EmailTemplateRules.cs`, DTO/service, utils/types/biên tập mẫu hiện có |
| 3B | Đường phát sự kiện nghiệp vụ chung; sau 3A | Ma trận tiếp tục enqueue trong cùng giao dịch; chống enqueue lặp theo business event key; nghiệp vụ mới tích hợp bằng service/interface hiện có | `IEmailNotificationService`, `EmailNotificationService`, các service producer và test |
| 4A | Gửi thủ công; sau checkpoint 3 | Chọn sự kiện/mẫu, nhập biến, xem trước nội dung và người nhận; nhóm cộng/trừ giữ đúng quy tắc; requestId chống gửi lặp HTTP; audit người gửi | DTO/service/controller hiện có, màn gửi dưới `features/emails`, test |
| 4B | Đặt lịch/hủy; sau 4A | Lịch tương lai, hiển thị múi giờ Việt Nam và lưu UTC; restart BE không mất lịch; gửi/hủy có version kiểm soát race; không hủy bản đã claim/gửi | Notification/config/migration cần thiết, service, UI và test lịch |
| 4C | Worker tôn trọng lịch; sau 4B | Cả chọn và claim đều yêu cầu đến hạn; worker song song không claim cùng bản; retry/lease vẫn đúng; kiểm tra lại người nhận trước SMTP | `EmailNotificationService.cs`, test worker/concurrency |
| 5A | Lịch sử phục vụ vận hành; sau checkpoint 4 | Phân trang/lọc sự kiện, thủ công/tự động, trạng thái, lịch; chi tiết kết quả và lý do lỗi an toàn; quyền theo trường | DTO/service, `EmailHistory.tsx`, central API/types, test |
| 5B | Kiểm tra tổng thể; sau 5A | Migration DB test từ bản hiện tại; regression matrix, cross-school/role, reload, biến sai, target inactive, send lặp, lịch/hủy, worker restart; smoke UI desktop/mobile và Gmail thực tế do người dùng thực hiện | Các test project sẵn có và README |

**Checkpoint sau phase 1, 2, 3 và 4:** focused tests và build sạch, kiểm tra trọn luồng tương ứng trước khi sang phase tiếp theo. Checklist tiến độ bên dưới đánh dấu phần code và kiểm thử tự động đã thực hiện; nghiệm thu giao diện thực tế và SMTP thật vẫn để riêng ở trạng thái chưa hoàn thành. Không thêm engine tự động hóa tổng quát, folder kiến trúc mới hoặc thư viện chỉ để giải quyết các task này. API FE tiếp tục tập trung ở `src/services/api.ts`.

### Quy tắc đặt lịch và bảo toàn lịch sử

- Khi xác nhận gửi/đặt lịch, lưu snapshot nội dung đã render, phiên bản mẫu/hợp đồng biến và tập người nhận đã xem trước. Nhóm được giải quyết tại thời điểm xác nhận; người mới vào nhóm sau đó không tự được thêm vào lần gửi cũ. Worker kiểm tra lại điều kiện người nhận khi đến hạn và bỏ qua người đã chuyển trường/ngừng hoạt động/đổi email.
- Đổi mẫu/cấu hình sau đó không sửa lần gửi đã xác nhận. Muốn đổi lịch/nội dung/người nhận: hủy lần gửi chưa được claim rồi tạo lần gửi mới có audit; không ghi đè thư đã gửi. Hủy phải atomic và từ chối khi đã bắt đầu gửi.
- Lịch đã đến hạn trong lúc BE tắt được xử lý khi BE chạy lại. Không cam kết gửi chính xác đến từng giây. Giữ cơ chế at-least-once hiện có, không tuyên bố SMTP đảm bảo exactly-once.
- Ngừng sự kiện/mẫu ngăn tạo lần gửi mới; lần gửi đã xác nhận giữ nguyên, muốn dừng phải hủy rõ ràng. Xóa chỉ khi chưa tham chiếu/sử dụng; lưu lịch sử khi đã dùng.

### Verification và rủi ro cần kiểm soát

Kiểm tra từng slice bằng test tương ứng trong project hiện có. Từ `TeLo-Backend`:

```powershell
dotnet test Tests/Application.Tests/Application.Tests.csproj
dotnet test Tests/Infrastructure.Tests/Infrastructure.Tests.csproj
dotnet test Tests/WebAPI.Tests/WebAPI.Tests.csproj
dotnet build WebAPI/WebAPI.csproj
```

Từ `TeLo-FrontEnd/TeLo-Frontend`: `npm run lint`, `npm run build` và kiểm tra browser trọn luồng lưu/reload/sự kiện tự tạo/gửi ngay/lịch/hủy. Test gửi dùng fake SMTP/database riêng; test Gmail thật chỉ dùng địa chỉ thật của người kiểm thử. Nếu BE đang chạy giữ DLL Debug, kiểm thử/build bằng `-c Release`; không cần dừng server của người khác.

| Rủi ro | Cách kiểm soát trong triển khai |
| --- | --- |
| Migration đổi enum/code làm hỏng ma trận cũ | Bổ sung schema và seed giữ nguyên mã; kiểm thử dữ liệu trước/sau trên DB riêng |
| Đổi biến khiến mẫu cũ không render | Phiên bản hợp đồng biến gắn với phiên bản mẫu; không sửa phá hợp đồng đã dùng |
| Worker gửi lịch tương lai sớm | Kiểm tra hạn ở cả query và atomic claim, test clock/restart/concurrency |
| Lấy người nhận/cấu hình của trường khác | BE authorize từng request và FK/query theo trường; test cross-school cho tất cả thao tác |
| “Lưu xong bị mất” do UI đổi ngữ cảnh | URL giữ trường/sự kiện/tab, dữ liệu GET từ DB, metadata rõ ràng và unsaved guard |
| Merge nhiều thành viên | Thay đổi nhỏ trong đúng tầng; phối hợp migration/snapshot/DI/central API; không tự commit/pull gây ghi đè |

Migration mới `20261006043838_AddEmailEventCatalogAndScheduling` đã áp dụng cho `sep` ngày 06/10/2026 sau sao lưu. Migration bổ sung bảng/cột, giữ bốn mã ma trận, sao chép hợp đồng biến vào các phiên bản mẫu cũ và phân loại email thử cũ. Không tự sửa bảng lịch sử EF hay xóa dữ liệu nghiệp vụ. Thành viên khác pull đầy đủ các migration rồi dùng connection của mình chạy `dotnet ef database update --project Infrastructure --startup-project WebAPI`.

## Tiến độ triển khai ngày 06/10/2026

- [x] Phase 1: giữ trường/tab/sự kiện trong URL; GET cấu hình có tên mẫu, phiên bản và trạng thái; cảnh báo khi đổi tab/trường/sự kiện trong màn email khi có thay đổi chưa lưu; tắt được cấu hình có target đã ngừng hoạt động mà không đổi các tham chiếu cũ.
- [x] Phase 2: danh mục `email_events` trong tầng Notification hiện có; CRUD sự kiện riêng của trường, quyền Admin, mã bất biến, trạng thái và optimistic concurrency; danh sách/picker tìm kiếm và phân trang trên server.
- [x] Phase 3: validation và render runtime theo danh mục DB. Khi lưu phiên bản mẫu, snapshot `variables_json` và `event_version`; sửa biến của sự kiện không làm hỏng mẫu đã ghim. Dùng snapshot trên phiên bản mẫu thay một bảng phiên bản sự kiện riêng để giữ mô hình nhỏ và bám cấu trúc hiện có.
- [x] Phase 4: gửi thủ công, xem trước nội dung/số địa chỉ nhận, đặt lịch giờ Việt Nam/lưu UTC, snapshot người nhận/nội dung, requestId chống gửi lặp HTTP, giới hạn 30 lần gửi thủ công/người/giờ. Hủy chỉ khi chưa claim; worker kiểm tra lịch và khóa cùng bản Notification với thao tác hủy.
- [x] Phase 5: lịch sử có loại gửi, lịch gửi, kết quả và hủy gửi; lọc/phân trang; BE kiểm tra quyền và trường cho các thao tác mới.
- [x] Migration local `sep`, sao lưu tại `C:\Users\admin\AppData\Local\Temp\sep_before_email_events_20261006_122248.sql`.
- [x] Kiểm thử API email trên DB riêng với fake SMTP: đọc lại cấu hình, tắt target inactive, CRUD/scope, thay hợp đồng biến, idempotency, lịch tương lai/đến hạn, hủy và worker đồng thời.
- [x] Regression BE: 133 Application + 21 Infrastructure + 21 WebAPI tests đạt, không skip. Kiểm thử bổ sung xác nhận thay địa chỉ người nhận sau preview bị từ chối trước khi tạo hàng đợi; biến số không được diễn giải `1,2` thành `12` hoặc `1-` thành `-1`.
- [x] Build FE và lint riêng phần email; 20 self-check FE gồm validation sự kiện, biến trùng/biến hệ thống, múi giờ và ngày lịch không tồn tại.
- [ ] Kiểm tra trực quan desktop/mobile và browser reload/back/forward thực tế: công cụ không có trình duyệt khả dụng trong phiên này.
- [ ] Gửi Gmail thật sau khi người dùng nhập SMTP và bật worker. Phiên phát triển không tự bật hoặc gửi email thật.

Lint toàn FE có lỗi ngoài phần email ở các màn trường/phân hiệu, profile và `any` cũ trong central API. Chưa sửa các phần này để giữ phạm vi công việc. Build có cảnh báo bundle lớn hơn 500 kB; không thay cấu hình chunk toàn ứng dụng trong phần email.

### Các bước sử dụng sau khi cập nhật

1. Khởi động lại BE để nạp code mới, chạy lại FE nếu cần. SMTP thực hiện theo hướng dẫn bên trên; chỉ bật sau khi nhập đúng tài khoản gửi và mật khẩu ứng dụng.
2. Chọn đúng trường. Mở **Sự kiện → Thêm sự kiện**. Ví dụ mã `STAFF_MEETING`, tên “Họp chuyên môn”, thêm biến `topic` kiểu văn bản/bắt buộc và `meetingDate` kiểu ngày/bắt buộc. Mã gồm 2–94 ký tự để chừa chỗ cho mã cấu hình; tối đa 20 biến riêng, tên 1–32 ký tự, không trùng `schoolName`, `actorName`, `actionUrl`.
3. Mở **Mẫu email → Tạo mẫu**, chọn sự kiện vừa tạo. Ví dụ tiêu đề `Mời họp: {{topic}}`, nội dung `{{schoolName}} thông báo họp ngày {{meetingDate}}. Người gửi: {{actorName}}`. Tiêu đề tối đa 200 ký tự, nội dung mẫu tối đa 10.000 ký tự; chỉ dùng biến thuộc hợp đồng hiện tại.
4. Mở **Cấu hình thông báo**, chọn đúng sự kiện và phiên bản mẫu. Chọn nhóm vai trò/cá nhân nhận và loại trừ; xem trước rồi bật/lưu cấu hình. Quay lại tab này sẽ giữ trường/sự kiện đã chọn; tên và số phiên bản mẫu được đọc từ DB.
5. Mở **Gửi thông báo**, chọn sự kiện thủ công đang áp dụng, nhập biến theo phiên bản mẫu đã ghim. Chọn **Gửi ngay** hoặc **Đặt lịch**; lịch tương lai tối đa một năm, hiển thị rõ giờ Việt Nam (UTC+7). Giá trị mỗi biến tối đa 2.000 ký tự; URL chỉ HTTP/HTTPS không chứa tài khoản/mật khẩu; ngày dạng `yyyy-MM-dd`, số dùng dấu chấm thập phân.
6. Bấm **Xem trước và xác nhận**, kiểm tra nội dung, số địa chỉ nhận và thời điểm rồi xác nhận. Danh sách người nhận dùng cấu hình đã lưu; muốn đổi người nhận cần sửa cấu hình trước. Một địa chỉ hợp lệ chỉ có một bản gửi trong lần thông báo.
7. Xem **Lịch sử gửi**, lọc thủ công/tự động/email thử và trạng thái. Lần gửi chưa bắt đầu có nút **Hủy gửi**. Khi đã claim/gửi, BE từ chối hủy; tải lại lịch sử để xem trạng thái mới. Nếu muốn đổi nội dung/lịch: hủy lần chưa bắt đầu rồi tạo lần gửi mới.

Sửa biến của sự kiện tạo hợp đồng hiện tại mới nhưng không đổi hợp đồng trên phiên bản mẫu đã lưu. Nếu cần dùng biến mới, sửa/tạo mẫu để có phiên bản mới rồi chủ động ghim phiên bản đó vào cấu hình. Các mẫu cũ vẫn dùng biến cũ; không tự đổi tất cả cấu hình.

### Contract API mới và thay đổi liên quan

Các endpoint nằm dưới `/api/schools/{schoolId}/emails`, dùng ApiResponse và actor từ token; FE chỉ gọi qua `src/services/api.ts`.

| Endpoint | Contract / hành vi |
| --- | --- |
| `GET events` | Trả `DirectoryPage<EmailEventItem>` thay mảng cũ; `search`, `status`, `page`, `pageSize` (1–100). FE đã cập nhật cùng contract; không lấy hết danh mục không giới hạn. |
| `GET events/{code}` | Đọc đúng một sự kiện hệ thống hoặc của trường; không đọc sự kiện riêng trường khác. |
| `POST events`, `PUT events/{code}` | Admin tạo/sửa sự kiện MANUAL của trường, `code/name/description/version/variableDefinitions`. Sửa mã bị từ chối; cập nhật phải truyền version. |
| `PATCH events/{code}/status`, `DELETE events/{code}?version=...` | Chỉ thao tác sự kiện riêng trường. Sự kiện đã sử dụng không xóa, chỉ ngừng áp dụng. Sự kiện hệ thống không sửa/xóa bằng các API này. |
| `GET configuration/{code}` | Bổ sung `templateName`, `revision`, `templateStatus`, `variableDefinitions` của phiên bản mẫu đang ghim. |
| `GET delivery-status` | Chỉ trả `enabled` và `smtpConfigured`, không trả email/password/secret SMTP. Đây là kiểm tra cấu hình, không xác nhận SMTP đăng nhập thành công. |
| `POST send/preview` | `eventCode/configVersion/requestId/values/scheduledFor`; kiểm tra hợp đồng mẫu, cấu hình và lịch, trả nội dung render + số địa chỉ nhận hợp lệ sau loại trừ/trùng địa chỉ + `fingerprint` của nội dung/người nhận. |
| `POST send` | Cùng payload preview, FE bổ sung `previewFingerprint` từ kết quả xem trước. Admin/PHT được phép trong trường; chỉ sự kiện MANUAL đang áp dụng. Gửi lặp cùng requestId và cùng payload không tạo thêm Notification; payload khác cùng ID trả 409. |
| `POST history/{id}/cancel` | Body `{ "version": 1 }`; hủy atomic khi chưa bắt đầu, giữ lịch sử, xung đột trạng thái/version trả 409. |
| `GET history` | Bổ sung lọc `sendKind=MANUAL/AUTOMATIC/TEST`; kết quả có `scheduledFor/sendKind/version/canCancel`. |

`scheduledFor` truyền ISO 8601 có offset hoặc UTC; FE chuyển giờ Việt Nam sang UTC trước gửi. Thay đổi cấu hình giữa xem trước và xác nhận gây `STALE_VERSION` và phải tải lại, không tự gửi theo cấu hình vừa bị người khác đổi. Nếu nội dung đã render hoặc người nhận/email thay đổi, `previewFingerprint` khác kết quả hiện tại gây `STALE_PREVIEW` (409), cần xem trước lại. FE luôn gửi fingerprint; API cho phép client khác bỏ trường này để gửi trực tiếp nhưng khi đó không có bước bảo vệ đối chiếu preview. Thiếu SMTP/worker chưa bật trả 503; dữ liệu sai trả 422; ngoài phạm vi quyền trả 403.

### Thêm sự kiện tự động cho nghiệp vụ mới

Admin tạo sự kiện thủ công đã dùng được ngay cho thông báo ngoài ma trận. Với sự kiện tự động mới, nhóm phát triển cần:

1. Đăng ký một định nghĩa SYSTEM cùng biến trong migration/seed ở tầng Notification; giữ mã ổn định và chỉ đăng ký khi có nghiệp vụ phát sinh thật.
2. Trong giao dịch nghiệp vụ hiện có, gọi `IEmailNotificationService.QueueEventAsync(schoolId, branchId, eventCode, eventKey, actor, values, ct)`. `eventKey` phải ổn định cho một lần chuyển trạng thái để retry không tạo thêm thông báo. Service yêu cầu transaction đang mở và chỉ gửi sự kiện SYSTEM đang áp dụng theo cấu hình trường.
3. Viết test rollback nghiệp vụ cũng rollback hàng đợi, retry cùng eventKey không tạo thêm, đúng trường và biến đã cung cấp. Không tự suy ra “đã hoàn tất” bằng cách cho Admin nhập SQL/script.

Bốn luồng ma trận đang dùng cùng đường enqueue này. Hộp thông báo cá nhân/đánh dấu đã đọc và SMTP riêng từng trường vẫn là phạm vi mở rộng riêng, chưa được bổ sung trong đợt này.
