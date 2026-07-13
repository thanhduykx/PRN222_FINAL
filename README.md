# EduVietRAG - Course Document Assistant

EduVietRAG là web app ASP.NET Core Razor Pages hỗ trợ quản lý tài liệu môn học và hỏi đáp theo mô hình RAG (Retrieval-Augmented Generation). Hệ thống cho phép giảng viên tải lên tài liệu, tự động trích xuất nội dung, chia đoạn, tạo embedding, sau đó sinh viên có thể đặt câu hỏi và nhận câu trả lời có trích dẫn nguồn.

> Mục tiêu chính: câu trả lời phải bám vào tài liệu đã index, có citation rõ ràng, hạn chế trả lời ngoài phạm vi dữ liệu của môn học.

## Mục Lục

- [Tính năng chính](#tính-năng-chính)
- [Kiến trúc hệ thống](#kiến-trúc-hệ-thống)
- [Cấu trúc source code](#cấu-trúc-source-code)
- [Phân quyền người dùng](#phân-quyền-người-dùng)
- [Luồng nghiệp vụ](#luồng-nghiệp-vụ)
- [Cấu hình môi trường](#cấu-hình-môi-trường)
- [Hướng dẫn chạy project](#hướng-dẫn-chạy-project)
- [Hướng dẫn sử dụng](#hướng-dẫn-sử-dụng)
- [Kiểm thử](#kiểm-thử)
- [Lưu ý vận hành](#lưu-ý-vận-hành)
- [Kiến Trúc Hệ Thống](#kiến-trúc-hệ-thống)

## Tính Năng Chính

| Nhóm chức năng | Mô tả |
|---|---|
| Xác thực | Đăng nhập bằng tài khoản nội bộ, hỗ trợ Google OAuth khi cấu hình client id/secret. |
| Quản trị người dùng | Admin tạo tài khoản, import Excel, đổi role, gán môn cho lecturer, gửi email welcome qua SMTP. |
| Quản lý môn học | Seed sẵn danh mục môn, tạo thêm môn, gán môn cho giảng viên phụ trách. |
| Quản lý tài liệu | Upload tài liệu PDF, DOCX, PPTX, TXT hoặc lấy nội dung từ URL. |
| Index tài liệu | Trích xuất text, chia chunk theo ngữ cảnh syllabus, tạo embedding, lưu vào SQL Server. |
| Chat RAG | Hỏi đáp theo phiên chat, truy xuất chunk liên quan, sinh câu trả lời từ context và lưu lịch sử. |
| Citation | Câu trả lời kèm nguồn tài liệu/chunk để người dùng kiểm tra lại. |
| Realtime status | SignalR cập nhật trạng thái index tài liệu cho giao diện. |
| Kiểm thử | Có script kiểm tra dependency 3 lớp, build và smoke test ứng dụng. |

## Kiến Trúc Hệ Thống

![Sơ đồ kiến trúc EduVietRAG](docs/architecture.jpg)

Hệ thống tuân thủ luồng một chiều `Web → BLL → DAL`. `Web` chỉ nhận input và hiển thị; `BLL` validate, xử lý nghiệp vụ và map raw data sang DTO/model; `DAL` thực hiện PostgreSQL, filesystem, SMTP và HTTP API. Kết quả quay về theo chiều `DAL → BLL → Web`; không có tham chiếu ngược lớp.

### Luồng RAG Tóm Tắt

```mermaid
sequenceDiagram
    actor Lecturer as Lecturer/Admin
    actor Student as Student
    participant UI as Razor Pages
    participant Worker as DocumentIndexWorker
    participant Indexing as DocumentIndexingService
    participant Repo as SqlKnowledgeRepository
    participant AI as Gemini API
    participant Chat as RagChatService

    Lecturer->>UI: Upload tài liệu hoặc URL
    UI->>Worker: Đưa job vào hàng đợi index
    Worker->>Indexing: Trích xuất text và chia chunk
    
    Indexing->>AI: Tạo embedding cho chunk
    %% Bổ sung trả về từ Gemini lúc tạo embedding
    AI-->>Indexing: Trả về vector embedding 
    
    Indexing->>Repo: Lưu document, chunk, embedding
    
    Student->>UI: Đặt câu hỏi trong chat
    UI->>Chat: Gửi câu hỏi + lịch sử phiên
    
    Chat->>Repo: Retrieve chunk liên quan
    %% Bổ sung trả về từ Database lúc tìm ngữ cảnh
    Repo-->>Chat: Trả về các chunk phù hợp (Context)
    
    Chat->>AI: Sinh câu trả lời từ context
    %% Bổ sung trả về từ Gemini lúc sinh câu trả lời
    AI-->>Chat: Trả về câu trả lời (Text)
    
    Chat->>Repo: Lưu message và citation
    UI-->>Student: Hiển thị answer + nguồn trích dẫn
```

## Cấu Trúc Source Code

```text
C:\PRN222_FINAL
|-- PRN222_FINAL.sln
|-- README.md
|-- BLL/                         # Business Logic Layer
|   |-- Contracts/               # Request/response DTO theo feature
|   |-- Models/                  # Model nghiệp vụ dùng qua interface BLL
|   |-- Mapping/                 # Raw DAL model -> business model/DTO
|   |-- Services/                # Validation và nghiệp vụ
|   `-- BLL.csproj
|-- DAL/                         # Data Access Layer
|   |-- Context/                  # EF Core DbContext và factory
|   |-- Entities/                 # Entity persistence, không lộ lên Web
|   |-- Models/                   # Raw data model chỉ dùng giữa DAL và BLL
|   |-- Mapping/                  # Mapper database entity <-> raw model
|   |-- Repositories/             # Database, file, SMTP và HTTP transport
|   |-- Schema/                   # Khởi tạo/cập nhật schema
|   `-- DAL.csproj
`-- Web/                         # Presentation Layer
    |-- Pages/                    # Razor Pages
    |-- Models/, ViewModels/      # Input/view model chỉ dành cho giao diện
    |-- Hubs/, Services/          # SignalR, worker và adapter web
    |-- wwwroot/
    |-- Program.cs
    `-- Web.csproj
```
## Kiến Trúc Hệ Thống

![Sơ đồ kiến trúc EduVietRAG](docs/architecture.jpg)

## Phân Quyền Người Dùng

Hệ thống có 3 role chính, được định nghĩa trong `BLL/Security/AppRoles.cs`.

| Role | Quyền truy cập | Ghi chú nghiệp vụ |
|---|---|---|
| Student | Chat, xem phiên chat, tạo/đổi tên/ghim/xóa phiên chat, xem chính sách trả lời. | Không được upload/quản lý tài liệu. Mặc định dùng toàn bộ tài liệu đã index theo phạm vi app hiện tại. |
| Lecturer | Toàn bộ quyền chat + quản lý tài liệu/môn được phụ trách. | Có thể upload tài liệu, xem document, preview, chỉnh sửa metadata, quản lý workspace môn học. |
| Admin | Toàn quyền Lecturer + trang Admin. | Tạo/import user, đổi role, gán môn cho lecturer, tạo subject, quản lý danh mục tài khoản. |

### Policy Trong Code

| Policy | Role được phép | Dùng cho |
|---|---|---|
| `ChatAccess` | Student, Lecturer, Admin | Các trang chat, course workspace, privacy/answer policy. |
| `DocumentRead` | Lecturer, Admin | Đọc tài liệu nếu cần policy tách riêng. |
| `DocumentManagement` | Lecturer, Admin | Upload, xem, preview, sửa, quản lý tài liệu. |
| `AdminOnly` | Admin | Trang `/Admin/Index`. |

### Quy Tắc Tài Khoản

- Người dùng không tự đăng ký. Trang Register sẽ chuyển về Login và báo liên hệ Nhà trường để được cấp tài khoản.
- Admin seed được tạo tự động nếu `SeedAdmin.Enabled = true` và database chưa có admin hợp lệ.
- Không được hạ quyền hoặc xóa seed admin.
- Không được xóa admin trực tiếp; cần đổi role về Student/Lecturer trước nếu nghiệp vụ cho phép.
- Google OAuth chỉ đăng nhập được khi đã cấu hình và email tồn tại/được cấp trong hệ thống theo luồng hiện tại.

## Luồng Nghiệp Vụ

### 1. Quản Trị Tài Khoản

```mermaid
flowchart LR
    Admin["Admin"] --> Create["Tạo user hoặc import Excel"]
    Create --> Role["Chọn role: Student / Lecturer / Admin"]
    Role --> Subject{"Là Lecturer?"}
    Subject -- Có --> Assign["Gán môn phụ trách"]
    Subject -- Không --> Save["Lưu tài khoản"]
    Assign --> Save
    Save --> Email["Gửi welcome email qua SMTP"]
```

### 2. Index Tài Liệu

```mermaid
flowchart LR
    Upload["Upload file / URL"] --> Extract["Extract text"]
    Extract --> Chunk["Chunk theo syllabus/context"]
    Chunk --> Embedding["Tạo embedding"]
    Embedding --> Save["Lưu SQL Server"]
    Save --> Notify["SignalR báo trạng thái"]
```

### 3. Hỏi Đáp RAG

```mermaid
flowchart LR
    Question["Câu hỏi"] --> Retrieve["Retrieve chunk liên quan"]
    Retrieve --> Prompt["Tạo prompt có context"]
    Prompt --> Answer["Gemini sinh câu trả lời"]
    Answer --> Guard["Kiểm tra bám context"]
    Guard --> Citation["Gắn citation"]
    Citation --> History["Lưu lịch sử chat"]
```

## Cấu Hình Môi Trường

### Yêu Cầu

- .NET SDK 9.x
- SQL Server LocalDB, Express, Developer hoặc instance SQL Server tương đương
- Gemini API key nếu dùng provider `Gemini`
- SMTP account nếu muốn gửi email welcome/reset password
- Google OAuth client nếu muốn bật đăng nhập Google

### File Cấu Hình Chính

`Web/appsettings.json` chứa các nhóm cấu hình sau:

| Nhóm | Ý nghĩa |
|---|---|
| `ConnectionStrings:DefaultConnection` | Chuỗi kết nối SQL Server. |
| `SeedAdmin` | Tài khoản admin mặc định khi khởi tạo. |
| `Embedding` | Bật/tắt embedding, chọn provider `Gemini` hoặc fallback hashing. |
| `Gemini` | API key, model chat, model embedding, timeout, base URL. |
| `Smtp` | Host, port, SSL, email gửi, username, password. |
| `Authentication:Google` | Google client id và client secret. |

Khuyến nghị thực tế: không commit API key, OAuth secret, SMTP password hoặc mật khẩu database thật. Dùng User Secrets, biến môi trường hoặc file cấu hình riêng theo môi trường deploy.

Ví dụ cấu hình biến môi trường trên PowerShell:

```powershell
$env:Gemini__ApiKey="YOUR_GEMINI_API_KEY"
$env:ConnectionStrings__DefaultConnection="Server=localhost;Database=EduVietRAG;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
```

## Hướng Dẫn Chạy Project

Chạy từ thư mục solution:

```powershell
cd C:\PRN222_FINAL
dotnet restore
dotnet build PRN222_FINAL.sln
dotnet run --project Web\Web.csproj --urls http://0.0.0.0:9999
```

Mở trình duyệt:

```text
http://localhost:9999
```

Máy khác trong cùng LAN có thể truy cập bằng IP máy chạy app:

```text
http://<IP-may-chay-app>:9999
```

Khi app khởi động, hệ thống sẽ:

1. Kết nối SQL Server theo `DefaultConnection`.
2. Tạo/bổ sung schema cần thiết nếu thiếu.
3. Seed danh mục môn mặc định.
4. Seed admin nếu cấu hình bật.
5. Khởi động hosted service xử lý hàng đợi index tài liệu.

## Hướng Dẫn Sử Dụng

### Admin

1. Đăng nhập bằng seed admin hoặc tài khoản admin đã có.
2. Vào trang Admin để quản lý người dùng.
3. Tạo tài khoản mới hoặc import Excel danh sách user.
4. Chọn role phù hợp: Student, Lecturer hoặc Admin.
5. Với Lecturer, gán môn phụ trách nếu có.
6. Kiểm tra SMTP nếu hệ thống không gửi được welcome email.

### Lecturer

1. Đăng nhập vào hệ thống.
2. Vào khu vực quản lý tài liệu/môn học.
3. Upload file PDF, DOCX, PPTX, TXT hoặc nhập URL cần index.
4. Chờ trạng thái index hoàn tất. SignalR sẽ cập nhật trạng thái trên giao diện.
5. Kiểm tra preview/document nếu cần xác nhận nội dung đã index đúng.
6. Dùng chat để kiểm thử câu trả lời trên tài liệu vừa đưa vào.

### Student

1. Đăng nhập bằng tài khoản được cấp.
2. Vào trang Chat.
3. Tạo phiên chat mới hoặc tiếp tục phiên cũ.
4. Đặt câu hỏi liên quan đến tài liệu môn học.
5. Kiểm tra citation để biết câu trả lời lấy từ tài liệu nào.
6. Nếu hệ thống báo không đủ dữ liệu, cần hỏi lại rõ hơn hoặc liên hệ lecturer/admin để bổ sung tài liệu.

## Kiểm Thử

Chạy toàn bộ test:

```powershell
dotnet test PRN222_FINAL.sln
```

Các nhóm test hiện có:

| File test | Mục đích |
|---|---|
| `RagChatServiceTests.cs` | Kiểm thử luồng hỏi đáp RAG và citation. |
| `DocumentIndexingServiceTests.cs` | Kiểm thử upload/index tài liệu. |
| `DocumentAccessScopeTests.cs` | Kiểm thử phạm vi truy cập tài liệu. |
| `ParagraphAwareTextChunkerTests.cs` | Kiểm thử chunking theo đoạn. |
| `FlmSyllabusAwareTextChunkerTests.cs` | Kiểm thử chunking theo cấu trúc syllabus. |
| `GeminiEmbeddingServiceTests.cs` | Kiểm thử embedding service. |
| `CompatibleChatCompletionServiceTests.cs` | Kiểm thử adapter chat completion. |
| `AiChunkRetrievalEnrichmentServiceTests.cs` | Kiểm thử enrichment cho retrieval. |

Bộ câu hỏi/đáp án kiểm thử thủ công nằm ở:

```text
TestData/qa-test-50-vi-q-a.txt
```

## Lưu Ý Vận Hành

- Không đưa secret thật lên repository public.
- Sau khi đổi model embedding hoặc số chiều embedding, nên re-index tài liệu cũ để tránh lệch vector.
- Metadata như mã môn, chương, tên file chỉ hỗ trợ lọc/tăng hạng retrieval; không nên xem là bằng chứng trả lời nếu nội dung chunk không có dữ kiện.
- Câu trả lời học thuật phải có citation. Nếu không có chunk đủ căn cứ, hệ thống nên từ chối hoặc yêu cầu bổ sung tài liệu.
- Với Google OAuth, origin công khai được cấu hình tại `Authentication:Google:PublicOrigin`. Khi chạy local, hãy đăng ký chính xác `http://localhost:9999/signin-google` trong **Authorized redirect URIs** của Google Cloud Console. Không đăng ký `/Account/GoogleCallback`: đây chỉ là trang xử lý nội bộ sau khi middleware Google hoàn tất callback.
- Google sign-in bị ẩn khi truy cập bằng private IP LAN để tránh lỗi OAuth redirect không hợp lệ.
- SQL schema được tạo bằng `EnsureCreated` và các câu lệnh bổ sung cột/index. Nếu deploy production nghiêm túc, nên chuyển sang migration có kiểm soát.
- User account hiện được lưu trong bảng `app_users`, còn dữ liệu RAG dùng các bảng `rag_*`.

## Công Nghệ Sử Dụng

| Thành phần | Công nghệ |
|---|---|
| Web app | ASP.NET Core Razor Pages, .NET 9 |
| Auth | Cookie Authentication, Google OAuth |
| Realtime | SignalR |
| Database | SQL Server, EF Core SQL Server |
| AI | Gemini chat + embedding, hashing embedding fallback |
| Document parsing | OpenXML, PdfPig, text extractor nội bộ |
| Test | xUnit, Microsoft.NET.Test.Sdk, coverlet |
### Student

1. Đăng nhập bằng tài khoản được admin cung cấp.
2. Vào trang Chat.
3. Tạo phiên chat mới hoặc tiếp tục phiên cũ.
4. Đặt câu hỏi liên quan đến tài liệu môn học đã được upload.
5. Kiểm tra citation để biết câu trả lời lấy từ tài liệu nào.
6. Nếu hệ thống báo không đủ dữ liệu, cần hỏi lại rõ ràng  hơn hoặc liên hệ lecturer/admin để bổ sung tài liệu cho phần liên quan.
