---
version: 0.4.0
locale: vi
audience: [public, driver]
scope: global
publication_state: draft
review_status: pending
effective_from: null
effective_to: null
published_at: null
published_by: null
---

# Kiến thức SmartPark — bản mở rộng

Đây là nguồn biên tập tiếng Việt 0.4.0 cho FAQ công khai và người lái, kế thừa [bản lưu 0.3.1](../../luu-tru/knowledge/smartpark-knowledge-old.md). Nội dung dựa trên các quy tắc sản phẩm đã ghi nhận và mô tả tính năng theo khả năng được triển khai. Chính sách hiệu lực, trạng thái bãi và giao dịch cụ thể cần dữ liệu backend có thẩm quyền.

### Hướng dẫn biên tập và kiểm tra

- Dùng mã mục ổn định trong dấu ngoặc vuông và các từ khóa Việt/Anh phân cách bằng dấu chấm phẩy. Chỉ phần “Nội dung” của mục được đưa vào ngữ cảnh trả lời. Giữ căn cứ và ghi chú biên tập ở “Đối chiếu — không gửi đến AI”.
- Giữ đơn vị, điều kiện biên, cấu hình mặc định và ngoại lệ trong cùng đoạn quy tắc. Thêm các cách gọi đặc trưng để tìm đúng mục. Duy trì một nguồn nội dung tiếng Việt để diễn đạt sang tiếng Anh.
- Bản trong repo ở trạng thái nháp. Khi phát hành, tạo bản triển khai đã duyệt mang tên smartpark-knowledge.md cùng manifest khớp theo [Knowledge và xuất bản](../../knowledge-and-faq.md).

## [vehicle-guide] Loại xe được SmartPark phục vụ

Từ khóa: loại xe; hỗ trợ xe nào; ô tô; xe máy; vehicle; vehicles; vehicle types; car; motorcycle; motorbike; bike; bicycle; park a motorcycle

### Nội dung

Thiết kế SmartPark phân biệt ô tô và xe máy. Mỗi bãi cần cấu hình loại xe tương thích; sức chứa ô tô và xe máy không thay thế nhau. Loại xe khác với loại bãi ngoài trời, tầng hầm hoặc nhiều tầng. Không suy ra hỗ trợ xe tải, xe buýt hoặc sạc xe điện khi chưa có nguồn xác nhận. Việc bãi cụ thể có nhận xe và còn chỗ cần dữ liệu hiện hành của backend; hướng dẫn này không xác nhận chỗ trống.

Trong câu hỏi tiếng Anh, “bike” có thể chỉ xe máy hoặc xe đạp. Tài liệu này xác nhận xe máy thuộc loại xe được thiết kế hỗ trợ, tùy cấu hình từng bãi. Tài liệu chưa xác nhận việc hỗ trợ xe đạp.

### Đối chiếu — không gửi đến AI

- Nội dung hợp nhất từ chủ đề vehicle-guide của S04; bản nháp chưa công bố.
- [SRS discovery](../../../../01-requirements/srs/modules/03-discovery-visualization.md)

## [assistant-limits] Giới hạn của trợ lý SmartPark

Từ khóa: giới hạn trợ lý; assistant limitations; what can the assistant do

### Nội dung

Trợ lý khách chỉ hướng dẫn thông tin công khai; việc đọc xe hoặc lịch đặt của tài khoản cần đăng nhập và khả năng tra cứu đang được bật. Kết quả tra cứu phải do backend kiểm tra đúng tài khoản. Trợ lý không tự tạo booking, giữ chỗ, thanh toán, hoàn tiền hoặc điều khiển cổng. Tin nhắn “đồng ý” không thay xác nhận trên form. Khi chưa có nguồn chỗ trống đáng tin cậy, kết quả là chưa xác định. Hướng dẫn từ tài liệu được ghi rõ, không giả làm câu trả lời model hoặc kết quả giao dịch thật. Không gửi mật khẩu, OTP hoặc dữ liệu bí mật vào chat.

### Đối chiếu — không gửi đến AI

- Nội dung hợp nhất từ chủ đề assistant-limits của S04, phân biệt guest FAQ với tra cứu có đăng nhập; chưa công bố.
- [SRS AI](../../../../01-requirements/srs/modules/10-ai-assistance.md)

## [account-security-guide] Tài khoản, xe và bảo mật

Từ khóa: tài khoản; bảo mật; mật khẩu; đăng nhập; phương tiện đã lưu; xe của tôi; account; security; password; login; sign in; saved vehicle; registered vehicle; manage my vehicles; my car; mfa; totp; otp; authentication

### Nội dung

Bạn chỉ được tra cứu và quản lý dữ liệu tài khoản/xe thuộc quyền của mình. Biết mã booking, biển số hoặc mã hội thoại của người khác không cấp quyền xem thông tin của họ.

Đổi email/số điện thoại phải qua xác minh lại; đổi mật khẩu, thiết lập hoặc khôi phục xác thực phải dùng màn hình bảo mật hay quy trình hỗ trợ thông thường. Nếu đã bật xác thực hai bước TOTP, đăng nhập vẫn cần hoàn thành bước đó. Trợ lý không tắt, bỏ qua hoặc khôi phục yếu tố bảo mật thay bạn.

Không gửi mật khẩu, OTP, mã TOTP, mã khôi phục hoặc thông tin đăng nhập bí mật vào chat. Nếu gặp lỗi xác thực, mô tả loại lỗi và bước đang thực hiện, không cung cấp mã bí mật. Đăng nhập bằng OTP dùng email hoặc số điện thoại đã được xác minh và liên kết hợp lệ; đăng nhập không tạo thêm quyền mới.

Nếu mất quyền truy cập ứng dụng xác thực, dùng quy trình khôi phục trong ứng dụng nếu có. Đặt lại mật khẩu không bỏ yêu cầu TOTP đang bật. Một mã dự phòng chưa dùng có thể dùng trong luồng thay thế yếu tố xác thực sau bước xác minh chính. Nếu không còn cách xác minh cần thiết, hãy gửi yêu cầu hỗ trợ tài khoản qua kênh được công bố. Gửi yêu cầu không tự cấp lại quyền truy cập.

Gỡ xe cần kiểm tra các booking, lượt đỗ hoặc gói còn phụ thuộc vào xe. Gỡ xe không tự chuyển quyền biển số sang tài khoản khác. Nếu biển số đã được liên kết với tài khoản khác, xử lý qua quy trình tranh chấp/hỗ trợ và kiểm chứng phù hợp; không tiết lộ chủ tài khoản hiện tại hoặc tự chuyển chỉ vì người đó chưa phản hồi.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [FR-AUTH-02/03/06](../../../../01-requirements/srs/modules/01-identity-access.md#fr-auth-02)
- [FR-VEH-01/02](../../../../01-requirements/srs/modules/01-identity-access.md#fr-veh-01)
- [BR-AI-02/04](../../../../01-requirements/business-rules/10-ai-assistance.md#br-ai-02)
- [Khôi phục tài khoản và TOTP](../../../../01-requirements/srs/authentication-recovery.md)
- [SRS AI — Security](../../../../01-requirements/srs/modules/10-ai-assistance.md)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [booking-guide] Đặt chỗ, thời gian và xác nhận

Từ khóa: đặt chỗ; đặt trước; giữ chỗ; phân bổ; booking; book; reserve; reservation; allocation; minimum duration; maximum duration; hold time

### Nội dung

Để chuẩn bị đặt chỗ, đăng nhập rồi chọn loại xe cần gửi, tìm và chọn bãi phù hợp cùng thời gian bắt đầu/kết thúc. Sau đó chọn chiếc xe đã đăng ký thuộc loại xe đã tìm kiếm, chọn hình thức đặt được bãi hỗ trợ, xem giá/điều kiện và xác nhận trên form.

Một bãi có thể phục vụ một hoặc nhiều loại xe theo cấu hình. Sức chứa ô tô và xe máy được tính riêng, không thay thế nhau. Nếu đổi sang xe thuộc loại khác sau khi chọn bãi, cần kiểm tra lại bãi/khu vực tương thích, khả dụng và giá/điều khoản trước xác nhận. Loại xe khác với loại bãi như ngoài trời, tầng hầm hoặc tòa nhà nhiều tầng.

Trước khi xác nhận, bạn cần kiểm tra xe, bãi/khu vực, khoảng thời gian, chế độ đặt được bãi hỗ trợ và điều khoản hiển thị. Bãi có thể bật đặt theo ô ưu tiên, theo khu vực hoặc theo sức chứa tương thích. Theo ô ghi nhận ô mong muốn theo khả năng đáp ứng. Theo khu vực dùng phạm vi khu đã chọn. Theo sức chứa giữ quyền theo sức chứa phù hợp trong phạm vi bãi mà không chọn sẵn một ô. Các chế độ áp dụng theo lựa chọn bãi công bố. Chỗ thực tế được phân có thể khác vị trí mong muốn theo phương án phù hợp.

Các mặc định được công bố gồm thời lượng tối thiểu 15 phút, tối đa 24 giờ, đặt trước tối đa 7 ngày và thời điểm bắt đầu phân bổ trước giờ đặt 1 giờ. Đây là mặc định chính sách có thể được cấu hình hợp lệ, không phải giới hạn cố định cho mọi bãi. Hãy dùng điều khoản hiệu lực hiển thị cho giao dịch của bạn. Giá trị ví dụ 30 phút trước giờ đặt không phải mặc định phân bổ mới.

Đặt chỗ có giá dương cần hoàn tất phần trả trước trong thời hạn giữ của đơn, mặc định là 5 phút. Hình thức trả trước có thể là toàn bộ, theo tỷ lệ hoặc số tiền cố định. Quyền lợi gói hợp lệ được áp dụng trước mức tối thiểu đã công bố. Đơn thực sự có giá bằng 0 sau khi áp dụng điều kiện có thể được xác nhận mà không qua nhà cung cấp thanh toán, sau khi backend kiểm tra sức chứa và giới hạn đặt chỗ. Hết hạn giữ thanh toán và không đến theo booking là hai tình trạng khác nhau. Không suy ra đơn đã xác nhận chỉ từ việc bạn đã gửi yêu cầu hoặc thấy tiền bị trừ.

Nếu trợ lý chuẩn bị được thông tin đặt chỗ, bạn vẫn xem và xác nhận trên form. Tin nhắn “đồng ý” trong chat không tự gửi đơn. Giá, điều khoản hoặc khả dụng thay đổi so với lúc bạn đã xem cần được xem lại trước xác nhận. Điều khoản đã chấp nhận của booking không bị sửa lại chỉ vì có chính sách mới.

Một xe không được có các đặt chỗ chồng lấn không tương thích. Một bãi có thể bật giới hạn số đơn đang chờ thanh toán, đã xác nhận hoặc đã phân chỗ của mỗi tài khoản. Hạn chế tạo đơn mới do quy tắc chống lạm dụng hủy không tự tước quyền đăng nhập hay quyền đã có.

Đến sớm còn phụ thuộc chỗ thực tế phù hợp và phí thời gian thêm đã công bố. Nếu không có chỗ phù hợp, cần chờ hoặc dùng phương án thay thế được phép; không yêu cầu di dời xe đang đỗ chỉ vì mình có booking.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [BR-RES-01–04, BR-POL-03/04](../../../../01-requirements/business-rules/04-reservations-allocation.md#br-res-01)
- [RA-01](../../../../05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md#ra-01--effective-allocation-lead-time)
- [RA-06](../../../../05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md#ra-06--numeric-values-identified-as-defaults-are-configurable-policies)
- [BR-AI-03](../../../../01-requirements/business-rules/10-ai-assistance.md#br-ai-03)
- [Discovery — loại xe, loại bãi, cấu hình và luồng vào đặt chỗ](../../../../01-requirements/srs/modules/03-discovery-visualization.md)
- [Facility — sức chứa theo loại xe](../../../../01-requirements/business-rules/02-facility-capacity.md)
- Clarification 28/10/2026: user duyệt thứ tự loại xe/bãi/interval trước, xe đăng ký cụ thể sau; chưa công bố phiên bản runtime.
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [cancellation-refund-guide] Hủy đặt chỗ và hoàn tiền

Từ khóa: hủy; hoàn tiền; hoàn; no-show; cancel; cancellation; refund; refunds; reimbursement; unused; dispute charge

### Nội dung

Bạn thực hiện yêu cầu hủy hoặc hoàn tiền qua màn hình thông thường. Trợ lý có thể giải thích quy tắc chung; việc một booking cụ thể có được hủy và được hoàn bao nhiêu cần căn cứ trạng thái hiện tại, điều khoản đã chấp nhận và số tiền đủ điều kiện thực thu. Không hủy một lượt xe đang đỗ như thể booking chưa được sử dụng.

Mức mặc định cho tự nguyện hủy reservation: trước giờ bắt đầu từ 30 phút trở lên hoàn 100%; từ 15 phút đến dưới 30 phút hoàn 50%; dưới 15 phút hoàn 0%; no-show hoàn 0%. Đây là mức mặc định cho reservation, không tự áp dụng cho gói tháng/năm hoặc mọi nguyên nhân hoàn tiền. NO_SHOW là vắng mặt sau hạn đến áp dụng khi không có bằng chứng đã đến đúng hạn. Hết hạn đơn chưa thanh toán là một trạng thái riêng. Khoản hoàn của đơn cụ thể vẫn phải được xác nhận theo điều khoản và dữ liệu của đơn.

Cơ sở tính là số tiền đủ điều kiện đã thực thu, không thu phần còn thiếu như một khoản phạt hủy bổ sung. Trường hợp bãi không thể cung cấp dịch vụ theo quy tắc áp dụng được xử lý hoàn 100% giá trị chưa sử dụng đủ điều kiện; sửa tính phí sai là hoàn phần chênh lệch phù hợp. Không tự suy ra trường hợp của bạn thuộc các nhánh này khi chưa được kiểm tra.

Hủy booking, chấp thuận hoàn và thực trả tiền hoàn là các mốc khác nhau. Chấp thuận hoàn không có nghĩa tiền đã về. Hoàn tiền không xác nhận xe đã rời bãi hoặc giải phóng trạng thái đang đỗ. Muốn biết tiến độ phải tra trạng thái thực tế; nếu trợ lý chưa có khả năng đó, trợ lý sẽ nói rõ.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [BR-RES-03](../../../../01-requirements/business-rules/04-reservations-allocation.md#br-res-03)
- [BR-RES-05](../../../../01-requirements/business-rules/04-reservations-allocation.md#br-res-05)
- [BR-REF-01](../../../../01-requirements/business-rules/06-pricing-payments.md#br-ref-01)
- [BR-AI-03/04](../../../../01-requirements/business-rules/10-ai-assistance.md#br-ai-03)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [discovery-guide] Tìm bãi và đọc thông tin bản đồ

Từ khóa: tìm bãi; tìm chỗ đỗ; bản đồ; định vị; find parking; find a parking; search; nearby; map; location; directions; available spaces

### Nội dung

Bạn có thể tìm từ vị trí hiện tại nếu đồng ý cấp quyền định vị, hoặc nhập một điểm xuất phát. Cần chọn loại phương tiện; loại xe và loại bãi ngoài trời, tầng hầm hoặc nhiều tầng là các lựa chọn khác nhau. Phạm vi tìm mặc định là 5 km; khi không có kết quả, có thể mở rộng phạm vi.

Nếu chưa chọn ngày giờ, kết quả dùng tình trạng hiện tại và biểu giá hiện hành kèm đơn vị tính. Không thể suy ra tổng tiền khi chưa có khoảng đỗ. Nếu chọn khoảng thời gian, khả dụng và tiền ước tính phải ứng với loại xe và khoảng thời gian đó. Không so sánh trực tiếp một số giá theo giờ với giá theo ngày/đêm như cùng một tổng tiền.

Khoảng cách đường thẳng không phải quãng đường lái xe hay thời gian di chuyển. Chỉ tin thời gian di chuyển khi có nguồn thực tế. Chỉ dẫn đường có thể mở bản đồ ngoài ứng dụng khi chức năng đó khả dụng.

Bấm vào marker, chọn bãi hoặc xem vị trí trong mô hình 2D/3D không giữ chỗ. Sức chứa được kiểm tra lại khi xác nhận đặt chỗ. Cần lưu ý thời điểm cập nhật và cảnh báo dữ liệu cũ/mất kết nối. Bãi đầy vẫn có thể xuất hiện với nhãn đầy nếu bạn chưa lọc chỉ bãi còn chỗ.

Trợ lý ở chế độ hướng dẫn không có số chỗ trống hoặc giá trực tiếp của một bãi. Bạn cần kết quả tra cứu hiện hành; không có kết quả, bị từ chối định vị và lỗi tải dữ liệu là các tình huống khác nhau.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [Discovery — FR-CAP-01](../../../../01-requirements/srs/modules/03-discovery-visualization.md#fr-cap-01)
- [Discovery — FR-MAP-02/03](../../../../01-requirements/srs/modules/03-discovery-visualization.md#fr-map-02)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [monthly-yearly-guide] Gói tháng và gói năm cho người lái

Từ khóa: gói tháng; gói năm; vé tháng; vé năm; gia hạn; monthly; yearly; annual; pass; passes; subscription; renewal; prepaid

### Nội dung

Gói tháng/năm là quyền lợi đỗ xe cho người lái theo xe và phạm vi bãi/khu vực đã mua. Bãi phải bật sản phẩm tương ứng; có giá hoặc thiếu giá không tự chứng minh gói đang được bán.

Cả gói tháng và gói năm đều chỉ dùng khi còn chỗ phù hợp. Gói không giữ sức chứa lúc xe ở ngoài, không đảm bảo một ô riêng và không tự tạo reservation. Bạn có thể đặt booking thông thường riêng; dù có booking, mỗi lượt vào vẫn phải được xác nhận đủ điều kiện và có chỗ thực tế phù hợp, không được chiếm ô đang có xe. Xe đang đỗ không tự bị di dời khi gói hết hạn; phí phần ngoài thời gian bao phủ phải được tính theo quy tắc áp dụng.

Gói tháng dùng tháng theo lịch, không mặc định là 30 ngày hoặc luôn bắt đầu ngày đầu tháng. Chu kỳ liên tiếp giữ mốc ngày/giờ gốc; nếu tháng thiếu ngày đó thì chốt ở ngày cuối tháng, rồi quay lại mốc gốc khi có thể. Ví dụ ngày 31/1 → 28/2 (hoặc 29/2) → 31/3. Quyền có hiệu lực từ mốc bắt đầu đến trước mốc kết thúc.

Gói năm là 12 tháng lịch liên tiếp, không mặc định 365 ngày. Có thể mua trước tối đa một kỳ năm; các chính sách thời hạn thanh toán/gia hạn và hoàn tự nguyện của gói năm cần điều khoản riêng, không tự lấy từ gói tháng.

Số kỳ tháng trả trước có mặc định 3 kỳ liên tiếp, nhưng áp dụng giới hạn hiệu lực theo chính sách được phép. Cửa sổ gia hạn tháng có mặc định 7 ngày trước cuối kỳ đã cam kết; dùng thời điểm hiệu lực thực tế. Gói không tự gia hạn chỉ vì gửi hoặc nhận thông báo, và không tự trừ tiền.

Thanh toán thành công và quyền sử dụng được ghi nhận là hai điều kiện cần phân biệt. Với gói tháng, quyền chỉ được kích hoạt khi việc xác minh đủ tiền và ghi nhận quyền hoàn tất trước hạn áp dụng; đúng bằng hạn là trễ. Hạn giữ thanh toán gói tháng mặc định là 15 phút. Với mua bắt đầu ngay, hạn là lúc tạo đơn cộng thời hạn giữ có hiệu lực. Với mua cho kỳ tương lai hoặc gia hạn, lấy mốc sớm hơn giữa hạn đó và giờ bắt đầu kỳ. Nếu đã trả đúng hạn nhưng quyền được ghi nhận tại hạn hoặc sau hạn, gói tháng chưa được kích hoạt theo đơn đó và cần đối soát hoặc hoàn. Kiểm tra giao dịch đang chưa rõ trước khi trả thêm. Hủy gói phải kiểm tra booking tương lai còn phụ thuộc vào phần quyền sắp mất.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [M-01–04](../../../../01-requirements/business-rules/07-monthly-passes.md#m-01)
- [M-07/09](../../../../01-requirements/business-rules/07-monthly-passes.md#m-07)
- [RA-06](../../../../05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md#ra-06--numeric-values-identified-as-defaults-are-configurable-policies)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [pass-cancellation-guide] Hủy gói và xử lý khi bãi đóng cửa

Từ khóa: hủy gói tháng; hủy gói năm; hoàn tiền gói tháng; hoàn gói năm; bãi đóng cửa khi còn gói; cancel a monthly pass; yearly pass refund; pass cancellation; lot closure with a pass

### Nội dung

Trước khi tự nguyện hủy gói tháng hoặc năm, cần xem các đặt chỗ tương lai phụ thuộc vào phần quyền sắp mất và tự xác nhận hủy các đặt chỗ liên quan. Việc nhấn nút yêu cầu hủy gói không tự hủy đặt chỗ hoặc thay đổi phí. Nếu vẫn muốn đặt chỗ, có thể tạo đơn mới theo điều khoản hiện hành. Bãi kiểm tra lại các đơn còn phụ thuộc trước khi chấp nhận hủy gói. Mốc dùng tính khoản hoàn là thời điểm backend chấp nhận hủy sau khi đủ điều kiện.

Khi tự nguyện hủy gói tháng, trước giờ bắt đầu hoàn 100% tiền trả trước đủ điều kiện. Trong kỳ, hoàn 50% giá trị thời gian chưa sử dụng đủ điều kiện theo tỷ lệ thời gian còn lại. Đúng lúc kết thúc hoặc sau đó, kỳ đã kết thúc không còn giá trị chưa dùng. Các kỳ tương lai đã trả tiền được xét riêng. Gói năm áp dụng điều khoản tài chính riêng đã chấp nhận. Trợ lý không thể xác định số tiền thật từ hướng dẫn chung.

Khi bãi tạm đóng theo điều kiện được xác minh, gói giữ nguyên ngày hết hạn và xử lý hoàn giá trị khoảng thời gian không phục vụ đủ điều kiện. Nếu bãi đóng vĩnh viễn, xử lý hủy phần quyền còn lại và hoàn 100% giá trị chưa sử dụng đủ điều kiện. Phần đã được bù không hoàn lặp. Việc bãi đầy thông thường không tự trở thành sự cố không cung cấp dịch vụ.

### Đối chiếu — không gửi đến AI

- [M-05](../../../../01-requirements/business-rules/07-monthly-passes.md#m-05)
- [M-06](../../../../01-requirements/business-rules/07-monthly-passes.md#m-06)

## [parking-rules-guide] Trạng thái chỗ đỗ và quy tắc trong bãi

Từ khóa: trạng thái ô; quy tắc; mất vé; biển số; vào bãi; ra bãi; occupied; reserved; protected; occupancy; gate; entry; exit; check in; check out; lost ticket; lost my ticket; license plate

### Nội dung

Trạng thái có xe đang đỗ và trạng thái đặt trước/bảo vệ quyền đặt chỗ thể hiện hai thông tin khác nhau. Một ô có thể vừa “OCCUPIED” vừa “RESERVED” hoặc “PROTECTED”. Tổ hợp đó không chứng minh xe đang ở ô là xe của người có booking; thông tin liên kết đã xác nhận mới xác định được quan hệ, và không vì vậy mà được xem danh tính người lái khác.

Thời gian dự phóng chiếm chỗ “Occupancy Validity Timespan” có mặc định 4 giờ từ thời điểm đánh giá, nhưng dùng giá trị hiệu lực được cấu hình. Nếu đánh giá lúc 10:00 với giá trị 4 giờ, dự phóng đến 14:00 trừ khi có bằng chứng mới làm thay đổi trạng thái sớm hơn. Đây không phải cam kết xe sẽ ở đến 14:00, cũng không tự làm ô trống lúc 14:00.

Cửa sổ bảo vệ reservation có mặc định 4 giờ trước giờ bắt đầu nhưng dùng giá trị hiệu lực. Bảo vệ booking không đồng nghĩa ô vật lý đã trống và không cho phép di dời xe đang đỗ. Khi nhiều nhãn cùng liên quan một ô, không được coi mỗi nhãn là một ô bị chiếm khác; số chỗ còn dùng được cần kết quả đã xác minh.

Mã vé, nhận diện biển số hoặc cổng đã mở không tự chứng minh quyền vào/ra hay xe đã rời bãi. Khi nhận diện không rõ, sai lệch biển số/vé hoặc mất vé, cần Operator kiểm tra theo quy trình; trợ lý không mở cổng hoặc tự kết luận vi phạm.

Ô có dữ liệu chưa xác định không được hiểu là ô trống. Nếu bãi chỉ ghi nhận vị trí ở mức khu vực thì không suy ra một ô cụ thể. Hãy theo vị trí được phân thực tế; vị trí thay thế hợp lệ khác ô đã mong muốn không tự là vi phạm.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [RA-02/03/04/05](../../../../05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md#ra-02--occupied-does-not-identify-the-reservation-holder)
- [RA-06](../../../../05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md#ra-06--numeric-values-identified-as-defaults-are-configurable-policies)
- [BR-GATE-01–04, BR-EMERG-04](../../../../01-requirements/business-rules/05-sessions-operations.md#br-gate-01)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [payment-guide] Thanh toán, tiền tạm thu và trạng thái giao dịch

Từ khóa: thanh toán; trả tiền; phí; đỗ quá giờ; đỗ xe quá giờ; lấy xe trễ; lấy xe muộn; đỗ sau giờ kết thúc; overstay; late pickup; stay past reservation; tính phí; tiền tạm thu; pay; payment; payments; fee; fees; price; prices; cost; checkout; charged; free parking

### Nội dung

Xem số tiền và điều khoản trên màn hình thanh toán của giao dịch. Phí phụ thuộc loại xe, nơi đỗ, thời gian áp dụng, quyền lợi được bao phủ và điều khoản đã chấp nhận. Trợ lý không thay bảng tính phí của giao dịch và không xác nhận một giá trực tiếp từ hướng dẫn chung.

Ảnh chụp chuyển khoản, trang chuyển hướng hoặc thông báo tiền bị trừ chưa đủ chứng minh giao dịch đã được xác minh. Nếu trạng thái đang xử lý hoặc chưa xác định, cần kiểm tra lại chính giao dịch đó trước khi thanh toán thêm hoặc đổi sang trả tiền mặt. Không coi lỗi kết nối là giao dịch thất bại chắc chắn.

Ngưỡng miễn phí thông thường là tối đa 15 phút cho toàn bộ phiên đến thời điểm checkout được chấp nhận. Nếu vượt ngưỡng, thời gian chưa được bao phủ được tính đầy đủ theo quy tắc áp dụng, không trừ thêm 15 phút. Thời gian được gói bao phủ vẫn tính trong tổng thời gian xét ngưỡng. Ví dụ 60 phút được bao phủ và 10 phút không được bao phủ thì 10 phút sau có thể phải trả phí. Mức tối thiểu theo thời lượng booking đã công bố và nghĩa vụ trả trước vẫn áp dụng khi có; không kết luận mọi lượt đỗ ngắn đều miễn phí.

Nếu nhà cung cấp xác minh thanh toán sau khi đơn đặt chỗ đã hết hạn giữ, hệ thống chỉ có thể xác nhận đặt chỗ nếu lấy lại được chỗ phù hợp. Nếu không còn chỗ phù hợp, cần thông báo và xử lý đối soát hoặc hoàn khoản đã thu đủ điều kiện. Chỉ thử thu lại khi lần trước đã được xác nhận thất bại và được phép thử lại. Quy tắc cho tối đa ba lần thử thêm sau lần đầu. Gửi lại cùng yêu cầu hoặc nhận lại cùng thông báo không làm tăng số lần thử.

Nếu đỗ quá giờ kết thúc đã đặt, phần thời gian sử dụng ngoài thời gian đã đặt và chưa được quyền lợi hợp lệ bao phủ có thể phát sinh phí theo biểu giá và điều khoản hiệu lực của lượt đỗ. Thời gian đỗ được tính đến checkout đã được chấp nhận. Phụ phí riêng chỉ áp dụng khi có chính sách tương ứng đã được duyệt và công bố. Một cảnh báo quá giờ hoặc việc không nhận được cảnh báo không tự xác lập tiền phạt. Bãi vẫn ghi nhận chỗ có xe cho tới khi xác nhận xe đã rời thực tế.

Biểu giá có thể chia khung giờ ngày/đêm. Tính theo phần thời gian thực tế rơi vào từng khung áp dụng, cộng các phần chưa làm tròn rồi làm tròn một lần đến nghìn đồng gần nhất, với 500 đồng làm tròn lên. Qua nửa đêm không bắt đầu lại ngưỡng miễn phí. Xem riêng thời gian được bao phủ, mức tối thiểu đã công bố, khoản phát sinh, tiền trả trước, số còn phải trả hoặc số tiền dư.

Khoản phí được chốt tại checkout đã được chấp nhận; mở cổng và xe thực sự rời bãi là các sự kiện riêng. Tiền dư được xử lý theo quy trình hoàn/đối soát; checkout hoàn tất không tự chứng minh khoản hoàn đã về tài khoản.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [BR-PAY-01/02/04/06](../../../../01-requirements/business-rules/06-pricing-payments.md#br-pay-01)
- [BR-REF-01](../../../../01-requirements/business-rules/06-pricing-payments.md#br-ref-01)
- [BR-GATE-02](../../../../01-requirements/business-rules/05-sessions-operations.md#br-gate-02)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [smartpark-overview] SmartPark và phạm vi trợ lý AI

Từ khóa: smartpark; smartparking; trợ lý; assistant; chatbot; tổng quan; overview; giới thiệu; introduction; hello; xin chào; what is smartpark

### Nội dung

SmartPark hỗ trợ người lái tìm hiểu bãi đỗ, đặt chỗ, theo dõi lượt đỗ và các giao dịch liên quan theo tính năng đang được cung cấp. Trợ lý AI có thể giải thích hướng dẫn bằng tiếng Việt hoặc diễn đạt bằng tiếng Anh theo câu hỏi. Ở chế độ khách chưa đăng nhập, mỗi câu FAQ được xử lý riêng. Hội thoại đăng nhập có lưu lịch sử khi khả năng tương ứng được bật; không tự nhập lịch sử khách vào tài khoản.

Ở chế độ hỏi đáp hướng dẫn, trợ lý giải thích quy trình chung; việc tìm bãi còn chỗ, xem booking, giá, thanh toán hoặc hoàn tiền của bạn cần khả năng tra cứu tương ứng đang được bật và dữ liệu đã xác minh. Trợ lý sẽ nói rõ khi chưa tra cứu được.

Câu trả lời hướng dẫn không tạo booking, giữ chỗ, thanh toán hay hoàn tiền. Nếu có chức năng chuẩn bị đặt chỗ, bạn vẫn cần xem, sửa và xác nhận trên form thông thường. Trợ lý không thay đổi mật khẩu, quyền tài khoản hoặc điều khiển cổng bãi xe.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [SRS AI — FR-AI-01/02](../../../../01-requirements/srs/modules/10-ai-assistance.md#fr-ai-01)
- [BR-AI-01–05](../../../../01-requirements/business-rules/10-ai-assistance.md)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [support-guide] Yêu cầu hỗ trợ và theo dõi xử lý

Từ khóa: hỗ trợ; yêu cầu hỗ trợ; liên hệ; khiếu nại; support; help; contact; ticket; complaint; operator; admin; response time

### Nội dung

Hỗ trợ được thiết kế theo yêu cầu xử lý không đồng bộ, không phải chat trực tiếp có người trực liên tục. Vấn đề tại bãi được chuyển tới Operator phụ trách; vấn đề nền tảng, tài khoản hoặc tranh chấp liên kết biển số toàn hệ thống tới Admin theo phạm vi xử lý.

Bạn cần chủ động yêu cầu hỗ trợ. Khi chức năng tạo yêu cầu qua trợ lý chưa khả dụng, trợ lý chỉ hướng dẫn kênh đang được xác nhận; không báo đã tạo ticket hoặc đã chuyển người xử lý. Chỉ xác nhận gửi thành công khi có kết quả thực tế của yêu cầu.

Mô tả vấn đề và các tham chiếu cần thiết của chính bạn, như booking hoặc lượt đỗ liên quan, theo yêu cầu của kênh hỗ trợ. Không gửi mã bí mật hoặc thông tin cá nhân của người khác. Không mặc định sao chép toàn bộ hội thoại vào hồ sơ hỗ trợ.

Giờ phục vụ và thời gian phản hồi phụ thuộc thông tin được công bố của kênh xử lý. Không mặc định có hỗ trợ 24/7 hoặc cam kết trả lời trong một thời hạn chưa công bố. Yêu cầu đã tiếp nhận không có nghĩa vấn đề đã được giải quyết, tiền đã hoàn hoặc tài khoản đã khôi phục.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [FR-BAS-10/11](../../../../01-requirements/srs/modules/10-ai-assistance.md#fr-bas-10)
- [Permissions — support.case.handle](../../../../01-requirements/srs/permissions.md)
- [Account recovery support](../../../../01-requirements/srs/modules/08-notifications-reporting.md#account-recovery-notifications-and-support-type)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [troubleshooting-guide] Khi dữ liệu thiếu, lỗi hoặc chưa cập nhật

Từ khóa: lỗi; mất kết nối; chưa cập nhật; dữ liệu cũ; lỗi mạng; error; unavailable; stale; offline; failed; failure; missing; not loading; not updated; connection; troubleshooting

### Nội dung

Không có kết quả và không tải được dữ liệu là hai tình huống khác nhau. Nếu bãi hoặc booking chưa tải được, không thể kết luận bãi hết chỗ hay bạn không có booking. Khi ứng dụng báo dữ liệu cũ hoặc mất kết nối, hãy tải lại dữ liệu hiện hành trước khi quyết định.

Nếu thao tác đặt chỗ/thanh toán mất kết nối hoặc chưa rõ kết quả, kiểm tra trạng thái thao tác/giao dịch đó trước khi gửi một yêu cầu mới có thể tạo thêm booking hoặc thu tiền. Không tự trả thêm chỉ vì chưa thấy phản hồi. Lỗi hệ thống không chứng minh thanh toán thành công hoặc được miễn phí.

Nếu phiên đăng nhập không còn hợp lệ, dùng luồng đăng nhập thông thường. Nếu chỉ trợ lý hoặc nguồn dữ liệu đang không hoạt động, thông báo đó không chứng minh tài khoản bị khóa hay hết phiên.

Thông báo chưa tới không chứng minh booking, khoản thanh toán hoặc quyền lợi chưa được ghi nhận. Ngược lại, một thông báo được gửi cũng không thay thế trạng thái chính thức của giao dịch. Kiểm tra màn hình trạng thái hoặc kênh hỗ trợ đang được cung cấp.

Khi chưa có đủ căn cứ, trợ lý sẽ hỏi thêm thông tin cần thiết hoặc thông báo chưa thể xác nhận. Trợ lý không dùng dữ liệu mẫu để khẳng định trạng thái thật của bạn.

### Đối chiếu — không gửi đến AI

- Trạng thái: bản biên tập P1, chưa được Admin công bố; không dùng như corpus runtime hiện hành.
- [BR-AI-05](../../../../01-requirements/business-rules/10-ai-assistance.md#br-ai-05)
- [BR-PAY-04](../../../../01-requirements/business-rules/06-pricing-payments.md#br-pay-04)
- [FR-MAP-04](../../../../01-requirements/srs/modules/03-discovery-visualization.md#fr-map-04)
- [FR-RPT-01](../../../../01-requirements/srs/modules/08-notifications-reporting.md#fr-rpt-01)
- [BR-FAIL-03](../../../../01-requirements/business-rules/05-sessions-operations.md#br-fail-03)
- Baseline: nhánh AI-ChatBot, commit 22ef051da14afce47eebeea44d46ab3377ebabf2; yêu cầu sản phẩm không chứng minh tính năng đã triển khai.

## [arrival-and-no-show-guide] Đến bãi sau giờ bắt đầu và vắng mặt

Từ khóa: tới trễ; tới trễ thì sao; đến muộn; tới bãi trễ; trễ giờ đến; đến sau giờ bắt đầu; thời gian chờ đến; không đến bãi; late arrival; arrive late; what happens if i arrive late; missed reservation

### Nội dung

Nếu đến sau giờ bắt đầu, hãy xem hạn đến và trạng thái trong chi tiết đặt chỗ. Khi chính sách dung sai đến trễ được bật, giá trị mặc định là 15 phút sau giờ bắt đầu và bãi có thể cấu hình trong khoảng được phép 15–30 phút. Khi tắt dung sai, quyền của đặt chỗ còn đến giờ kết thúc. Đến đúng hạn hoặc trước hạn được tính là đúng giờ. Chỉ có thể kết luận vắng mặt (NO_SHOW) sau hạn áp dụng và khi không có bằng chứng có thẩm quyền cho thấy đã đến đúng hạn.

Vị trí GPS trên điện thoại không thay xác nhận đã đến của bãi. Nếu đã đến đúng hạn nhưng trạng thái hiển thị vắng mặt, hãy liên hệ nhân viên vận hành của bãi để đối chiếu bằng chứng. Chỗ phù hợp và quyền vào thực tế vẫn do bãi xác nhận. Hết thời hạn giữ đơn chưa thanh toán (EXPIRED) là một trạng thái riêng.

### Đối chiếu — không gửi đến AI

- [BR-RES-03](../../../../01-requirements/business-rules/04-reservations-allocation.md#br-res-03)
- [FR-RES-06](../../../../01-requirements/srs/modules/04-reservations-allocation.md#fr-res-06)
- [FR-RES-11](../../../../01-requirements/srs/modules/04-reservations-allocation.md#fr-res-11)

## [walk-in-and-entry-guide] Đến gửi xe trực tiếp và xác nhận vào bãi

Từ khóa: vào bãi không đặt trước; có cần đặt trước mới vào bãi được không; không đặt trước có gửi xe được không; không có tài khoản thì có gửi xe được không; gửi xe vãng lai; gửi xe trực tiếp; đến thẳng bãi; khách vãng lai; walk in; walk-in parking; no reservation required; parking without booking; park without a reservation

### Nội dung

Theo thiết kế, tại bãi có cung cấp luồng này, khách có thể đến gửi xe trực tiếp mà không cần tài khoản hoặc đặt chỗ trước. Việc được vào tùy loại xe bãi phục vụ, sức chứa phù hợp, trạng thái vận hành và xác nhận của bãi. Đến trực tiếp không giữ chỗ và bãi có thể từ chối tiếp nhận khi không đủ điều kiện.

Khi tới cổng, hãy theo hướng dẫn kiểm tra và cấp vé hoặc tham chiếu của bãi. Kết quả nhận diện biển số và thao tác mở cổng là các tín hiệu riêng. Lượt đỗ chỉ được xác lập khi hệ thống vận hành ghi nhận xe đã vào thực tế. Nếu cổng không nhận diện đúng hoặc có sai lệch giữa xe và vé, hãy nhờ Operator kiểm tra theo quy trình tại bãi.

### Đối chiếu — không gửi đến AI

- [SRS Sessions and Operations](../../../../01-requirements/srs/modules/05-sessions-operations.md)
- [BR-GATE-01/02/04](../../../../01-requirements/business-rules/05-sessions-operations.md#br-gate-01)

## [checkout-and-departure-guide] Trả xe, chốt phí và ra cổng

Từ khóa: lấy xe ra; làm thủ tục ra bãi; chốt phiên đỗ; xe chưa ra; cổng không mở; what if the exit gate doesn't open; chưa xác nhận rời bãi; checkout procedure; exit gate; leave the parking lot; actual departure

### Nội dung

Khi lấy xe, bãi kiểm tra xe, vé hoặc tham chiếu và phiên đỗ. Mốc checkout được chấp nhận là mốc chốt tiền. Điều kiện mở cổng thông thường còn gồm xác nhận nghĩa vụ thanh toán đã được xử lý. Mở cổng và xe thực sự rời bãi là hai sự kiện riêng. Chỗ chỉ được giải phóng khi bãi ghi nhận xe đã rời thực tế. Hệ thống không tính lại phí theo giờ rời thực tế sau một lần checkout đã được chấp nhận.

Nếu mất vé, cần Operator tra cứu được phép và đối chiếu bằng chứng lúc vào với thông tin xe hoặc tài khoản phù hợp. Chỉ trùng biển số chưa đủ xác minh. Phí mất vé chỉ áp dụng khi đã được công bố và có hiệu lực. Nếu đã thanh toán mà cổng chưa mở hoặc giao dịch chưa rõ, hãy để bãi kiểm tra chính phiên và giao dịch đó trước khi trả thêm.

### Đối chiếu — không gửi đến AI

- [BR-GATE-02–04](../../../../01-requirements/business-rules/05-sessions-operations.md#br-gate-02)
- [BR-PAY-04/06](../../../../01-requirements/business-rules/06-pricing-payments.md#br-pay-04)

## [allocation-alternative-guide] Được phân chỗ khác và yêu cầu đổi chỗ

Từ khóa: đổi ô đỗ; đổi vị trí đỗ; phân chỗ khác; được xếp chỗ khác; phương án thay thế; ô đã chọn có xe; reallocation; alternate space; assigned a different spot

### Nội dung

Đặt theo ô cụ thể ghi nhận ưu tiên cho ô đó. Bãi có thể phân một chỗ thay thế tương thích theo điều khoản đã chấp nhận. Nếu ô mong muốn đang có xe, không được yêu cầu di dời xe đó chỉ vì có đặt chỗ. Khi bãi mất khả năng cung cấp chỗ đã phân, Operator phụ trách xử lý tìm phương án tương thích. Nếu không có phương án phù hợp khả thi, áp dụng quy trình bãi không cung cấp được dịch vụ. Nếu từ chối phương án hợp lệ và có thể sử dụng, áp dụng điều khoản tự nguyện hủy.

Nếu có đề nghị chỗ thay thế, cần chấp nhận trước hạn hiển thị. Đúng bằng hạn là hết hạn. Theo mặc định, người lái có tối đa hai yêu cầu phân lại được chấp nhận. Số lượt thực tế theo chính sách có hiệu lực. Gửi lại do lỗi truyền tải, yêu cầu không hợp lệ hoặc bãi tự tìm chỗ thay thế để khôi phục dịch vụ không tính vào số lượt đó. Việc phân lại sang chỗ hợp lệ tự nó không phát sinh khoản hoàn tiền. Các căn cứ hoàn tiền độc lập vẫn được xét.

### Đối chiếu — không gửi đến AI

- [BR-RES-08 và BR-POL-01](../../../../01-requirements/business-rules/04-reservations-allocation.md#br-res-08)
- [RA-06](../../../../05-project-management/decision-log/2026-10-25-reservation-allocation-state-clarification.md#ra-06--numeric-values-identified-as-defaults-are-configurable-policies)

## [invoice-guide] Hóa đơn và chứng từ thanh toán

Từ khóa: hóa đơn; hóa đơn PDF; chứng từ thanh toán; invoice; parking invoice; payment receipt; pdf invoice

### Nội dung

Theo thiết kế SmartPark, chứng từ PDF được phát hành cho nghĩa vụ tài chính đã được xác nhận thanh toán và các khoản điều chỉnh tương ứng. Xem giao dịch đã xác nhận và dùng chức năng chứng từ của giao dịch nếu chức năng đó đang được cung cấp. Ảnh chụp chuyển khoản hoặc trang chuyển hướng của ứng dụng thanh toán chưa đủ xác nhận hệ thống đã ghi nhận giao dịch. Nếu khoản hoàn mới được chấp thuận, cần chờ kết quả chi trả đã xác minh.

Chỉ dùng quyền truy cập được cấp để xem chứng từ. Nếu thấy sai khoản tiền hoặc thiếu chứng từ sau khi giao dịch đã được xác nhận, dùng kênh hỗ trợ được cung cấp và chỉ gửi tham chiếu cần thiết của giao dịch. Trợ lý không tự phát hành chứng từ hay xác nhận thanh toán.

### Đối chiếu — không gửi đến AI

- [BR-PAY-03/04 và BR-REF-01](../../../../01-requirements/business-rules/06-pricing-payments.md#br-pay-03)

## [notification-guide] Thông báo và cách xác nhận trạng thái

Từ khóa: thông báo; không nhận thông báo; không nhận được thông báo phải làm sao; nhắc đặt chỗ; tin nhắn nhắc; notification; missing notification; reminder; status update

### Nội dung

SmartPark thiết kế thông báo để nhắc và cập nhật sự kiện liên quan đến đặt chỗ, lượt đỗ, thanh toán, quyền lợi hoặc tài khoản. Kênh gửi và nội dung phụ thuộc tính năng đang được triển khai và loại sự kiện. Chỉ dùng kênh được công bố cho tình huống đó.

Nếu không nhận được thông báo, kiểm tra trạng thái ngay trong chức năng liên quan. Thông báo không đến không chứng minh đặt chỗ thất bại hoặc khoản tiền chưa được ghi nhận. Nhận thông báo cũng không thay việc xác nhận trạng thái mới nhất của đơn, phiên đỗ hoặc khoản hoàn. Nhắc gia hạn không tự gia hạn hoặc tự trừ tiền.

### Đối chiếu — không gửi đến AI

- [SRS thông báo và báo cáo](../../../../01-requirements/srs/modules/08-notifications-reporting.md)
- [BR-PAY-04](../../../../01-requirements/business-rules/06-pricing-payments.md#br-pay-04)

## [incident-and-appeal-guide] Sai ô đỗ, sai phí và khiếu nại

Từ khóa: đỗ sai ô; bị ghi nhận sai ô; khiếu nại sai phí; báo thu sai; bị thu sai phí phải làm gì; khiếu nại tại bãi; wrong parking space; wrong slot; parking incident; billing appeal; incorrect charge

### Nội dung

Nếu cho rằng bị ghi nhận sai ô hoặc thu sai, hãy gửi yêu cầu theo kênh của bãi và nêu vị trí đã được phân, thời điểm hoặc tham chiếu giao dịch liên quan. Operator phụ trách đối chiếu với phân bổ và bằng chứng thực tế. Đỗ đúng vị trí thay thế đã được chấp nhận không tự trở thành vi phạm vì khác ô mong muốn. Ghi nhận nghi ngờ, nhận diện bằng camera và quyết định sau khi xem xét là các bước riêng.

Khiếu nại được tiếp nhận không đồng nghĩa đã chấp nhận. Nếu phát hiện thu sai, khoản hoàn chỉ là phần chênh lệch đủ điều kiện. Quyết định hoàn và việc thực trả là hai mốc riêng. Owner xử lý trường hợp được chuyển cấp theo quy trình.

### Đối chiếu — không gửi đến AI

- [BR-VIOL-02–04](../../../../01-requirements/business-rules/05-sessions-operations.md#br-viol-02)
- [BR-REF-01](../../../../01-requirements/business-rules/06-pricing-payments.md#br-ref-01)

## [roles-and-partner-guide] Vai trò và đăng ký đối tác bãi đỗ

Từ khóa: đăng ký chủ bãi; mở thêm bãi; phê duyệt bãi; quản lý bãi; vai trò SmartPark; parking lot owner registration; lot owner; parking operator; add a parking lot

### Nội dung

Driver dùng các chức năng và dữ liệu cá nhân được cấp quyền. Operator xử lý lượt đỗ, thu tiền và sự cố tại bãi được phân công. Owner quản lý bãi thuộc quyền của mình, cấu hình chính sách trong giới hạn được phép và quản lý phân công Operator. Admin xét duyệt hồ sơ, xử lý các vấn đề nền tảng và tài khoản theo quyền được cấp.

Theo thiết kế, đăng ký đối tác ban đầu yêu cầu xác minh VNeID mô phỏng có sự đồng ý. Cần lưu thiết lập bãi đầu tiên và các tài liệu bắt buộc trước bước OTP email. Phải xác minh email trước khi Admin xét duyệt. Xét duyệt, cấp phát bãi và mở bãi phục vụ là các mốc riêng. Owner chỉ đăng nhập bình thường sau khi cấp phát bãi đầu tiên thành công. Bãi vẫn cần cấu hình và kiểm tra sẵn sàng trước khi mở. Mỗi bãi bổ sung có hồ sơ xét duyệt riêng. Việc một bãi bổ sung bị từ chối không hủy quyền của Owner hay trạng thái các bãi khác.

VNeID mô phỏng là bước của SmartPark. Kết quả mô phỏng không có giá trị xác minh danh tính của cơ quan nhà nước. Làm theo hướng dẫn trong ứng dụng khi luồng tương ứng khả dụng.

### Đối chiếu — không gửi đến AI

- [SRS v1.1 về vai trò và luồng chính](../../../../01-requirements/srs/smartpark-srs-v1.1.md#2-product-scope-and-users)
- [BR-CAP-01](../../../../01-requirements/business-rules/02-facility-capacity.md#br-cap-01)
- [FR-AUTH-01 và FR-AUTH-05](../../../../01-requirements/srs/modules/01-identity-access.md#fr-auth-01)
