# Kế hoạch test API: Danh bạ lớp/học sinh + Import Excel

Phạm vi: backend, chạy trên DB dev `sep_smoke` (đã áp đủ 12 migration).
Chạy API: `dotnet run --project WebAPI -c Release` (môi trường Development), Swagger tại `/swagger`.

## 0. Chuẩn bị

**Tài khoản (mỗi role một token JWT):**

| Ký hiệu | Role | Quyền |
|---|---|---|
| ADMIN | `OperationalAdmin` | Đọc + CRUD + duyệt import, theo `schoolId` trong route |
| HT | `HIEU_TRUONG` / `PRINCIPAL` | Xem + import + gửi duyệt (trường mình) |
| PHT | `PHT` | Như HT |
| GV | `GIAO_VIEN` / `TEACHER` | Chỉ xem |
| TT | `TEAM_LEAD` / `TO_TRUONG` | Chỉ xem |
| ANON | không token | — |

**Dữ liệu tối thiểu:** 2 trường (A, B) mỗi trường ≥1 cơ sở; 1 năm học ACTIVE; ở trường A các lớp `C6A`, `C6B` (ACTIVE) và 1 lớp INACTIVE; trường B có lớp `C6A` (cùng mã, khác trường); vài học sinh mã `HS001..HS003` ở trường A; 1 kỳ thi có điểm đã công bố cho HS001. Mỗi user HT/PHT/GV/TT gắn vào chi nhánh của trường A; thêm 1 HT thuộc trường B.

**File Excel mẫu (tạo bằng tay):**
- F-OK: 3 dòng hợp lệ (mã mới `N001..N003`, lớp `C6A`).
- F-MIX: 2 dòng đúng + dòng trùng mã hệ thống (`HS001`) + mã lớp sai + ngày sinh sai (`31/02/2010`) + mã trùng dòng trước trong file + thiếu họ tên.
- F-BIG: >1000 dòng. F-HUGE: file >2 MB. F-XLS: đuôi `.xls`/`.csv`. F-EMPTY: chỉ có header. F-FORMULA: tên có `=1+1`.

Quy ước: mã lỗi trả trong `ApiResponse` (`errorCode`). Status HTTP viết đậm.

---

## 1. Ma trận quyền truy cập (chạy trước tiên)

| # | Endpoint | ANON | GV | TT | HT/PHT | ADMIN |
|---|---|---|---|---|---|---|
| 1.1 | `GET /api/students`, `/api/students/{id}`, `/{id}/scores` | **401** | 200 | 200 | 200 | **403** |
| 1.2 | `GET /api/classes`, `/reference-data`, `/{id}` | **401** | 200 | 200 | 200 | **403** |
| 1.3 | `GET /api/student-imports/template`, `/template.xlsx` | **401** | **403** | **403** | 200 | **403** |
| 1.4 | `POST/GET /api/student-imports`, `/{id}`, `/{id}/file.xlsx`, `/{id}/submit`, `DELETE /{id}` | **401** | **403** | **403** | 200 (hợp lệ) | **403** |
| 1.5 | `/api/admin/schools/{sid}/students…` (GET/POST/PUT/DELETE/transfer-class) | **401** | **403** | **403** | **403** | 200/201 |
| 1.6 | `/api/admin/schools/{sid}/classes…` | **401** | **403** | **403** | **403** | 200/201 |
| 1.7 | `/api/admin/schools/{sid}/student-imports…` (kể cả `apply`, `reject`) và `GET /api/admin/student-imports` | **401** | **403** | **403** | **403** | 200 |

Expected: chỉ đúng role mới qua; role sai luôn 403 (không lộ dữ liệu), không token luôn 401.

---

## 2. Kịch bản theo role

### 2A. ANON
| # | Bước | Expected |
|---|---|---|
| A1 | Gọi bất kỳ endpoint nào không kèm token | **401** |
| A2 | Token hết hạn / sai chữ ký | **401** |

### 2B. GV / TT (chỉ xem)
| # | Bước | Expected |
|---|---|---|
| G1 | `GET /api/classes` | 200, chỉ lớp thuộc trường của user; có `studentCount` |
| G2 | `GET /api/classes?gradeLevelId=&status=&search=` | 200, lọc đúng; **không gửi `status` thì trả cả ACTIVE lẫn INACTIVE** (thiết kế: admin cần thấy lớp đã xóa mềm; muốn ẩn thì `status=ACTIVE`) |
| G3 | `GET /api/classes/reference-data` | 200, danh sách khối/năm/cơ sở của trường mình |
| G4 | `GET /api/classes/{id}` lớp trường mình | 200, có roster (mọi enrollment, gồm cả `TRANSFERRED_OUT`) |
| G5 | `GET /api/classes/{id}` lớp trường B | **404** (không lộ tồn tại) |
| G6 | `GET /api/students` (có phân trang, `search`, `classId`, `status`) | 200, chỉ học sinh trường mình; không gửi `status` thì trả mọi trạng thái, `status=INACTIVE` để xem học sinh đã xóa mềm |
| G7 | `GET /api/students/{id}` | 200, có lớp hiện tại + lịch sử lớp (mới nhất trước) |
| G8 | `GET /api/students/{id}` học sinh trường B | **404** |
| G9 | `GET /api/students/{id}/scores` | 200, chỉ điểm đã **công bố** |
| G10 | `GET /api/students/{idKhôngTồnTại}`, `id=0` | 404 / 404 (route `min(1)`) |
| G11 | `POST/PUT/DELETE` bất kỳ route học sinh/lớp; mọi route import | **403** hoặc **405** |

### 2C. HT / PHT (xem + import + gửi duyệt)
**Xem** — giống G1–G10 (mọi kết quả chỉ trong trường của mình).

**Tải mẫu**
| # | Bước | Expected |
|---|---|---|
| H1 | `GET /api/student-imports/template?academicYearId=` | 200 JSON: 6 cột, định dạng ngày/giới tính, danh sách mã lớp ACTIVE **của trường mình** |
| H2 | `GET /api/student-imports/template.xlsx?academicYearId=` | 200, content-type `…spreadsheetml.sheet`, `Content-Disposition` có `.xlsx`; mở được, 2 sheet `HocSinh` + `HuongDan`, header đúng thứ tự, sheet 2 liệt kê mã lớp |
| H3 | Tên lớp bắt đầu bằng `=`, tải mẫu | Ô sheet 2 bị escape, không thành công thức |
| H4 | `academicYearId` không tồn tại | 404/422 |

**Upload + xem trước**
| # | Bước | Expected |
|---|---|---|
| H5 | POST multipart `file=F-OK` | 200/201, batch `DRAFT`, `source=SCHOOL`, `totalRows=3, validRows=3, invalidRows=0`, dòng có `resolvedSchoolClassId` |
| H6 | POST `F-MIX` | 200, `validRows=2, invalidRows=4`; mỗi dòng lỗi có `errors[{field,message}]` đúng trường: `code` (trùng hệ thống / trùng trong file — đánh dấu **dòng thứ hai**), `classCode`, `dateOfBirth`, `fullName`; **không dừng ở lỗi đầu** |
| H7 | `F-BIG` (>1000 dòng) | **422** `IMPORT_FILE_INVALID` |
| H8 | `F-HUGE` (>2 MB) | Bị chặn trước service: 400/413, hoặc **connection reset** nếu file lớn hơn nhiều mức cap 2,5 MB (Kestrel đóng kết nối khi client còn đang gửi). File 2–2,5 MB → 422 |
| H9 | `F-XLS` / file rỗng / không phải xlsx | **422** `IMPORT_FILE_INVALID` |
| H10 | `F-EMPTY` (chỉ header) | 422 `IMPORT_FILE_INVALID` hoặc batch 0 dòng — ghi nhận thực tế |
| H11 | Request không có phần `file` | **422**; multipart rỗng hoàn toàn: **400** |
| H12 | `F-FORMULA` | Ô tên được lưu nguyên văn, không thực thi |
| H13 | Mã lớp `C6A` — HT trường A vs HT trường B | Mỗi bên resolve vào lớp của **trường mình** |

**Xem batch**
| # | Bước | Expected |
|---|---|---|
| H14 | `GET /api/student-imports` | 200, chỉ batch của trường mình |
| H15 | `GET /api/student-imports/{id}?onlyInvalid=true` | 200, chỉ dòng lỗi |
| H16 | `GET /api/student-imports/{id}/file.xlsx` | 200, đúng file gốc đã upload (byte-for-byte) |
| H17 | `GET …/{id}` của trường B | **404** |

**Vòng đời**
| # | Bước | Expected |
|---|---|---|
| H18 | `POST …/{draft}/submit` (batch 0 dòng lỗi) | 200, `SUBMITTED` |
| H19 | `submit` batch có dòng lỗi | ghi nhận: 200 (admin vẫn không apply được) hoặc 409 `IMPORT_HAS_INVALID_ROWS` — theo đúng code hiện tại |
| H20 | `submit` lần 2 / batch đã SUBMITTED | **409** `IMPORT_BATCH_STATE_INVALID` |
| H21 | `DELETE …/{draft}` | 200, `CANCELLED`; sau đó submit → 409 |
| H22 | `DELETE` batch đã SUBMITTED/APPLIED | **409** |
| H23 | Batch bị REJECTED: xem chi tiết | thấy `reviewComment` (lý do admin); upload lại tạo batch mới |
| H24 | HT gọi `apply`/`reject` (route admin) | **403** |
| H25 | HT gọi POST/PUT/DELETE học sinh/lớp/transfer-class (route admin) | **403** |
| H26 | Sau khi admin APPLY: `GET /api/students`, `/api/classes/{id}` | học sinh mới xuất hiện, sĩ số lớp tăng |

### 2D. ADMIN
**Đọc (mọi trường theo `schoolId`)**
| # | Bước | Expected |
|---|---|---|
| D1 | `GET /api/admin/schools/{A}/classes`, `/students`, các route chi tiết, `/scores`, `reference-data` | 200 |
| D2 | `schoolId` không tồn tại / class-student thuộc trường khác | **404** |
| D3 | `schoolId=0` | 404 (route `min(1)`) |

**CRUD học sinh**
| # | Bước | Expected |
|---|---|---|
| D4 | `POST …/students` hợp lệ (`code, fullName, admissionDate, schoolClassId,…`) | 201, có enrollment ACTIVE |
| D5 | POST trùng mã đang hoạt động | **409** (duplicate) |
| D6 | POST thiếu trường bắt buộc / ngày sinh tương lai / gender lạ / lớp INACTIVE hoặc trường khác | **422** (lớp sai: 404 `ClassNotFound`) |
| D7 | `PUT …/students/{id}` đổi tên/ngày sinh | 200, giữ nguyên enrollment |
| D8 | `PUT` đổi `schoolClassId` sang lớp khác | **422**, hướng dẫn dùng `transfer-class` (lịch sử không bị ghi đè) |
| D9 | `PUT` học sinh chưa có lớp trong năm kèm `schoolClassId` | 200, tạo enrollment |
| D10 | `DELETE …/students/{id}` | 200, `INACTIVE` (xóa mềm), biến mất khỏi danh sách mặc định |
| D11 | Tạo mới học sinh dùng lại mã của em đã INACTIVE | 201 (mã chỉ unique giữa học sinh chưa xóa) |
| D12 | `PUT` kích hoạt lại em INACTIVE khi mã đã bị em khác dùng | **409** (không văng lỗi 500) |

**CRUD lớp**
| # | Bước | Expected |
|---|---|---|
| D13 | `POST …/classes` hợp lệ | 201 |
| D14 | POST trùng mã lớp trong (cơ sở, năm) | **409** |
| D15 | `PUT …/classes/{id}` | 200 |
| D16 | `DELETE` lớp còn học sinh ACTIVE | **409** |
| D17 | `DELETE` lớp trống | 200, `INACTIVE` |

**Chuyển lớp (giữa năm)**
| # | Bước | Expected |
|---|---|---|
| D18 | `POST …/students/{id}/transfer-class {schoolClassId:6B, effectiveOn}` | 200; enrollment 6A → `TRANSFERRED_OUT` + `endedOn`; 6B `ACTIVE` + `startedOn`; lịch sử hiển thị cả hai |
| D19 | Sĩ số: `GET classes` sau chuyển | 6A −1, 6B +1, **không đếm trùng**; lọc theo lớp chỉ ra em ở lớp mới |
| D20 | `GET …/students/{id}/scores` theo lớp cũ và lớp mới | **cùng** danh sách điểm (điểm đi theo học sinh) |
| D21 | Chuyển vào chính lớp đang học | 200, không tạo dòng mới (idempotent) |
| D22 | `effectiveOn` < ngày bắt đầu enrollment hiện tại | **422** |
| D23 | Lớp đích INACTIVE / khác trường / không tồn tại | **404** |
| D24 | Học sinh chưa có enrollment trong năm của lớp đích | **409/422** `StudentNotEnrolledInYear` |
| D25 | Sau chuyển hết học sinh khỏi lớp cũ → `DELETE` lớp cũ | 200 (lớp đã trống) |

**Chuyển trường**
| # | Bước | Expected |
|---|---|---|
| D26 | `DELETE` học sinh ở trường A, rồi import/tạo cùng mã ở trường B | thành công (mã được giải phóng) |
| D27 | Trường B tạo mã đang còn hoạt động ở trường A | **409** |

**Import trực tiếp (admin)**
| # | Bước | Expected |
|---|---|---|
| D28 | `GET …/student-imports/template` và `template.xlsx` | 200 (như H1/H2, theo `schoolId`) |
| D29 | POST `F-OK` | batch `DRAFT`, `source=ADMIN`, 0 lỗi |
| D30 | `POST …/{id}/apply` | 200, `APPLIED`; học sinh + enrollment ACTIVE được tạo, mỗi dòng có `createdStudentId` |
| D31 | Apply lần 2 | **409** `IMPORT_BATCH_STATE_INVALID`, không sinh thêm học sinh |
| D32 | Apply `F-MIX` (có dòng lỗi) | **409** `IMPORT_HAS_INVALID_ROWS`, không ghi gì |
| D33 | Apply khi giữa chừng có người tạo tay trùng mã | **409**, rollback toàn bộ, `details` có số dòng vi phạm |
| D34 | Hai request apply đồng thời cùng batch | Đúng một cái 200, cái kia 409; không nhân đôi học sinh |
| D35 | `DELETE …/{draft}` | 200, `CANCELLED`; apply sau đó → 409 |

**Duyệt batch trường gửi lên**
| # | Bước | Expected |
|---|---|---|
| D36 | `GET /api/admin/student-imports` (hộp thư) | 200, các batch SUBMITTED của mọi trường (có lọc theo trường/trạng thái nếu hỗ trợ) |
| D37 | `GET …/{id}/file.xlsx` batch của trường gửi | 200, đúng file gốc |
| D38 | `POST …/{id}/reject {comment}` | 200, `REJECTED`, lưu người duyệt + lý do |
| D39 | reject thiếu lý do / >1000 ký tự | **422** |
| D40 | reject batch DRAFT / đã APPLIED | **409** |
| D41 | Sau reject: apply | **409** |
| D42 | apply batch SUBMITTED hợp lệ | 200, `APPLIED`, HT thấy học sinh mới ở `/api/students` |
| D43 | Route `schoolId=B` nhưng `batchId` thuộc trường A | **404** |

---

## 3. Kịch bản end-to-end (phối hợp role)

1. **Luồng duyệt thành công:** HT tải mẫu (H2) → upload F-OK (H5) → xem trước → submit (H18) → ADMIN thấy trong hộp thư (D36), tải file gốc (D37) → apply (D42) → HT/GV thấy học sinh mới, sĩ số lớp tăng.
2. **Luồng từ chối & gửi lại:** HT upload F-MIX → sửa file → upload F-OK mới → submit → ADMIN reject (D38) → HT đọc lý do (H23) → HT upload lại → submit → ADMIN apply → 200. Apply lần hai → 409.
3. **Chuyển lớp giữa năm:** ADMIN transfer HS001 6A→6B (D18) → GV xem chi tiết HS001: lịch sử 2 dòng, lớp hiện tại 6B; điểm không đổi (D20); sĩ số đúng (D19).
4. **Chuyển trường:** ADMIN xóa HS002 ở trường A (D10) → HT trường B upload file có mã `HS002` → submit → ADMIN apply → HS002 thuộc trường B; HT trường A không còn thấy em ở danh sách mặc định.
5. **Cô lập trường:** HT trường B gọi mọi `GET` với id của trường A → luôn 404; upload mã lớp `C6A` chỉ resolve về lớp trường B.

## 4. Kiểm tra không chức năng

| # | Kiểm tra | Expected |
|---|---|---|
| N1 | Upload 1000 dòng hợp lệ | Preview ≤ vài giây, chỉ ~2 truy vấn tra cứu (không N+1) |
| N2 | Apply 1000 dòng | Một transaction, toàn bộ hoặc không gì cả |
| N3 | Ô Excel dạng ngày và dạng chuỗi `15/03/2010` | Cho cùng kết quả |
| N4 | Định dạng ngày `d/M/yyyy`, `yyyy-MM-dd` | Được chấp nhận |
| N5 | Log server sau toàn bộ kịch bản | Không có exception 500 chưa xử lý (đặc biệt D12) |
| N6 | Swagger | Đủ route import (trường + admin), transfer-class |

## 5. Dọn dữ liệu sau test

Xóa học sinh/lớp/batch tạo trong test (mã tiền tố `N…`, `TEST…`) để không làm bẩn `sep_smoke`; `student_import_rows` xóa cascade theo batch.

## 6. Ghi chú lệch so với kế hoạch gốc

- Upload quá lớn trả **400** (không phải 413) vì MVC bọc lỗi Kestrel.
- Một mã lỗi chung `IMPORT_FILE_INVALID` cho file sai định dạng / quá nhiều dòng / quá lớn.
- Thêm ngoài kế hoạch: `GET …/template` (JSON) và hộp thư admin `GET /api/admin/student-imports`.
- Các dòng "hoặc/ghi nhận" (H10, H19) là chỗ hành vi chưa được test tự động cố định — hãy ghi lại kết quả thật khi chạy.
