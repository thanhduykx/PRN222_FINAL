# Cấu hình vận hành

Ứng dụng đọc thông tin nhạy cảm từ biến môi trường. Không đưa mật khẩu hoặc khóa truy cập vào `appsettings.json`.

Các biến bắt buộc cho môi trường thật:

```text
ConnectionStrings__DefaultConnection
Gemini__ApiKey
Authentication__Google__ClientId
Authentication__Google__ClientSecret
Smtp__FromEmail
Smtp__UserName
Smtp__Password
Payment__MoMo__PartnerCode
Payment__MoMo__AccessKey
Payment__MoMo__SecretKey
Payment__PayOS__ClientId
Payment__PayOS__ApiKey
Payment__PayOS__ChecksumKey
```

Chỉ bật tài khoản quản trị khởi tạo khi cài đặt lần đầu bằng `SeedAdmin__Enabled=true`, đồng thời đặt `SeedAdmin__Email` và `SeedAdmin__Password` qua biến môi trường. Sau khi tạo xong, tắt lại tùy chọn này.

Các khóa từng xuất hiện trong lịch sử mã nguồn phải được thu hồi và tạo mới trước khi triển khai. Sau khi cấu hình, chạy:

```powershell
dotnet build PRN222_FINAL.sln -c Release
dotnet run --project Web/Web.csproj -c Release
```

Admin có thể thay đổi cách trợ lý đọc tài liệu tại **Người dùng → Thiết lập trợ lý**. Thiết lập được lưu trong `Web/App_Data/ai-settings.json`; thư mục này cần ổ đĩa bền vững khi chạy bằng container.
