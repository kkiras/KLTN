# Kế hoạch: Leaderboard theo mùa + Rank + tiền đề phân trận theo rank

> Trạng thái: **bản kế hoạch, chờ duyệt** (16/10/2026). Chưa có dòng code nào được viết.
> Đọc kèm: `.agents/AGENTS.md`, `.agents/memory.md` §11 (kinh tế + Firestore), `Firebase/firestore.rules`.
> Nhãn: `[Đã kiểm chứng trong code]` = đã đọc trong repo; `[Chưa kiểm chứng]` = cần thử trên Firebase/UGS thật;
> `[Mô hình tổng quát]` = hành vi chung, có thể khác theo dịch vụ.

Các mục cần bạn quyết định được đánh dấu **❓ QUYẾT ĐỊNH Dn**, gom lại ở §11.

---

## 0. Giả định ẩn trong yêu cầu (đọc trước)

Trước khi thiết kế, có 3 giả định cần gọi tên vì chúng quyết định toàn bộ kiến trúc:

```
Giả định 1: "Reset rank mỗi 24h cần live server (một tiến trình chạy liên tục để reset)."
  → Không hẳn. Reset chỉ cần server nếu ta MUỐN reset là một SỰ KIỆN (xoá/ghi đè dữ liệu lúc 00:00).
    Nếu dữ liệu được PHÂN VÙNG THEO MÙA (mỗi mùa một collection), "reset" chỉ là
    "đổi phân vùng hiện tại" — một phép tính từ thời gian, không ai phải chạy gì cả.
    (Giống log rotation theo ngày: không ai "xoá log cũ", chỉ là hôm nay ghi vào file mới.)

Giả định 2: "Thứ cần server là reset."
  → Thứ thật sự cần server là ĐỘ TIN CẬY CỦA ĐIỂM. Hiện tại mỗi client TỰ GHI kết quả trận của mình
    (MatchResultPresenter → ClaimMatchRewardAsync) [Đã kiểm chứng trong code]. Với xu/bạc, gian lận
    chỉ hại chính người gian lận. Với leaderboard CÔNG KHAI, một client sửa đổi có thể bịa matchId
    và tự cộng "win" vô hạn → đứng top 1. Đây là nợ #12 trong AGENTS.md, nay bị khuếch đại.

Giả định 3: "Reset 24h + xếp trận theo rank" là hai tính năng độc lập.
  → Chúng xung đột: nếu mỗi 24h mọi người về Đồng, thì đầu mỗi ngày ai cũng cùng nhóm Đồng
    → phân trận theo rank gần như vô nghĩa vài giờ đầu mỗi mùa, và rank đo "độ chăm chơi trong ngày"
    nhiều hơn "trình độ". 24h hợp cho DEMO/kiểm thử KLTN; vận hành thật nên 1–4 tuần.
    → Vì vậy độ dài mùa để trong Inspector là đúng, nhưng mặc định 24h nên ghi rõ là "chế độ demo".
```

---

## 1. Phạm vi

| Có trong kế hoạch | Ngoài phạm vi (theo yêu cầu) |
|---|---|
| Scene `LeaderboardScene` + nút "Leaderboard" ở MainMenu + nút Back | Bảo vệ tụt rank |
| Bảng 5 cột: STT, Username, Rank (icon + tên), Điểm, Tỉ lệ thắng | Elo / MMR |
| Top 1–3 nổi bật, hàng của mình tô vàng nhạt, hàng "dính đáy" khi ngoài tầm nhìn | Thưởng cuối mùa |
| Leaderboard theo mùa, tiêu đề "Mùa N", menu trái xem mùa cũ | Ghép trận theo rank **chạy thật** (chỉ làm tiền đề) |
| Reset mùa theo chu kỳ chỉnh trong Inspector (mặc định 24h) | |
| Bảng điểm cộng/trừ cố định + ngưỡng 7 rank (để bạn duyệt) | |
| Username duy nhất `userXXXXXX` khi đăng ký + bổ sung cho user cũ | |
| Tiền đề phân trận: gắn rank vào ticket matchmaker, Host/Client không lọc | |

---

## 2. Reset mùa: các phương án và đánh đổi — ❓ QUYẾT ĐỊNH D1

### 2.1 Mô hình tư duy

```
Mở rộng:         time-bucketed partition (log rotation, bảng partition theo ngày trong DB)
Xung đột:        giả định "reset = một job xoá/ghi đè dữ liệu tại một thời điểm"
Tiên quyết:      đồng hồ tin cậy (server time vs client clock), idempotency, Security Rules request.time
Rủi ro dùng sai: tin đồng hồ client để quyết định mùa → client chỉnh giờ máy là ghi vào mùa cũ/mùa sau
```

### 2.2 Ba phương án

**A. Mùa "lười" tính từ thời gian, không server (KHUYẾN NGHỊ cho giai đoạn này)**

```
Cấu hình: anchorSeason = 1, anchorStartUtc = 2026-10-20T00:00Z, durationHours = 24  (SeasonConfig.asset)
Client ghi kết quả trận lúc T
  → seasonId = anchorSeason + floor((T − anchorStart) / duration)   (Domain: SeasonClock, thuần C#)
  → ghi vào seasons/{seasonId}/entries/{uid}
  → Security Rules tính lại seasonId từ request.time (đồng hồ GOOGLE, không phải client)
       khác nhau → từ chối                                        [Chưa kiểm chứng: cú pháp math.floor/toMillis]
Hết mùa: không ai làm gì. Mùa cũ tự "đóng băng" vì Rules từ chối mọi ghi có seasonId ≠ mùa hiện tại.
Xem mùa cũ: đọc seasons/{n}/entries — dữ liệu vẫn nguyên.
```

- Cấu hình nhúng vào `firestore.rules` bằng chính cơ chế đã có (Export Firestore Rules, giống bảng giá Shop).
- Đổi `durationHours` trong Inspector ⇒ phải Export Rules + dán lên Console (như đổi giá thẻ).

**B. Firebase Cloud Functions + Cloud Scheduler (cron 24h)**

```
Cloud Scheduler (00:00) → Function (Node, Admin SDK)
  → chụp top N mùa cũ vào seasons/{n}/final, ghi config/season.current = n+1
  → (tuỳ chọn) phát thưởng cuối mùa
```

**C. UGS Cloud Code + UGS Scheduler/Triggers** `[Chưa kiểm chứng: gói UGS hiện tại có Scheduler]`

```
UGS Scheduler → Cloud Code module C# → Firestore (service account)
```
Cùng hướng với "giai đoạn 2" đã ghi trong memory.md §11 (server ghi thưởng qua Cloud Code).

### 2.3 Bảng đánh đổi

| Tiêu chí | A. Lười, không server | B. Cloud Functions | C. UGS Cloud Code |
|---|---|---|---|
| Hạ tầng mới | Không | Firebase **Blaze** (gắn thẻ thanh toán), Node | Module C# mới, secret service account |
| Có "sự kiện hết mùa" (chốt top, phát thưởng) | Không | Có | Có |
| Đồng hồ tin cậy | Có (`request.time` trong Rules) | Có | Có |
| Điểm đáng tin (chống bịa kết quả) | **Không** — vẫn client tự ghi | Chỉ nếu Function cũng ghi điểm | Chỉ nếu Cloud Code cũng ghi điểm |
| Đổi độ dài mùa | Export Rules lại | Đổi cron | Đổi lịch |
| Rủi ro khi job lỗi | Không có job để lỗi | Cron lỗi ⇒ mùa không chuyển | Như B |
| Chi phí học/thời gian KLTN | Thấp | Trung bình–cao | Trung bình (đã có Cloud Code) |

**Điểm mấu chốt:** A, B, C khác nhau ở chỗ **có sự kiện hết mùa hay không**, KHÔNG khác nhau ở độ tin
cậy của điểm. Độ tin cậy của điểm là bài toán "ai ghi kết quả trận" (§6), độc lập với reset.

**Khuyến nghị:** A ngay bây giờ; thiết kế dữ liệu sao cho B/C có thể thêm sau (chỉ thêm `seasons/{n}/final`)
mà không đổi cấu trúc entries.

### 2.4 Kịch bản thất bại của phương án A (và cách xử lý)

| Kịch bản | Cơ chế | Xử lý |
|---|---|---|
| Trận kết thúc 23:59:58, commit tới Google lúc 00:00:01 | Client tính mùa N, Rules tính N+1 → `PERMISSION_DENIED` | Gateway thử lại **1 lần** với mùa N+1. Trận vắt qua ranh giới tính cho mùa mới |
| Đồng hồ máy người chơi lệch 10 phút | Client hiển thị sai đồng hồ đếm ngược, ghi sai mùa sát ranh giới | Lấy giờ server từ header `Date` của response Firestore → lưu `serverOffset`; mọi tính toán dùng `UtcNow + offset` |
| Đổi `durationHours` giữa mùa | Công thức tính lại ⇒ số mùa nhảy lung tung (mùa 5 thành mùa 9) | Editor tool **re-anchor**: `anchorSeason = mùaHiệnTại + 1`, `anchorStart = lúc kết thúc mùa hiện tại` (hoặc "bắt đầu ngay") |
| Rules trên Console chưa cập nhật sau khi đổi Inspector | Client và Rules tính mùa khác nhau ⇒ mọi ghi bị từ chối | Log rõ `[Leaderboard] Season mismatch`; checklist "Export Rules" trong skill |
| Mùa không có ai chơi | Không có document nào | UI hiện "Mùa này chưa có dữ liệu" |

---

## 3. Bảng điểm và ngưỡng rank — ❓ QUYẾT ĐỊNH D2, D3

### 3.1 Điểm mỗi trận (chỉ trận xếp hạng, xem §6.1)

| Kết quả | Điểm | Lý do |
|---|---|---|
| Thắng (kể cả đối thủ đầu hàng) | **+30** | |
| Thua | **−20** | Không âm dưới 0 trong mùa |
| Hòa | **0** | Hiện game có `MatchOutcome.Draw` [Đã kiểm chứng trong code] |
| Tự đầu hàng (bất kỳ vòng nào) | **−20** | Như thua; không phạt nặng hơn vì chưa có cơ chế bảo vệ |
| Rớt mạng giữa trận | 0 (trận bị hủy, không ghi) | Giữ nguyên hành vi hiện tại (nợ #14) |

**Vì sao +30/−20 chứ không +25/−25?** — đây là đánh đổi bạn nên nhìn thấy:

```
Điểm hoà vốn (tỉ lệ thắng để đứng yên) = Thua / (Thắng + Thua)
  +25/−25 → 50%   : rank thuần trình độ, nhưng người 50% đứng yên ở Đồng cả mùa
  +30/−20 → 40%   : người 50% vẫn lên trung bình +5/trận → rank đo cả "chăm chơi"
Với mùa 24h, người chơi chỉ đánh ~10–20 trận/ngày ⇒ cần "trôi dương" để rank có ý nghĩa trong 1 ngày.
Mùa dài (1–4 tuần) có thể chuyển về +25/−25 mà không đổi code (chỉ đổi hằng số + Export Rules).
```

### 3.2 Ngưỡng rank (điểm tối thiểu của mùa)

| Rank | Ngưỡng | Khoảng cách | Thắng liên tiếp từ 0 | Số trận ở 60% thắng (+10/trận) |
|---|---|---|---|---|
| Đồng | 0 | — | 0 | 0 |
| Bạc | 60 | 60 | 2 | 6 |
| Vàng | 150 | 90 | 5 | 15 |
| Bạch kim | 270 | 120 | 9 | 27 |
| Kim cương | 420 | 150 | 14 | 42 |
| Cao thủ | 600 | 180 | 20 | 60 |
| Thách đấu | 800 | 200 | 27 | 80 |

- Khoảng cách tăng dần ⇒ rank cao khó hơn. Trong 24h, người chơi tích cực (~15–20 trận, 55–60%) đạt Vàng–Bạch kim;
  Thách đấu gần như chỉ đạt được bằng chuỗi thắng dài — đúng tinh thần "hiếm".
- Rank **không lưu** trong DB, luôn **suy ra từ điểm** (`RankPolicy.TierFor(points)`). Một nguồn sự thật duy nhất ⇒
  đổi ngưỡng không cần migrate dữ liệu.

❓ **D3 — Thách đấu:** (a) ngưỡng cố định 800 *(khuyến nghị: tính được cục bộ, dùng ngay cho matchmaking)*;
hay (b) "top 10 trong số người ≥ 600" (giống game thương mại, nhưng rank của mình phụ thuộc người khác ⇒ phải query
mới biết, matchmaking khó dùng).

---

## 4. Cấu trúc dữ liệu Firestore

### 4.1 Hiện trạng [Đã kiểm chứng trong code]

```text
users/{uid}                        coins, silver, schemaVersion, createdAt, updatedAt, lastPurchase, lastRewardMatchId
users/{uid}/ownedCards/{cardId}    acquiredAt, source
users/{uid}/matchRewards/{matchId} result, coins, silver, createdAt
```
`users/{uid}` chỉ chủ sở hữu đọc được (`allow read: if isOwner(uid)`). ⇒ **Leaderboard không thể đọc `users/`**;
cần dữ liệu công khai riêng.

### 4.2 Bổ sung

```text
usernames/{username}                         uid, createdAt                       ← khoá duy nhất (create-only)
users/{uid}                                  + username                           ← thêm field, ghi 1 lần
seasons/{seasonId}/entries/{uid}             username, points, wins, losses, draws, matches,
                                             lastMatchId, updatedAt                ← 1 doc / người / mùa
seasons/{seasonId}/entries/{uid}/results/{matchId}   result, delta, createdAt     ← chống ghi trùng 1 trận
```

`seasonId` là chuỗi số thuần (`"1"`, `"2"`, ...) — dùng làm ID document và parse lại được.

**Vì sao mỗi người một document mỗi mùa (không phải 1 doc chứa cả bảng)?**

```
Một doc "leaderboard" chứa mảng top 100:
  ✗ mọi trận đều ghi cùng 1 doc → tranh chấp ghi (Firestore ~1 ghi/giây/doc [Mô hình tổng quát])
  ✗ client phải sắp xếp lại và ghi đè cả mảng → lost update khi 2 trận kết thúc cùng lúc
Một doc / người:
  ✓ mỗi người chỉ ghi doc của mình → Rules kiểm "uid == request.auth.uid" dễ dàng
  ✓ sắp xếp do Firestore làm bằng index (orderBy points desc)
  ✗ cần composite index + query, đọc N document mỗi lần mở bảng
```

**Vì sao `username` chép (denormalize) vào entry thay vì join?** Firestore không có join; `users/` lại riêng tư.
Username bất biến ⇒ chép một lần không bao giờ lệch.

**Vì sao `results/{matchId}` thay vì dùng lại `matchRewards/{matchId}`?** Tách ghi điểm khỏi ghi tiền thành
2 batch độc lập: Rules leaderboard lỗi (vd. lệch mùa) **không làm mất xu/bạc**, và không phải sửa Rules kinh tế.
Đánh đổi: 2 request thay vì 1, và không ràng buộc "điểm khớp thưởng" (cả hai đều client tự khai nên ràng buộc
đó cũng không chặn được gian lận thật).

### 4.3 Index

| Collection | Field | Dùng cho |
|---|---|---|
| `entries` (scope collection) | `points DESC, wins DESC, __name__ ASC` | Trang bảng xếp hạng |
| `entries` | `points` (single-field, tự có) | Đếm số người hơn điểm mình (tìm STT của mình) |

Composite index scope "collection" áp dụng cho mọi `seasons/*/entries` — tạo 1 lần. Thêm file
`Firebase/firestore.indexes.json` để ghi lại (dán tay trên Console như rules) `[Chưa kiểm chứng]`.

### 4.4 Security Rules (thêm vào exporter, không sửa tay)

```text
usernames/{name}
  read:   signed-in
  create: request.auth.uid == data.uid
          && name.matches('^user[0-9]{6}$')
          && getAfter(users/{uid}).data.username == name
  update, delete: false

users/{uid}  (update thêm nhánh isUsernameClaim)
  affectedKeys ⊆ {username, updatedAt}
  && !('username' in resource.data)                  ← chỉ gán 1 lần
  && existsAfter(usernames/{username}) && getAfter(...).uid == uid
  (create cũng cho phép key 'username' cho tài khoản mới)

seasons/{s}/entries/{uid}
  read:   signed-in
  create/update: isOwner(uid)
          && s == string(currentSeason())             ← currentSeason() tính từ request.time
          && username == get(users/{uid}).data.username
          && !exists(results/{lastMatchId}) && existsAfter(results/{lastMatchId})
          && delta = pointsFor(getAfter(results/{lastMatchId}).data.result)
          && points == (o.points + delta < 0 ? 0 : o.points + delta)   ← viết bằng 2 nhánh OR
          && wins/losses/draws/matches tăng đúng 1 ô
          && (tuỳ D5) request.time > o.updatedAt + 60s
seasons/{s}/entries/{uid}/results/{matchId}
  create: isOwner(uid) && result ∈ {win, loss, draw, surrender}
          && delta == pointsFor(result)
          && getAfter(entries/{uid}).data.lastMatchId == matchId
  update, delete: false
```

`points` ghi giá trị tuyệt đối (không dùng `increment`) vì cần chặn sàn 0. Không bị lost update vì Rules so với
`resource.data` tại thời điểm commit: hai commit đua nhau thì commit sau thấy điểm đã đổi ⇒ bị từ chối ⇒ client đọc
lại và thử lại `[Chưa kiểm chứng: Rules được đánh giá trong cùng giao dịch với ghi — tài liệu Firebase nói vậy]`.

---

## 5. Username `userXXXXXX`

### 5.1 Cơ chế

```
Đăng ký / đăng nhập Google lần đầu (cả hai đi qua PlayerProfileService.EnsureLoadedAsync → LoadOrCreateProfileAsync)
  → sinh name = "user" + 6 chữ số ngẫu nhiên (000000–999999, RNG mã hoá)   — [vì sao ngẫu nhiên, xem dưới]
  → cùng batch tạo profile: create usernames/{name} + users/{uid}.username
       name đã có người dùng → ALREADY_EXISTS → cả batch hỏng → sinh tên khác, thử lại (tối đa 5 lần)
  → Profile có Username
User cũ (users/{uid} đã tồn tại nhưng thiếu username)
  → cùng điểm vào LoadOrCreateProfileAsync: phát hiện thiếu → batch "claim" (create usernames + update users)
```

### 5.2 Đánh đổi cách sinh tên

| Cách | Ưu | Nhược |
|---|---|---|
| **Ngẫu nhiên 6 số + thử lại** (khuyến nghị) | Không có điểm nóng; không lộ số lượng user | Có va chạm: với 1.000 user, xác suất trùng mỗi lần ≈ 0,1% → thử lại là đủ; với >100k user cần thêm chữ số |
| Bộ đếm tăng dần `counters/username` | Không bao giờ trùng | Mọi đăng ký ghi cùng 1 doc (tranh chấp); lộ tổng số user; Rules kiểm "đúng counter+1" phức tạp |
| Lấy từ uid (vd. 6 ký tự đầu) | Không cần ghi khoá | Không đúng format số; vẫn có thể trùng |

### 5.3 Backfill user cũ — "lười" (khuyến nghị) vs script

- **Lười (khi đăng nhập):** không cần quyền admin, dùng đúng Rules. User không bao giờ đăng nhập lại thì không có
  username — nhưng họ cũng không bao giờ xuất hiện trên leaderboard nên không sao.
- **Script admin (Node + service account):** gán cho tất cả ngay, nhưng cần quản lý secret và chạy ngoài Unity.
  Chỉ cần nếu bạn muốn trang quản trị liệt kê đủ username.

---

## 6. Ai ghi kết quả trận — ❓ QUYẾT ĐỊNH D4, D5

### 6.1 Trận nào được tính điểm? (D4)

| Đường vào | Topology | Tính điểm? (khuyến nghị) | Lý do |
|---|---|---|---|
| Find Random | Dedicated (Edgegap) | **Có** | Đối thủ do matchmaker chọn |
| Host/Client | Listen (Session/Relay) | **Không** | 2 tài khoản của cùng 1 người tự host – tự thua ⇒ farm điểm vô hạn |

Cơ chế: server điền `snapshot.isRanked` trong `MatchSnapshotDto` (dedicated server: `IsServer && !IsHost`).
Client chỉ gọi ghi điểm khi `isRanked`. Xu/bạc vẫn nhận ở cả hai chế độ như hiện tại.
⚠ Client sửa đổi vẫn có thể tự đặt `isRanked = true` — chỉ server-side write (§6.3) mới chặn triệt để.

### 6.2 Luồng ghi (giai đoạn 1 — client tự ghi, Rules kiểm)

```
MatchResultPresenter.HandleSnapshotChanged (outcome ≠ Running)
  → ClaimReward(...)                          (giữ nguyên — xu/bạc)
  → nếu snapshot.isRanked:
      RankPointsPolicy.Compute(outcome, viewer, surrenderedSeat) → (result, delta)   [Domain thuần, có test]
      PlayerProfileService/LeaderboardService.RecordRankedResultAsync(matchId, result)
        → ILeaderboardGateway (FirestoreLeaderboardGateway)
           → seasonId = SeasonClock.SeasonAt(ServerClock.UtcNow)
           → GET seasons/{s}/entries/{uid}   (chưa có ⇒ tạo mới từ 0)
           → GET results/{matchId}           (đã có ⇒ AlreadyApplied, không ghi)
           → :commit [ entry (create|update), results/{matchId} (create) ]
           → PERMISSION_DENIED & sát ranh giới mùa ⇒ thử lại 1 lần với s+1
  → UI kết quả: "+30 điểm xếp hạng · Vàng → Bạch kim"
```

### 6.3 Mức chống gian lận (D5)

| Mức | Làm gì | Chặn được | Không chặn |
|---|---|---|---|
| 0 | Chỉ Rules kiểm format/điểm đúng bảng | Ghi điểm tuỳ ý (vd. +9999) | Bịa "win" với matchId giả, liên tục |
| **1 (khuyến nghị bây giờ)** | Mức 0 + giới hạn tốc độ: ≥ 60s giữa hai kết quả (`request.time` vs `updatedAt`) | Bot spam hàng nghìn win/phút | Bịa ~1 win/phút (≈ 1.440/ngày) |
| 2 | Dedicated server ghi kết quả bằng service account (hoặc Cloud Code) — Rules đổi entries `create/update: false` cho client | Gần như mọi bịa kết quả | Đòi secret trên server Edgegap, Admin REST, thêm thời gian |

Mức 2 là "live server" mà bạn cảm nhận được là cần — nhưng lý do là **niềm tin vào điểm**, không phải reset.
Thiết kế `ILeaderboardGateway` giữ cho việc chuyển sang mức 2 không đụng UI (giống `IEconomyGateway`).

---

## 7. Tiền đề phân trận theo rank

### 7.1 Cơ chế dự kiến (UGS Matchmaker) `[Chưa kiểm chứng: cấu hình queue/pool trên Dashboard]`

```
FindMatch → DedicatedMatchmakerClient.StartSearch
  → tier = RankPolicy.TierFor(myPointsInCurrentSeason)        (đọc entry của mình, chưa có ⇒ Đồng)
  → bracket = RankBracketPolicy.BracketFor(tier)
  → CreateTicketOptions(queueName, attributes: { "rank_bracket": bracket })
     + Player customData thêm { "rank_tier": tier, "season": seasonId }
  → Queue có N pool, mỗi pool filter `rank_bracket == k` ⇒ ticket chỉ ghép trong pool của mình
Host/Client: không đi qua matchmaker ⇒ không lọc (đúng yêu cầu)
```

### 7.2 Cách chia nhóm — ❓ QUYẾT ĐỊNH D6

| Cách | Ví dụ | Ưu | Nhược |
|---|---|---|---|
| **Bracket cứng bằng pool** (khuyến nghị) | B0 = Đồng+Bạc, B1 = Vàng+Bạch kim, B2 = Kim cương+Cao thủ+Thách đấu | Đúng "rank thấp không gặp rank cao"; cấu hình Dashboard đơn giản | Ít người chơi ⇒ pool vắng ⇒ chờ mãi (với KLTN vài người test, nguy cơ thật) |
| Chênh lệch ±1 tier (rule trên customData) | Vàng gặp Bạc/Vàng/Bạch kim | Mượt hơn, ít chờ | Bạc gặp Vàng, Vàng gặp Bạch kim ⇒ Bạc và Bạch kim "gián tiếp" cùng hàng chờ; rule syntax cần thử `[Chưa kiểm chứng]` |

Ghi chú: dù chọn gì, `rank_bracket` là **client tự khai** — smurf có thể khai thấp. Mức 2 (§6.3) hoặc Cloud Code
kiểm tier trước khi tạo ticket mới chặn được. Kế hoạch này chỉ làm phần client + tài liệu cấu hình Dashboard.

---

## 8. Thiết kế code (theo ranh giới hiện có)

```text
Game/Domain/Ranking/                      (asmdef Domain, thuần C#, có test)
  RankTier.cs            enum Bronze, Silver, Gold, Platinum, Diamond, Master, Challenger
  RankPolicy.cs          ngưỡng §3.2, TierFor(points), NextThreshold(points)
  RankPointsPolicy.cs    bảng §3.1, Compute(outcome, viewer, surrenderedSeat) → RankedResult(kind, delta)
                         ApplyDelta(points, delta) → max(0, ...)
  SeasonClock.cs         SeasonAnchor(seasonId, startUtcMs, durationMs); SeasonAt(utcMs); StartOf/EndOf(id)
  RankBracketPolicy.cs   tier → bracket (D6)
  SeasonStanding.cs      record: username, points, wins, losses, draws, matches; WinRate
Game/Content/Catalogs/
  RankCatalog.cs (SO)    RankTier → (tên hiển thị, Sprite)  ← gán sprite THEO THAM CHIẾU, không theo tên/index
  SeasonConfig.cs (SO)   durationHours (Inspector, mặc định 24), anchorSeason, anchorStartUtc (ISO)
Infrastructure/Firestore/
  FirestoreRestClient    + RunQueryAsync(structuredQuery), RunCountAsync (runAggregationQuery), ServerTimeOffset (header Date)
Infrastructure/Ranking/                   (song song Infrastructure/Economy)
  ILeaderboardGateway    GetPageAsync(season, cursor, size), GetMyStandingAsync(season), CountAheadAsync(...),
                         RecordRankedResultAsync(matchId, result)
  FirestoreLeaderboardGateway
  LeaderboardService     MonoBehaviour bootstrap giống PlayerProfileService (singleton — theo nợ #4, không nhân rộng thêm)
Infrastructure/Economy/
  FirestoreEconomyGateway: + username khi tạo profile + claim cho user cũ; PlayerProfile.Username
Infrastructure/SceneFlow/
  SceneNames.Leaderboard = "LeaderboardScene"; MainMenuController.OpenLeaderboard()
UI/Leaderboard/
  LeaderboardController, LeaderboardRowView, SeasonListView, PinnedSelfRow
Editor/KLTN/
  LeaderboardSceneBuilder   (Tools/KLTN/Build Leaderboard Scene) — theo quy ước Shop: scene sinh mã
  Exporter Rules: thêm bảng điểm, ngưỡng mùa, khối usernames/seasons
Game/Networking/Contracts/NetworkDtos.cs
  MatchSnapshotDto.isRanked
```

Rank sprites `[Đã kiểm chứng trong asset]`: 2 sheet `Bronze to Plate.png` (4 slice `_0.._3`: Đồng, Bạc, Vàng,
Bạch kim) và `Diamond to Challenger.png` (3 slice `_0, _2, _3` — **thiếu `_1`**: Kim cương, Cao thủ, Thách đấu).
Vì tên slice không liên tục, `RankCatalog` phải gán sprite trực tiếp; installer gán theo thứ tự slice, có kiểm tra
đủ 7 sprite.

MainMenu: UI hiện tại do nhóm dựng tay (commit `38a5661`, `652e653`) ⇒ **thêm nút Leaderboard bằng tay** vào
MainMenu (gọi `MainMenuController.OpenLeaderboard`), còn `LeaderboardScene` thì sinh bằng builder. Cần xác nhận
`ShopSceneBuilder` không ghi đè MainMenu khi chạy lại.

---

## 9. Màn hình Leaderboard

### 9.1 Bố cục

```
┌──────────────┬──────────────────────────────────────────────────────────┐
│ [← Back]     │              BẢNG XẾP HẠNG — MÙA 12                       │
│              │              Kết thúc sau 05:42:17                        │
│ MÙA          ├──────┬────────────────┬──────────────────┬───────┬───────┤
│ ▶ Mùa 12 ●   │ STT  │ Username       │ Rank             │ Điểm  │ Thắng │
│   Mùa 11     ├──────┼────────────────┼──────────────────┼───────┼───────┤
│   Mùa 10     │ 🥇 1 │ user482913     │ [icon] Thách đấu │  830  │ 71.4% │ ← nền vàng kim
│   ...        │ 🥈 2 │ user100274     │ [icon] Cao thủ   │  640  │ 66.7% │ ← nền bạc
│ (cuộn được)  │ 🥉 3 │ user559031     │ [icon] Kim cương │  450  │ 60.0% │ ← nền đồng
│              │   4  │ ...            │                  │       │       │
│              │  ... (cuộn, tải thêm từng trang 50)                      │
│              ├──────┴────────────────┴──────────────────┴───────┴───────┤
│              │ 147  │ user031577 (Bạn)│ [icon] Bạc      │   90  │ 52.0% │ ← DÍNH ĐÁY, vàng nhạt
└──────────────┴──────────────────────────────────────────────────────────┘
```

- Tỉ lệ thắng = `wins / matches` (hòa tính vào mẫu số), `matches = 0` ⇒ "—".
- Thứ tự: `points DESC, wins DESC, docId ASC` (docId để thứ tự ổn định khi hoà điểm).
- Hàng của mình trong top 3: giữ huy chương + viền top 3, **nền vàng nhạt** và hậu tố "(Bạn)" để vẫn nhận ra.
- Mùa cũ: không có đồng hồ đếm ngược, hiển thị "Đã kết thúc (dd/MM HH:mm)".
- Menu trái: liệt kê từ mùa hiện tại lùi về mùa 1 (tính từ `SeasonClock`, không cần query); mùa 24h ⇒ danh sách dài
  ⇒ ScrollRect, mặc định chọn mùa hiện tại.

### 9.2 Cơ chế "hàng của mình dính đáy rồi nhảy vào bảng"

```
Mở mùa s
  → GetPageAsync(s, null, 50)                 trang đầu
  → GetMyStandingAsync(s)                      entry của mình (chưa có ⇒ không hiện hàng ghim)
  → myIndex = CountAhead(points > mine) + CountAhead(points == mine && wins > myWins)   (2 aggregation count)
       ⚠ bỏ qua tie-break theo docId ⇒ STT có thể lệch 1 khi trùng cả điểm lẫn số thắng [chấp nhận]
  → Mỗi lần ScrollRect.onValueChanged:
       rowTop    = myIndex * rowHeight                         (hàng cao CỐ ĐỊNH ⇒ O(1), không cần layout)
       viewBottom = content.anchoredPosition.y + viewport.height
       if myIndex chưa được tải  || rowTop + rowHeight > viewBottom → hiện PinnedSelfRow (overlay ở đáy viewport)
       else                                                       → ẩn PinnedSelfRow (hàng thật trong bảng hiện ra)
  → Cuộn gần cuối nội dung đã tải → GetPageAsync(s, cursor=lastDoc, 50) (startAfter) → nối hàng
```

- `PinnedSelfRow` nằm **ngoài** content của ScrollRect (con của viewport frame) ⇒ không cuộn theo.
- Hàng đã cuộn qua phía trên: không ghim lên đỉnh (đúng mô tả "chỉ ghim ở dưới cùng"). Nếu bạn muốn ghim cả
  đỉnh thì là thay đổi 1 điều kiện.
- Rủi ro: nếu sau này hàng có chiều cao thay đổi (tên dài xuống dòng) thì công thức `index × rowHeight` sai ⇒
  giữ row height cố định, cắt chữ bằng ellipsis.
- Chi phí: mở bảng = 50 document reads + 2 aggregation (count tính ~1 read / 1.000 entries [Mô hình tổng quát])
  ⇒ phù hợp gói Spark.

---

## 10. Lộ trình thực hiện (mỗi bước tự kiểm chứng được)

| Bước | Nội dung | Kiểm chứng |
|---|---|---|
| 1 | Domain `Ranking/` + test EditMode (ngưỡng biên 59/60, sàn 0, SeasonClock ranh giới, re-anchor) | Test pass, Domain vẫn `noEngineReferences` |
| 2 | Username: sinh + claim + backfill lười, Rules `usernames` | Tài khoản mới có `userXXXXXX`; user cũ đăng nhập → có field; tạo trùng tay trên Console → client tự đổi tên |
| 3 | `FirestoreRestClient`: RunQuery, Count, server time offset | Log offset; query thử trên mùa giả |
| 4 | `ILeaderboardGateway` + Rules `seasons/**` + index | Chơi 1 trận Find Random → 1 entry + 1 result; claim lại cùng matchId → không đổi điểm; sửa giờ máy → vẫn đúng mùa |
| 5 | `isRanked` trong snapshot + ghi điểm sau trận + hiện điểm ở màn hình kết quả | Host/Client: có xu, không có điểm; Find Random: có cả hai |
| 6 | `SeasonConfig`, `RankCatalog`, `LeaderboardSceneBuilder`, nút MainMenu, Back | Mở/đóng scene; đổi `durationHours` = 0.05 (3 phút) để xem chuyển mùa thật |
| 7 | Hàng ghim + phân trang + menu mùa cũ | Tạo ~120 entry giả bằng tài khoản test/Console; cuộn tới hàng mình → hàng ghim biến mất |
| 8 | Tiền đề matchmaking: attribute `rank_bracket`, customData, tài liệu cấu hình pool | Log ticket chứa bracket; (cấu hình Dashboard: làm khi bạn sẵn sàng) |
| 9 | Cập nhật `AGENTS.md`, `memory.md`, skill mới `adjust-rank-season` (đổi điểm/ngưỡng/độ dài mùa → Export Rules) | Review |

Rule 0 warning/0 error và tuân thủ Unity 6000.5.4f1 (`rules/unity-api-compliance.md`) áp dụng cho mọi bước.

---

## 11. Các quyết định cần bạn duyệt

| # | Câu hỏi | Khuyến nghị |
|---|---|---|
| **D1** | Reset mùa dùng phương án nào? A (lười, không server) / B (Cloud Functions) / C (UGS Cloud Code) | **A** |
| **D2** | Bảng điểm +30 / −20 / hòa 0 / đầu hàng −20 và ngưỡng §3.2 | **Duyệt như trên** |
| **D3** | Thách đấu: ngưỡng cố định 800 / top 10 | **Cố định** |
| **D4** | Trận Host/Client có tính điểm? | **Không** |
| **D5** | Mức chống gian lận: 0 / 1 (giới hạn 60s) / 2 (server ghi) | **1** bây giờ, 2 khi làm giai đoạn 2 kinh tế |
| **D6** | Chia nhóm ghép trận: 3 bracket cứng / ±1 tier | **3 bracket** |
| D7 | Mùa đầu tiên bắt đầu khi nào (anchor)? | 00:00 giờ VN (17:00 UTC) ngày triển khai |
| D8 | Reset về 0 hoàn toàn mỗi mùa (mặc định) hay giữ một phần điểm? | **Về 0** (mùa 24h giữ một phần không có ý nghĩa) |

---

## 12. Bản đồ kỹ năng (cho người học)

```yaml
khái_niệm: phân vùng dữ liệu theo thời gian (season partition)
liên_kết:
  - "[[Log rotation]] ↔ [[Season partition]]: reset = đổi phân vùng, không phải xoá"
  - "[[Trusted clock]] ↔ [[Security Rules request.time]]: đồng hồ nào được tin"
  - "[[Denormalization]] ↔ [[Firestore không có join]]: chép username vì bất biến"
  - "[[Idempotency key]] ↔ [[results/{matchId}]]: ghi lại cùng trận không cộng 2 lần"
  - "[[Client-authoritative write]] ↔ [[Leaderboard gian lận]]: vì sao 'live server' cần cho điểm, không cho reset"
câu_hỏi_tự_kiểm:
  - "Nếu 2 thiết bị của cùng 1 tài khoản kết thúc 2 trận cùng giây, điều gì xảy ra với points? Vì sao không mất điểm?"
  - "Vì sao rank không được lưu trong DB?"
  - "Đổi durationHours giữa mùa mà không re-anchor thì số mùa thay đổi thế nào?"
```
