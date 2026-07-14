# Cấu hình Groq cho Chatbot

Groq là provider tùy chọn cho bước sinh câu trả lời RAG. Việc index chunk và tìm kiếm ngữ nghĩa luôn dùng Gemini Embeddings để bảo đảm query và tài liệu nằm trong cùng một không gian vector. Groq sử dụng endpoint OpenAI-compatible:

```text
https://api.groq.com/openai/v1/chat/completions
```

## Lưu API key an toàn

Không lưu API key trong `appsettings.json`. Với môi trường Development, dùng .NET User Secrets:

```powershell
dotnet user-secrets set "Groq:ApiKey" "YOUR_GROQ_API_KEY" --project Web\Web.csproj
dotnet user-secrets set "Groq:Enabled" "true" --project Web\Web.csproj
```

Khi deploy, có thể dùng biến môi trường:

```powershell
$env:Groq__ApiKey="YOUR_GROQ_API_KEY"
$env:Groq__Enabled="true"
```

## Chọn provider

1. Đăng nhập bằng Admin.
2. Mở **Thiết lập trợ lý**.
3. Chọn provider **Groq**.
4. Chọn model Groq trong dropdown.
5. Lưu cấu hình.

Provider/model được validate tại BLL. API key không được gửi xuống trình duyệt và không được lưu trong `Web/App_Data/ai-settings.json`.

## Model được allowlist

- `llama-3.3-70b-versatile`: ưu tiên chất lượng câu trả lời.
- `llama-3.1-8b-instant`: ưu tiên tốc độ.
- `openai/gpt-oss-20b`: lựa chọn cân bằng cho tác vụ reasoning nhẹ.

Danh sách model có thể thay đổi theo chính sách deprecation của Groq; cần kiểm tra tài liệu Groq trước khi cập nhật allowlist.

## Luồng ba lớp

```text
Web (provider/model dropdown)
  → BLL (validate provider/model, RAG orchestration)
  → DAL (HTTP transport)
  → Groq API
  → DAL → BLL → Web
```

Nguồn chính thức:

- [Groq OpenAI compatibility](https://console.groq.com/docs/openai)
- [Groq Chat Completions API](https://console.groq.com/docs/api-reference)
- [Groq model deprecations](https://console.groq.com/docs/deprecations)
