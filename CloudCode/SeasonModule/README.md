# SeasonModule — lịch mùa xếp hạng (UGS Cloud Code, C#)

Mùa giải **tính theo đồng hồ**: `config/season` chỉ lưu mốc (mùa số mấy bắt đầu lúc nào), độ dài mùa
và số phút khóa điểm. Mùa `k` bắt đầu lúc `anchorStart + (k − anchorSeasonId) × duration`, nên mùa mới
mở **đúng giờ** dù Cloud Code chạy trễ. Client và Security Rules tự tính mùa đang chạy từ dữ liệu này.

```text
UGS Scheduler (season-tick, cron 0 * * * *  — UGS chỉ cho tối thiểu mỗi giờ)
  → Trigger → Cloud Code SeasonModule/AdvanceSeason(durationHours, lockMinutes)
  → chưa có config/season: tạo lịch, mùa 1 = chu kỳ chứa "bây giờ", căn theo 00:00 giờ VN
  → payload khác cài đặt hiện tại: ghi lịch "pending" áp dụng từ mùa SAU mùa đang chạy
  → ghi seasons/{mùa hiện tại} và seasons/{mùa trước}.closedAt (danh sách lịch sử ở sidebar)
  → tạo hàng trống (0 điểm = Đồng, tỉ lệ thắng 0) cho mọi tài khoản trong users/ chưa có hàng ở mùa đang chạy;
    tài khoản cũ chưa có username được cấp username trước (giống game làm khi đăng nhập)
```

Bảng xếp hạng sắp theo **điểm giảm dần → tỉ lệ thắng giảm dần → username tăng dần**. Tỉ lệ thắng được
lưu ở trường `winRate` (phần vạn: `wins * 10000 / matches`, làm tròn xuống), Rules kiểm tra đúng công thức.
Người chơi đăng nhập (hoặc mở bảng xếp hạng) cũng tự tạo hàng trống của mình, nên tài khoản mới không phải đợi lần chạy kế tiếp.

Idempotent: chạy lại nhiều lần không đổi gì; hai lần chạy song song thì precondition `updateTime`
làm một lần thất bại.

## Cài đặt (làm một lần)

1. **Service account Firebase**: Firebase Console → Project settings → Service accounts →
   *Generate new private key*. Không commit file JSON này.
2. **UGS Secret Manager**: Dashboard → Administration → Secrets → secret `FIREBASE_SERVICE_ACCOUNT`,
   giá trị là toàn bộ nội dung file JSON.
3. **Deploy module**: Unity → Services → Deployment (hoặc Ctrl+K gõ "Deployment") → tick
   `SeasonModule.ccmr` → Deploy Selected. Cần .NET SDK 6+.
4. **Schedule** (Dashboard → Triggers → Schedules → Create): Name/Event name `season-tick`,
   Recurring, Expression `0 * * * *`, Payload `{"durationHours": 24, "lockMinutes": 30}`.
5. **Trigger** (Dashboard → Triggers → Create): event của schedule `season-tick` →
   Cloud Code module `SeasonModule`, function `AdvanceSeason`.
6. **Chặn người chơi gọi hàm**: Access Control → policy *Deny*
   `urn:ugs:cloud-code:/v1/projects/*/modules/SeasonModule/*` cho player. Hàm cũng tự từ chối khi có PlayerId.
7. Kiểm tra: sau lần chạy đầu (≤ 1 giờ, phút 0), Firestore có `config/season` và `seasons/1`.

**Đổi độ dài mùa / phút khóa**: UGS không cho sửa schedule → xóa schedule `season-tick`, tạo lại cùng
tên với payload mới (trigger giữ nguyên). Lần chạy kế tiếp ghi lịch pending; mùa đang chạy giữ nguyên.

Muốn reset ngay một giờ khác 00:00 VN: sửa `SeasonSchedule.AlignmentOffsetMs` trước lần chạy đầu, hoặc
xóa `config/season` khi chưa có ai đánh (mùa 1 sẽ được tạo lại).

## Test trên emulator (không cần UGS)

```bash
cd Firebase && npm install && npx firebase emulators:start --only firestore --project demo-kltn
# terminal khác:
cd CloudCode/SeasonModule && dotnet run --project SeasonModule.Tests
```

13 kiểm tra: thay config bản cũ, mùa 1 căn 00:00 VN, chạy lại không đổi gì, chạy trễ 59 phút vẫn thấy mùa 2 mở đúng giờ,
mùa cũ có `closedAt`, đổi cài đặt đồng thời chỉ ghi một lần, độ dài mới áp dụng từ mùa sau, lịch pending
được thăng cấp, tạo hàng trống cho mọi tài khoản, cấp username cho tài khoản cũ (thử lại khi trùng tên), bổ sung `winRate` cho hàng thiếu, chạy lại không ghi thêm.

## Chưa kiểm chứng

- Việc Trigger truyền `Payload` của schedule thành tham số `durationHours`/`lockMinutes` của hàm.
  Nếu log Cloud Code báo thiếu tham số, gửi log để sửa. (`deploy/*.sched|.tr` chỉ dùng với UGS CLI.)
- `modulePath` tương đối trong `.ccmr` (`Matchmaker.ccmr` hiện dùng đường dẫn tuyệt đối). Nếu
  Deployment window báo không thấy module, đổi thành đường dẫn tuyệt đối tới `SeasonModule.sln`.

## Matchmaker: chỉ ghép rank chênh ≤ 1 bậc

Client gửi `rank_tier` (0 = Đồng … 6 = Thách đấu) trong custom data của ticket
(`DedicatedMatchmakerClient`). Trong Dashboard → Matchmaker → Queue → Pool → Rules, thêm:

```json
{
  "source": "Players.CustomData.rank_tier",
  "name": "rank-within-one-tier",
  "type": "Difference",
  "reference": 1,
  "enableRule": true,
  "not": false
}
```

`[Chưa kiểm chứng]` cú pháp rule trên Dashboard. Có thể thêm relaxation (nới lên 2 sau 60s) nếu
hàng chờ ít người. Lưu ý: `rank_tier` do client tự khai → client sửa đổi có thể khai sai (nợ kỹ thuật).
Host/Client (listen) không đi qua Matchmaker nên không bị giới hạn rank.
