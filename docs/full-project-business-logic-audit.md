# Audit toàn bộ tính năng và logic nghiệp vụ PRN222_FINAL

Ngày audit: 2026-07-14  
Phạm vi: toàn bộ source local tại `C:\PRN222_FINAL` (Web, BLL, DAL, test và cấu hình).  
Nguyên tắc: chỉ báo cáo điều có bằng chứng trong code; không thay đổi application code và không gọi thật cổng thanh toán/email/AI.

## Trạng thái xử lý sau audit

Cập nhật 2026-07-14 theo danh sách 15 lỗi đã tóm tắt cho người dùng:

- **Giữ nguyên theo yêu cầu:** mục 1, 2, 4, 5 (tương ứng F-01, F-02, F-11, F-12).
- **Đã xử lý:** mục 3 và 6–15. Các thay đổi gồm private document storage, callback origin động, bảo toàn thời gian subscription, atomic FREE claim, khóa giao dịch và unique invariant cho subscription, doanh thu theo `PaidAt`, thu hẹp quyền mutate tài liệu của Admin, chống SSRF, rate-limit/lockout/suspend account, durable email outbox không chứa plaintext password và row-level user persistence.
- Phần findings bên dưới là **snapshot trước khi sửa**, được giữ lại làm bằng chứng và lịch sử quyết định.

## Kết luận điều hành

Hệ thống **build được và 49/49 test BLL hiện có đều pass**, nhưng chưa an toàn để đưa cấu hình hiện tại lên Internet. Hai việc cần xử lý ngay là:

1. Thu hồi/rotate toàn bộ secret đã commit trong `Web/appsettings.json`.
2. Xóa cơ chế tài khoản Admin/mật khẩu mặc định có thể đoán và việc tự đặt lại mật khẩu seed Admin.

Ngoài ra có các sai lệch nghiệp vụ đáng chú ý: assignment môn học của sinh viên không giới hạn phạm vi RAG; đổi mã môn không cascade sang document/chunk; file tài liệu nằm dưới `wwwroot`; Google OAuth bị cố định về localhost; chưa có cơ chế khóa tài khoản; dữ liệu doanh thu được quy về ngày tạo payment thay vì ngày trả tiền.

### Tổng hợp mức độ

| Mức | Số finding | Ý nghĩa |
|---|---:|---|
| P0 | 2 | Xử lý ngay trước mọi lần deploy/chia sẻ repo |
| P1 | 17 | Có thể gây takeover, lộ dữ liệu, sai quyền hoặc sai dữ liệu cốt lõi |
| P2 | 17 | Sai lệch chức năng, độ bền, hiệu năng hoặc hardening |
| P3 | 4 | Rủi ro thấp/quality gap |

## Inventory tính năng và luồng nghiệp vụ

### Tài khoản và phân quyền

- Role: Student, Lecturer, Admin; cookie authentication; cookie được kiểm tra lại user/role mỗi request.
- Trang register công khai hiện bị vô hiệu hóa; account được Admin tạo/import.
- Local login bằng email/password; Google OAuth chỉ cho email đã được provision trước.
- Forgot/reset/change password; email welcome chạy background, forgot-password hiện gửi đồng bộ.
- Admin quản lý user, phân role lúc tạo, import hàng loạt, gán Lecturer/Student vào subject, xóa user có điều kiện.

### Môn học, tài liệu và RAG/chat

- Catalog subject/chapter, subject leader, teaching lecturers, enrolled students, trạng thái active/inactive.
- Lecturer upload file hoặc URL, index nền, chunk, embedding, re-index, preview/edit/delete tài liệu.
- Chat RAG có session, rename/star/delete, citation, subject filter, grounding/fallback, quota theo gói/tháng.
- SignalR báo tiến độ index và danh sách user online.

### Gói, subscription và payment

- Danh sách gói, Free/paid subscription, giới hạn câu hỏi, ngày bắt đầu/kết thúc.
- Checkout MoMo/PayOS, return URL, webhook, payment history.
- Admin “phát hành giá mới”, bắt buộc lý do, optimistic check giá cũ, lịch sử thay đổi và thông báo 3 ngày.
- UI/service ngăn mua gói thấp hơn gói hiện tại; payment giữ snapshot số tiền lúc tạo.

### Admin/analytics/settings

- Dashboard users/subjects; thống kê chat, tài liệu, doanh thu, subscription; sinh recommendation.
- Admin chọn provider/model chat, embedding model/dimension, chunk size/overlap; cấu hình lưu JSON runtime.
- System notification cho thay đổi giá và đổi subject trong cửa sổ ba ngày.

## Findings chi tiết

### P0 — Critical

#### F-01 — Secret thật được commit vào repository

- **Evidence:** `Web/appsettings.json:13` có DB superuser credential; `:18-19` Google client secret; `:35-37` Gemini key; `:45-52` Gmail/app password; `:54-67` MoMo và PayOS credential. `git ls-files Web/appsettings.json` xác nhận file được track.
- **Impact:** người có repo/history có thể truy cập DB, gửi email, tiêu quota AI, giả mạo/tác động payment integration. Xóa secret ở commit mới không xóa khỏi lịch sử Git.
- **Điều kiện tái hiện:** đọc file hoặc lịch sử Git; endpoint MoMo trong cấu hình là production.
- **Khuyến nghị:** revoke/rotate **tất cả** credential ngay; purge khỏi Git history; chỉ commit placeholder; inject qua environment/User Secrets/secret manager; tạo DB user least-privilege, không dùng superuser.

#### F-02 — Admin mặc định có mật khẩu đoán được và mật khẩu seed bị tự khôi phục

- **Evidence:** `BLL/Services/Accounts/UserAccountService.cs:47-52,562-565,676-715` hard-code `admin@gmail.com / 123456 / Admin`; `Web/Program.cs:267-273` kích hoạt load/bootstrap lúc startup. `Web/appsettings.json:22-27` đặt seed password `123456`; `UserAccountService.cs:617-674` so sánh rồi ghi password hash về password cấu hình ở mỗi lần load.
- **Impact:** takeover toàn hệ thống bằng credential biết trước; Admin đổi/reset password thành công nhưng password cũ có thể hoạt động lại sau request tiếp theo.
- **Tái hiện:** đổi mật khẩu seed Admin, phát sinh một request load users, sau đó đăng nhập lại bằng password cấu hình cũ.
- **Khuyến nghị:** bỏ `FixedAccounts`; bootstrap đúng một lần khi chưa có Admin bằng secret mạnh cấp ngoài band; không bao giờ reconcile password sau khi tạo; bắt đổi mật khẩu lần đầu và nên có MFA.

### P1 — High

#### F-03 — Google Login chỉ hoạt động trên chính máy localhost

- **Evidence:** `Web/appsettings.json:17` đặt `PublicOrigin=http://localhost:9999`; `Web/Pages/Account/Login.cshtml.cs:95-147` và `Web/Program.cs:179-192` chủ động rebase `redirect_uri` về origin này.
- **Impact:** truy cập qua LAN/domain/server sẽ bị trình duyệt đưa về localhost của người dùng; production vẫn dùng HTTP nên Google login thất bại hoặc không an toàn.
- **Tái hiện:** mở app bằng IP/domain khác localhost và bấm Google Login.
- **Khuyến nghị:** cấu hình canonical HTTPS origin theo environment, đăng ký chính xác `<origin>/signin-google` trong Google Console; xử lý forwarded headers từ proxy tin cậy thay vì rewrite thủ công cứng.

#### F-04 — Password seed/welcome dùng chung và queue giữ plaintext credential

- **Evidence:** `Web/Pages/Admin/Index.cshtml.cs:188-215` dùng chung password `12345678` cho import; `BLL/Services/Jobs/AccountEmailJobQueue.cs:6-13,30-45` đưa plaintext password vào unbounded in-memory channel; `Web/Services/AccountEmailWorker.cs:35-59` chỉ retry ba lần rồi bỏ.
- **Impact:** người biết email có thể race-login; credential nằm trong RAM; restart làm mất job và user không nhận được thông tin truy cập.
- **Tái hiện:** import user, restart trước khi worker gửi hoặc làm SMTP lỗi ba lần.
- **Khuyến nghị:** không gửi password; dùng activation/set-password token một lần, unique; durable transactional outbox, bounded retry, dead-letter và idempotency.

#### F-05 — Không rate-limit/lockout và login làm lộ account tồn tại

- **Evidence:** `Web/Pages/Account/Login.cshtml.cs:66-92`, `ForgotPassword.cshtml.cs:37-70`; `Web/Program.cs` không đăng ký/dùng rate limiter. Login trả thông điệp khác cho account chưa provision (`Login.cshtml.cs:74-79`) và sai password (`:82-86`).
- **Impact:** credential stuffing, brute force, email enumeration và abuse SMTP/reset-token.
- **Tái hiện:** gửi lặp login/reset; không có 429/lockout.
- **Khuyến nghị:** limiter theo IP + account, exponential backoff/temporary lock, lỗi login đồng nhất, log/audit sự kiện bảo mật.

#### F-06 — Reset password có thể tạo link theo Host do client cung cấp

- **Evidence:** `Web/Pages/Account/ForgotPassword.cshtml.cs:47-51` dựng absolute URL từ `Request.Scheme`/request host; `Web/appsettings.json:70` đặt `AllowedHosts=*`.
- **Impact:** email nạn nhân có thể chứa link domain attacker kèm reset token.
- **Tái hiện:** POST forgot-password với Host header giả trong môi trường proxy/server chấp nhận header đó.
- **Khuyến nghị:** strict `AllowedHosts`; build URL từ canonical `PublicOrigin` đã validate; allowlist forwarded hosts/proxies.

#### F-07 — Đổi/reset password không thu hồi cookie đã bị đánh cắp

- **Evidence:** cookie persistent bảy ngày tại `Login.cshtml.cs:150-168` và `GoogleCallback.cshtml.cs:67-75`; `UserAccountService.cs:434-438,472-476` cập nhật `PasswordChangedAt`, nhưng `Web/Program.cs:67-137` chỉ đối chiếu id/name/role, không kiểm tra security stamp/password time.
- **Impact:** session bị đánh cắp vẫn dùng được sau khi chủ tài khoản đổi/reset password.
- **Tái hiện:** giữ cookie ở browser A, đổi password ở B, cookie A vẫn qua `OnValidatePrincipal`.
- **Khuyến nghị:** security stamp/session version claim; revoke cookie phát hành trước `PasswordChangedAt`; quản lý/revoke session.

#### F-08 — Không thể khóa ngay tài khoản bị compromise

- **Evidence:** model/service không có `IsActive/Suspended`; role change bị vô hiệu tại `Web/Pages/Admin/Index.cshtml.cs:633-637`; xóa yêu cầu inactive hơn ba tháng tại `UserAccountService.cs:319-366`.
- **Impact:** Admin không có thao tác khẩn cấp để chặn account đang bị chiếm; cookie tiếp tục hợp lệ.
- **Tái hiện:** account vừa hoạt động không thể delete và không có suspend action.
- **Khuyến nghị:** thêm suspended/revoked state, lý do + audit trail; kiểm tra state trong login và cookie validation.

#### F-09 — Repository user xóa rồi chèn lại toàn bộ bảng cho mọi mutation

- **Evidence:** `DAL/Repositories/Accounts/PostgresUserAccountRepository.cs:30-47` thực hiện `DELETE FROM app_users` rồi INSERT từng user; `UserAccountService.cs:532-581` có thể gọi save trong load/normalization; `MarkActive` đi đường repository riêng.
- **Impact:** O(N) và write amplification trên luồng auth; multi-instance có thể lost update; một lỗi giữa batch làm tăng blast radius vận hành.
- **Tái hiện:** hai instance đồng thời thay đổi hai user từ snapshot khác nhau; lần save sau có thể ghi mất thay đổi trước.
- **Khuyến nghị:** CRUD/UPSERT theo row với concurrency token/version; query trực tiếp theo id/email; cleanup reset token thành job/query riêng.

#### F-10 — Tài liệu được lưu dưới static web root, có thể bypass authorization

- **Evidence:** upload dùng `Path.Combine(_environment.WebRootPath, "uploads")` tại `Web/Pages/Home/SubjectDocuments.cshtml.cs:133-159`; `Web/Program.cs:288` map static assets; repository hiện có file thật trong `Web/wwwroot/uploads/`.
- **Impact:** ai biết/thu được tên file GUID có thể tải trực tiếp qua static middleware mà không qua `ViewDocument`/`CanViewDocumentAsync`, kể cả chưa login.
- **Tái hiện:** request trực tiếp `/uploads/<stored-file>` thay vì `/Home/ViewDocument?id=...`.
- **Khuyến nghị:** lưu ngoài `wwwroot`; chỉ stream qua endpoint có resource authorization; migration file hiện có và chặn route `/uploads` công khai.

#### F-11 — Enrolled-subject của Student không giới hạn dữ liệu RAG

- **Evidence:** assignment được lưu ở `rag_subject_students` (`KnowledgeSqlSubjectStudent.cs:6-18`, repository `SqlKnowledgeRepository.cs:1072-1127`), nhưng scope Student+Chat trả toàn bộ document/chunk tại `SqlKnowledgeRepository.cs:723-726,763-766`; `GetIndexedSubjectsAsync` dùng scope này tại `:114-125`.
- **Impact:** Student được gán môn A vẫn có thể hỏi/nhận citation từ mọi môn active; thao tác register/unregister Student vào subject không có tác dụng với quyền chat.
- **Tái hiện:** gán Student chỉ vào A, truyền subject filter B hoặc hỏi nội dung đặc thù của B.
- **Khuyến nghị:** join `SubjectStudents` theo `scope.UserId` trong document/chunk scope; định nghĩa rõ gói dịch vụ có override enrollment hay không; thêm test authorization RAG.

#### F-12 — Đổi mã môn không cascade sang document/chunk nên mất liên kết nghiệp vụ

- **Evidence:** `SqlKnowledgeRepository.UpsertSubjectAsync` chỉ sửa `rag_subjects.Code/Name` và notification (`:292-358`); document lưu subject dạng chuỗi độc lập (`KnowledgeSqlDocument.cs:18-22`); authorization/matching dựa vào so sánh chuỗi code tại `SqlKnowledgeRepository.cs:735-738,776-778`.
- **Impact:** sau đổi code, tài liệu cũ không còn thuộc subject mới theo scope; lecturer có thể mất quyền xem/quản lý, analytics và active/inactive filtering sai, RAG phân loại lệch.
- **Tái hiện:** subject `ABC` có document `ABC - ...`, đổi code thành `XYZ`; document vẫn giữ `ABC`.
- **Khuyến nghị:** document/chunk dùng `SubjectId` FK; ngắn hạn rename trong transaction và cascade mọi metadata liên quan, kiểm tra collision, re-index nếu embedding metadata đổi.

#### F-13 — Endpoint nhập URL tài liệu có SSRF

- **Evidence:** `WebPageTextExtractor.cs:21-28` chỉ kiểm tra scheme HTTP/HTTPS rồi gửi request; `HttpRepository.cs:9-18` dùng `HttpClient` mặc định (theo redirect), không chặn loopback/private/link-local/DNS rebinding; gọi từ `SubjectDocuments.cshtml.cs:148-159`.
- **Impact:** Lecturer có thể khiến server gọi dịch vụ nội bộ, localhost hoặc metadata endpoint và lưu/index response.
- **Tái hiện:** nhập URL HTTP trỏ tới IP private/loopback mà server truy cập được.
- **Khuyến nghị:** deny private/link-local/loopback sau DNS resolve và sau từng redirect; allowlist domain nếu có thể; giới hạn size/content-type/time; outbound proxy/network policy.

#### F-14 — Lộ directory user online và trạng thái gói cho mọi tài khoản

- **Evidence:** hub chỉ `[Authorize]`, broadcast `Clients.All` và public method snapshot tại `Web/Hubs/DocumentStatusHub.cs:13,74-89`; payload gồm userId, email, role, premium tại `OnlineUserPresenceTracker.cs:8-22,55-80`; page dùng `ChatAccess` tại `OnlineUsers.cshtml.cs:7`.
- **Impact:** mọi Student có thể enumerate email, UUID, role, premium và trạng thái online của toàn hệ thống.
- **Tái hiện:** login Student, kết nối hub/gọi `GetOnlineUsersAsync`.
- **Khuyến nghị:** Admin/Lecturer-only hoặc payload Student chỉ có count/anonymous display; group broadcast theo policy; không phát email/UUID nếu không cần.

#### F-15 — Xóa user không xử lý toàn bộ dữ liệu tham chiếu

- **Evidence:** `UserAccountService.DeleteAsync` chỉ chặn subject owner/lecturer rồi xóa user (`:319-366`), không kiểm tra `SubjectStudents`, documents, chat sessions, payments, subscriptions. Các entity lưu `UserId` nhưng không FK đến `app_users`, ví dụ `KnowledgeSqlSubjectStudent.cs:18`, `KnowledgeSqlPayment.cs:13-20`, `KnowledgeSqlSubscription.cs:13-20`.
- **Impact:** orphan assignment/subscription/session; email cũ vẫn nằm ở payment/document; recreate cùng email tạo identity ID khác và dữ liệu không nối lại; audit khó giải thích.
- **Tái hiện:** Student inactive >3 tháng nhưng còn enrollment/subscription, Admin delete account.
- **Khuyến nghị:** ưu tiên soft-delete/anonymization có policy retention; nếu hard-delete, transaction cleanup/cascade rõ từng aggregate và giữ immutable financial audit.

#### F-28 — Webhook/return URL mặc định là localhost nhưng gateway là production

- **Evidence:** `Web/appsettings.json:55` đặt `BaseReturnUrl=http://localhost:9999`; `PaymentService.cs:376-386` dựng IPN URL từ giá trị này, trong khi MoMo endpoint tại config `:60` là production.
- **Impact:** nếu deployment không override, gateway ngoài Internet không thể gọi localhost của server/người dùng; payment treo Pending dù người dùng đã trả.
- **Tái hiện:** deploy nguyên appsettings và tạo checkout MoMo.
- **Khuyến nghị:** startup validation từ chối localhost/HTTP ngoài Development; cấu hình public HTTPS canonical URL bằng environment và health-check callback từ ngoài.

#### F-29 — FREE trial có thể nhận hai lần

- **Evidence:** provisioning tạo subscription FREE trực tiếp, không có payment (`UserProvisioningService.cs:47-73`); checkout FREE chỉ kiểm tra từng có payment Paid hay chưa (`PaymentService.cs:59-63`, `PaymentRepository.cs:98-105`).
- **Impact:** sau 30 ngày FREE đầu hết hạn, cùng user kích hoạt thêm FREE vì hệ thống không thấy paid FREE record.
- **Tái hiện:** provision Student, chờ/chỉnh trial hết hạn, checkout FREE.
- **Khuyến nghị:** durable `TrialGrantedAt/TrialClaim` unique theo user hoặc tạo zero-value payment/grant ledger idempotent ngay lúc provisioning.

#### F-30 — Mua lại/nâng gói làm mất toàn bộ thời gian đã trả còn lại

- **Evidence:** activation luôn bắt đầu subscription mới tại `now` (`PaymentService.cs:341-368`); `SubscriptionRepository.ActivateExclusiveAsync` hủy mọi active subscription và cắt `EndsAt` về thời điểm activation (`:70-83`).
- **Impact:** mua Annual/Pro mới khi gói hiện tại còn 20 ngày làm mất 20 ngày, không gia hạn/prorate/cảnh báo.
- **Tái hiện:** user còn active entitlement rồi thanh toán một gói được phép mua.
- **Khuyến nghị:** cùng gói thì renewal bắt đầu từ current `EndsAt`; upgrade phải carry credit/prorate hoặc xác nhận rõ forfeiture trước checkout; tính atomically.

#### F-31 — Hai callback Paid đồng thời có thể tạo trạng thái subscription không xác định

- **Evidence:** payment được commit Paid trước activation (`PaymentService.cs:173-179`); guard “latest paid” là read rời (`:332-339`); repository đọc/hủy/insert trong transaction khác (`SubscriptionRepository.cs:62-104`); schema chỉ unique PaymentId, không có invariant một active subscription/user (`KnowledgeSqlSchemaInitializer.cs:117-118`).
- **Impact:** hai payment khác nhau callback đồng thời có thể cùng qua guard, để hai active rows hoặc hủy nhau theo thứ tự race.
- **Tái hiện:** gửi đồng thời hai webhook hợp lệ của hai order cùng user.
- **Khuyến nghị:** một serializable transaction/advisory lock theo UserId bao gồm paid transition + entitlement; DB constraint/state machine bảo đảm một active subscription; idempotent event/outbox.

### P2 — Medium

#### F-16 — Thống kê doanh thu dùng ngày tạo payment thay vì ngày paid

- **Evidence:** `DAL/Repositories/Analytics/AnalyticsRepository.cs:120-136` lọc payment bằng `CreatedAt`; doanh thu tính các row hiện có status Paid tại `:183-185`; daily chart dùng `PaidAt` nhưng chỉ trên tập đã lọc theo CreatedAt tại `:324-347`.
- **Impact:** payment tạo trước kỳ nhưng trả trong kỳ bị bỏ; payment tạo trong kỳ và trả sau có thể bị quy về kỳ tạo; báo cáo tài chính theo khoảng ngày sai.
- **Tái hiện:** tạo payment ngày cuối tháng, webhook Paid đầu tháng sau; chọn từng tháng.
- **Khuyến nghị:** revenue/payment success lọc theo `PaidAt`; pending/failure theo `CreatedAt` hoặc timestamp trạng thái tương ứng; định nghĩa timezone báo cáo và test boundary.

#### F-17 — Quota chat chỉ khóa trong một process

- **Evidence:** `ChatUsageService.cs:19-20,47-129` dùng static dictionary + `SemaphoreSlim`; `Ask.cshtml.cs:60-75` check quota rồi mới gọi AI/add message.
- **Impact:** khi scale nhiều instance, hai request song song cùng user có thể cùng thấy quota còn 1 và đều tiêu; restart mất lock state.
- **Tái hiện:** gửi đồng thời qua hai app instance dùng chung DB.
- **Khuyến nghị:** atomic quota counter/transaction hoặc distributed lock; unique usage ledger và idempotency request.

#### F-18 — Queue index là unbounded RAM, không durable

- **Evidence:** `BLL/Services/Jobs/DocumentIndexJobQueue.cs:11-21` dùng `Channel.CreateUnbounded`; worker có scan lại document Processing ở startup (`Web/Services/DocumentIndexWorker.cs:57-63`).
- **Impact:** upload burst có thể tăng RAM; crash mất thứ tự/job đang queue (startup recovery giảm nhưng không loại hết duplicate/stuck/race); không có backpressure/quan sát queue.
- **Tái hiện:** enqueue số lượng lớn hoặc restart giữa cập nhật trạng thái và enqueue.
- **Khuyến nghị:** durable job table/queue, bounded capacity/backpressure, lease/status/attempt timestamps và idempotent worker.

#### F-19 — Upload copy toàn bộ file vào RAM trước khi lưu

- **Evidence:** `DocumentIndexingService.cs:72-85` copy stream vào `MemoryStream`; không có giới hạn business trong service trước copy.
- **Impact:** request file lớn hoặc nhiều upload đồng thời gây memory pressure/GC/OOM (dù web server có thể có giới hạn request mặc định).
- **Tái hiện:** nhiều Lecturer upload file gần request-size limit cùng lúc.
- **Khuyến nghị:** giới hạn size explicit theo loại file; stream thẳng tới temp file có quota; validate extension/signature/content-type; cleanup khi DB save lỗi.

#### F-20 — Admin hiện vẫn có quyền edit/delete/re-index tài liệu người khác

- **Evidence:** `HomePageModelBase.CanManageSubjectAsync` trả true ngay cho Admin (`:206-227`); `CanManageDocumentAsync` dùng kết quả đó (`:239-251`); các handler delete/reindex gọi helper tại `SubjectDocuments.cshtml.cs:183-218`. Admin chỉ bị chặn upload (`:101-107`).
- **Impact:** nếu rule mong muốn là “Admin quản lý catalog nhưng không mutate tài liệu Lecturer”, implementation đang rộng hơn UI/business rule và có thể xóa/reindex ngoài ý muốn.
- **Tái hiện:** Admin POST DeleteDocument/ReindexDocument với id tài liệu của Lecturer.
- **Khuyến nghị:** xác nhận rule; nếu đúng, tách quyền CatalogAdmin và DocumentOwner/SubjectLecturer, resource authorization ở BLL chứ không chỉ PageModel.

#### F-21 — Forgot-password vẫn chờ SMTP trên request

- **Evidence:** `ForgotPassword.cshtml.cs:53-65` await gửi email trực tiếp; welcome email đã có worker riêng.
- **Impact:** UI chậm/timeout theo SMTP; abuse request giữ worker và lộ tình trạng hạ tầng qua timing.
- **Khuyến nghị:** đưa reset email vào durable outbox; trả generic response ngay; rate-limit như F-05.

#### F-22 — Bốn endpoint mutation chat tắt antiforgery

- **Evidence:** `[IgnoreAntiforgeryToken]` tại `CreateChatSession.cshtml.cs:11-13`, `RenameChatSession.cshtml.cs:12-14`, `DeleteChatSession.cshtml.cs:12-14`, `StarChatSession.cshtml.cs:13-14`.
- **Impact:** SameSite cookie và JSON giảm exploit cross-site phổ biến, nhưng same-site/subdomain attack vẫn có thể mutate dữ liệu; bypass này không cần thiết.
- **Khuyến nghị:** gửi antiforgery header như endpoint Ask đang làm hoặc tách API bearer-token + CORS chặt.

#### F-23 — Cấu hình ép Development và chưa bắt buộc secure cookie

- **Evidence:** `Web/appsettings.json:2-4` đặt environment Development; `Program.cs:23-36,275-281` dùng giá trị đó để quyết định HSTS/error handler; cookie setup `:59-65` không set `SecurePolicy=Always`.
- **Impact:** deploy nguyên cấu hình không có HSTS; cookie có thể đi qua HTTP; lỗi production có thể lộ chi tiết tùy middleware.
- **Khuyến nghị:** lấy `ASPNETCORE_ENVIRONMENT` từ deployment, refuse production nếu không HTTPS/canonical origin; Secure=Always, HttpOnly, SameSite explicit; cấu hình proxy đúng.

#### F-24 — Đổi embedding/chunk setting cần thao tác re-index riêng và có cửa sổ dữ liệu không đồng nhất

- **Evidence:** `AiSettingsService.cs:79-118` áp dụng model/dimension/chunker ngay; UI chỉ nói tài liệu mới dùng setting (`AiSettings.cshtml.cs:45-48`); stale docs chỉ được phát hiện/re-index thủ công tại `Home/Index.cshtml.cs:96-101,358-380`.
- **Impact:** giữa lúc đổi setting và re-index, query embedding mới được so với chunk embedding/metadata cũ, chất lượng retrieval không nhất quán; Admin dễ bỏ quên bước re-index.
- **Khuyến nghị:** tạo migration job ngay khi save, hiển thị trạng thái/ảnh hưởng và giữ version active cũ cho tới khi re-index hoàn tất.

#### F-25 — Test suite chưa bao phủ các luồng rủi ro cao

- **Evidence:** 49 test hiện tập trung BLL chat/billing/notification/embedding; không có test UserAccountService, login/Google/reset/cookie, resource authorization document, enrollment scope, URL SSRF, worker recovery, Razor webhook integration.
- **Impact:** các regression như seed reset password, session revocation, Google callback, document access và subject rename cascade không bị CI bắt.
- **Khuyến nghị:** integration test với DB cho auth/resource authorization/payment idempotency; security tests host/CSRF/SSRF; concurrency tests quota/payment/webhook; end-to-end role matrix.

#### F-32 — Chống downgrade dùng `SortOrder` merchandising làm tier nghiệp vụ

- **Evidence:** backend so `SortOrder` tại `PaymentService.cs:48-56`, UI lặp lại tại `Web/Pages/Packages/Index.cshtml.cs:105-124`; default PRO order 30/180 câu, ANNUAL order 40/60 câu (`PackageService.cs:137-183`).
- **Impact:** Annual bị chặn mua Pro dù quota Pro gấp ba; chỉ đổi thứ tự hiển thị cũng thay đổi luật downgrade.
- **Tái hiện:** active ANNUAL rồi chọn PRO.
- **Khuyến nghị:** trường immutable `TierRank` riêng; tách tier, billing duration và entitlement; chỉ một service quyết định transition và UI render kết quả đó.

#### F-33 — Gateway lỗi để lại Payment Pending vĩnh viễn và click lại tạo duplicate

- **Evidence:** Pending được insert trước external call (`PaymentService.cs:65-81`); đoạn gọi/update gateway `:93-120` không catch để chuyển Failed; pending count không có TTL (`PackageRepository.cs:139-148`).
- **Impact:** gateway timeout làm bẩn analytics/admin warning; retry bằng UI tạo không giới hạn order Pending.
- **Tái hiện:** làm gateway timeout sau khi row Pending được save, rồi click thanh toán lại.
- **Khuyến nghị:** catch -> Failed/Unknown; idempotency/reuse open checkout; expiry + reconciliation job; rate-limit checkout.

#### F-34 — Trang return không kiểm tra payment thuộc user đăng nhập

- **Evidence:** page chỉ `[Authorize]` (`Payments/Return.cshtml.cs:9-28`); service lookup/reconcile bằng provider + orderCode (`PaymentService.cs:210-230`), không nhận UserId; order code là timestamp + ba chữ số random (`:406-415`).
- **Impact:** user đăng nhập có thể dò/xem trạng thái và provider transaction id của người khác, đồng thời gây nhiều request reconcile PayOS.
- **Tái hiện:** login account A rồi gọi return với order code của B.
- **Khuyến nghị:** query `(provider, orderCode, authenticatedUserId)` cho browser return; tách webhook/internal reconciliation; order id đủ entropy và rate-limit.

#### F-35 — Webhook có thể chấp nhận Paid khi amount thiếu/parse lỗi

- **Evidence:** amount chỉ bị reject nếu `AmountVnd.HasValue` và khác expected (`PaymentService.cs:149-152`); gateway mapper trả null khi parse lỗi (`MomoPaymentGateway.cs:92-100`, `PayOsPaymentGateway.cs:132-140`).
- **Impact:** chữ ký có thể hợp lệ nhưng payload thiếu invariant amount vẫn chuyển Paid; kiểm tra financial amount đang fail-open.
- **Tái hiện:** webhook ký hợp lệ với amount field không parse được/không có, tùy provider payload validation.
- **Khuyến nghị:** fail closed nếu amount null/nonpositive/currency sai; schema-validate payload trước signature/business transition.

#### F-36 — Notification lấy 50 bản cũ nhất nên làm mất notification mới

- **Evidence:** `SystemNotificationRepository.cs:19-25` order tăng dần rồi `Take(50)`.
- **Impact:** nếu hơn 50 event trong ba ngày, event thứ 51 trở đi không được trả cho tới khi bản cũ hết cửa sổ; người dùng bỏ lỡ thay đổi mới nhất.
- **Tái hiện:** tạo 51 thay đổi giá/tên trong ba ngày.
- **Khuyến nghị:** lấy newest 50 descending trong DB rồi đảo ascending để hiển thị, hoặc paginate/read receipt server-side.

#### F-37 — Seed package bị race ở cold start/request đầu

- **Evidence:** mọi catalog read gọi seed (`PackageService.cs:17-33`); seed read-then-insert từng item (`:98-194`); DB có unique code index (`KnowledgeSqlSchemaInitializer.cs:57`).
- **Impact:** hai request đầu đồng thời cùng thấy package thiếu, một request có thể unique violation/500.
- **Tái hiện:** DB trống, gửi đồng thời hai request đọc packages.
- **Khuyến nghị:** migration/startup seed một lần; `INSERT ... ON CONFLICT DO NOTHING` trong transaction/advisory lock.

#### F-38 — Callback payment cũ có thể override gói mới sau nhiều checkout bỏ dở

- **Evidence:** guard lấy 100 payment mới nhất rồi mới lọc Paid (`PaymentService.cs:334-339`; `PaymentRepository.cs:86-95`).
- **Impact:** hơn 100 Pending/Failed mới có thể đẩy payment Paid hiện hành khỏi tập; callback cũ qua guard, kích hoạt và hủy gói hiện tại.
- **Tái hiện:** tạo >100 checkout bỏ dở sau một paid payment, rồi gửi late callback của order cũ hơn.
- **Khuyến nghị:** query latest Paid trực tiếp trong DB; deterministic effective-order rule và compare settlement/effective sequence atomically.

### P3 — Low

#### F-26 — Google external cookie không được dọn ở nhánh failure

- **Evidence:** `GoogleCallback.cshtml.cs:29-48` redirect ở failure; chỉ success sign out External tại `:53`; cookie hết hạn sau 5 phút (`Program.cs:140-144`).
- **Impact:** stale external state có thể làm lần thử kế tiếp khó hiểu trong cửa sổ ngắn.
- **Khuyến nghị:** sign out External ở mọi terminal branch.

#### F-27 — Logic “last admin” trong DeleteAsync là dead code

- **Evidence:** `UserAccountService.cs:328-331` đã chặn mọi role ngoài Student/Lecturer; check `user.Role == Admin` tại `:333-336` vì vậy không bao giờ chạy.
- **Impact:** code gây hiểu nhầm rằng Admin deletion được hỗ trợ và có last-admin guard, trong khi thực tế không có flow đó.
- **Khuyến nghị:** xóa dead branch hoặc thiết kế lại lifecycle Admin/suspend rõ ràng, kèm test.

#### F-39 — Notification query DB trên mọi layout dù browser đã acknowledged

- **Evidence:** `_Layout.cshtml:52-63` gọi notification service ở mọi authenticated render trước khi JavaScript kiểm tra localStorage; exception bị nuốt không log. Schema chỉ tạo bảng/index (`KnowledgeSqlSchemaInitializer.cs:137-147`), không retention.
- **Impact:** DB load tăng theo page view và bảng tăng mãi; lỗi notification bị ẩn hoàn toàn.
- **Khuyến nghị:** cache/paginate, server-side receipt hoặc `since` cursor; retention job; log warning có rate limit.

#### F-40 — Hai entitlement package được model nhưng không enforce

- **Evidence:** `MonthlyDocumentUploadLimit` và `StorageLimitMb` xuất hiện trong schema/entity/DTO/mapping nhưng không có usage check trước upload; default hiện bằng 0.
- **Impact:** nếu UI/marketing hiển thị các quyền lợi này, khách hàng thực tế không bị giới hạn; model gây hiểu nhầm rằng rule đã tồn tại.
- **Khuyến nghị:** bỏ khỏi contract cho tới khi hỗ trợ, hoặc atomic usage ledger/check trước nhận upload với semantics rõ `0 = unlimited/disabled`.

## Điểm tích cực đã xác nhận

- Admin Razor Pages chính dùng `AdminOnly`; return URL login/Google được kiểm tra local và lọc theo role.
- Password dùng PBKDF2 với salt ngẫu nhiên/fixed-time verify; reset token random, lưu hash và single-use.
- Payment lưu `AmountVnd` snapshot; việc đổi giá không sửa payment đang pending.
- Giá package có optimistic expected-price check, reason/history và system notification.
- Document file service có canonical-path check trước đọc/xóa qua endpoint ứng dụng.
- Chat session repository kiểm tra owner trước rename/star/delete/add message.

## Thứ tự xử lý đề xuất

1. **Hôm nay:** F-01, F-02; tạm không public deploy cho tới khi rotate secret và loại bỏ default Admin.
2. **Trước deploy:** F-03, F-05–F-08, F-10, F-13, F-14, cấu hình HTTPS ở F-23.
3. **Trước nghiệm thu nghiệp vụ:** F-11, F-12, F-16 và xác nhận rule F-20.
4. **Đợt hardening:** F-04, F-09, F-15, F-17–F-19, F-21–F-25.

## Kiểm chứng đã chạy

- `dotnet test BLL.Tests\BLL.Tests.csproj --no-restore`: **49 passed, 0 failed, 0 skipped**.
- `dotnet build Web\Web.csproj --no-restore -p:BaseOutputPath=...`: **Build succeeded, 0 warning, 0 error**.
- Không gọi gateway/SMTP/Google/AI thật; không thay đổi dữ liệu production.

## Giới hạn audit

- Không có PRD chính thức/matrix quyền hoàn chỉnh, nên finding F-20 được ghi rõ là rule cần xác nhận; các finding còn lại dựa trên hành vi code có thể tái hiện.
- Không pentest hạ tầng, không kiểm tra lịch sử đã bị clone/expose hay credential còn hiệu lực; vì secret đã commit nên vẫn phải coi là compromised và rotate.
- Build/test pass chỉ chứng minh code biên dịch và test hiện có pass, không phủ các khoảng trống integration/security đã liệt kê.
