# Research: Có nên cho admin chỉnh sửa giá gói trực tiếp?

## Kết luận ngắn

**Có, nhưng không nên cho phép ghi đè giá như một trường dữ liệu thông thường.** Admin có quyền phù hợp nên được tạo và phát hành **phiên bản giá mới**. Giá cũ cần được giữ lại để đối soát giao dịch, còn việc áp dụng giá mới cho thuê bao đang hoạt động phải là một thao tác riêng, có xem trước ảnh hưởng và thời điểm hiệu lực rõ ràng.

Đối với dự án hiện tại, hướng phù hợp là:

1. admin được nhập giá mới nhưng phải qua màn hình xác nhận;
2. mỗi lần đổi giá tạo một `PriceVersion`, không sửa mất giá cũ;
3. mặc định chỉ áp dụng cho giao dịch/đăng ký mới;
4. thuê bao hiện tại giữ nguyên quyền lợi và giá đã mua;
5. nếu cần chuyển thuê bao cũ, admin phải chọn riêng thời điểm áp dụng và xem trước phần tiền chênh lệch;
6. lưu người thực hiện, thời gian, lý do và toàn bộ giá trị trước/sau.

## Bằng chứng từ các hệ thống billing

### Stripe: giá tiền là dữ liệu có phiên bản

Stripe không cho sửa `unit_amount` của một Price đã tạo. Tài liệu khuyến nghị tạo Price mới, chuyển sang ID mới rồi vô hiệu hóa Price cũ. Price đã archive vẫn được giữ cho lịch sử; các subscription đang dùng Price cũ tiếp tục hoạt động cho đến khi bị hủy hoặc được chuyển đổi rõ ràng. [Stripe — Manage products and prices](https://docs.stripe.com/products-prices/manage-prices)

Việc đổi giá của subscription là thao tác khác với đổi catalog: hệ thống phải thay Price trên subscription, quyết định có prorate hay không và có thể xem trước khoản chênh lệch. Stripe cũng hỗ trợ `pending updates` để chỉ áp dụng thay đổi khi khoản thanh toán liên quan thành công. [Stripe — Modify subscriptions](https://docs.stripe.com/billing/subscriptions/change), [Stripe — Pending updates](https://docs.stripe.com/billing/subscriptions/pending-updates), [Stripe — Prorations](https://docs.stripe.com/billing/subscriptions/prorations)

Đối với thay đổi trong tương lai, Subscription Schedules cho phép định nghĩa phase, ngày chuyển phase và cách tính proration. Đây là cơ sở tốt cho UX “áp dụng ngay” hoặc “áp dụng từ kỳ tiếp theo”. [Stripe — Subscription schedules](https://docs.stripe.com/billing/subscriptions/subscription-schedules)

### Paddle: cho sửa Price, nhưng việc chuyển subscription vẫn tách riêng

Paddle cho phép cập nhật `unit_price` nếu có quyền `price.write`; Price không thể xóa mà chỉ archive. Đây là mô hình ít nghiêm ngặt hơn Stripe. Tuy nhiên, thay đổi gói/giá của một subscription vẫn là thao tác riêng, yêu cầu gửi Price ID mới, chọn `proration_billing_mode` và Paddle khuyến nghị gọi endpoint preview trước khi cập nhật. [Paddle — Update a price](https://developer.paddle.com/api-reference/prices/update-price/), [Paddle — Prices](https://developer.paddle.com/api-reference/prices/), [Paddle — Upgrade or downgrade subscriptions](https://developer.paddle.com/build/subscriptions/replace-products-prices-upgrade-downgrade/)

Paddle còn có component tham khảo cho màn hình xác nhận, hiển thị gói cũ/mới, khoản charge hoặc credit và thời điểm có hiệu lực. [Paddle — Plan change preview](https://developer.paddle.com/sdks/components/plan-change-preview/)

### Shopify: tách riêng quyền sửa giá và ghi hoạt động

Shopify tách quyền `Edit price` khỏi quyền tạo/sửa sản phẩm nói chung. Điều này cho thấy thay đổi giá nên là một quyền nhạy cảm độc lập, không mặc nhiên đi kèm mọi quyền quản lý catalog. [Shopify — Store permissions](https://help.shopify.com/en/manual/your-account/users/roles/permissions/store-permissions)

Shopify Activity Log lưu thời gian và tác nhân thực hiện các thay đổi quản trị; tài liệu cũng nêu log hoạt động là dữ liệu chỉ đọc. [Shopify — Activity logs](https://help.shopify.com/en/manual/shopify-admin/activity-logs)

## Đánh giá implementation hiện tại

Implementation hiện tại đã có một số điểm đúng:

- route được bảo vệ bởi policy `AdminOnly`;
- backend kiểm tra khoảng giá và chỉ nhận số nguyên;
- mỗi thay đổi lưu `OldPriceVnd`, `NewPriceVnd`, `ChangedBy`, `ChangedAt` trong `package_price_changes`;
- payment lưu snapshot `AmountVnd`, còn subscription lưu snapshot giới hạn sử dụng, nên thay đổi catalog không sửa số tiền của payment đã tạo;
- thông báo hiện nói rõ giá mới áp dụng cho giao dịch tiếp theo.

Điểm còn rủi ro:

- `packages.PriceVnd` đang bị ghi đè trực tiếp, chưa có Price ID/version độc lập;
- form hiện chỉ có nút “Cập nhật giá”, chưa có bước review giá cũ → giá mới;
- chưa yêu cầu lý do thay đổi;
- chưa có `effective_from`, lịch áp dụng hoặc trạng thái draft/published;
- chưa hiển thị số checkout/payment đang chờ có thể đã chụp giá cũ;
- quyền hiện là `AdminOnly` khá rộng, chưa có quyền riêng như `PackagePrice.Write`;
- có audit record nhưng chưa thấy màn hình lịch sử/khôi phục bằng cách phát hành phiên bản mới.

## UX admin được đề xuất

### Luồng mặc định

1. Admin chọn **Tạo giá mới** trên gói.
2. Form hiển thị cố định giá hiện tại, nhập giá mới và lý do.
3. Admin chọn thời điểm: **Áp dụng ngay cho giao dịch mới** hoặc **Lên lịch**.
4. Màn hình review hiển thị:
   - giá cũ → giá mới và phần trăm thay đổi;
   - thời điểm hiệu lực và múi giờ;
   - chỉ áp dụng cho giao dịch mới hay có migration subscription;
   - số subscription/payment đang bị ảnh hưởng;
   - thông báo nào sẽ gửi cho người dùng.
5. Nút xác nhận dùng nội dung cụ thể, ví dụ **Phát hành giá 129.000đ từ 00:00 01/08/2026**, không dùng chữ “Lưu” chung chung.
6. Sau khi phát hành, hiển thị timeline lịch sử và cho phép **Tạo phiên bản giá khác**; không “undo” bằng cách sửa/xóa record cũ.

### Thuê bao hiện tại

Mặc định nên **grandfather**: thuê bao đang dùng giữ điều khoản đã mua đến hết hạn/kỳ hiện tại. Nếu sản phẩm sau này có tự động gia hạn, migration giá cần là workflow riêng gồm preview, ngày hiệu lực, quy tắc proration/credit, xử lý thanh toán thất bại và thông báo trước cho khách hàng. Đây là phần mà Stripe và Paddle đều tách khỏi thao tác quản lý catalog.

### Quyền và phê duyệt

- Tách `PackagePrice.Read`, `PackagePrice.Write` và nếu cần `PackagePrice.Approve`.
- Áp dụng least privilege; Stripe khuyến nghị cấp quyền thấp nhất cần thiết, còn Shopify tách hẳn quyền sửa giá. [Stripe — Manage organization access](https://docs.stripe.com/get-started/account/orgs/team), [Shopify — Store permissions](https://help.shopify.com/en/manual/your-account/users/roles/permissions/store-permissions)
- Các nguồn khảo sát không cho thấy phê duyệt hai người là yêu cầu mặc định cho mọi thay đổi giá. Vì vậy với đồ án/nhóm nhỏ, một admin có quyền riêng + xác nhận mạnh + audit log là đủ. Chỉ thêm maker-checker khi có nhiều admin, giá trị giao dịch cao hoặc yêu cầu kiểm soát nội bộ; khi đó người tạo không được tự duyệt.

## Mức ưu tiên đề xuất cho dự án

### P0 — nên làm trước khi cho dùng thật

- Thêm dialog review/xác nhận, hiển thị giá cũ và giá mới.
- Bắt buộc nhập lý do.
- Ghi audit đầy đủ và có trang xem lịch sử.
- Giữ nguyên nguyên tắc giá mới chỉ dùng cho payment được tạo sau thời điểm phát hành.
- Cảnh báo hoặc khóa thao tác nếu đang có payment `Pending` của gói; payment đó phải tiếp tục dùng `AmountVnd` đã snapshot.

### P1 — thiết kế bền vững

- Tạo bảng `package_price_versions` gồm `Id`, `PackageId`, `AmountVnd`, `EffectiveFrom`, `EffectiveTo`, `Status`, `CreatedBy`, `ApprovedBy`, `Reason`, `CreatedAt`.
- Payment tham chiếu `PackagePriceVersionId` ngoài snapshot `AmountVnd`.
- Catalog chọn phiên bản `Published` có hiệu lực tại thời điểm tạo checkout.
- Thêm lịch áp dụng và xử lý đồng thời bằng transaction/optimistic concurrency.

### P2 — chỉ khi nghiệp vụ phát triển

- Maker-checker/phê duyệt hai người.
- Preview và migration hàng loạt subscription tự động gia hạn.
- Proration, credit, retry/payment-failure policy và thông báo trước khi tăng giá.

## Quyết định đề xuất

**Giữ chức năng admin chỉnh giá, nhưng đổi ý nghĩa từ “sửa trực tiếp giá hiện tại” thành “phát hành phiên bản giá mới”.** Với phạm vi hiện tại, chưa cần bắt buộc hai người duyệt; cần ưu tiên review rõ ràng, lý do, audit, snapshot payment và không tác động hồi tố đến thuê bao đang dùng.

