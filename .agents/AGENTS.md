# AGENTS.md — KLTN

> Trạng thái: **bản nháp, còn chỉnh sửa**. Cập nhật lần cuối: 16/10/2026 (thêm Leaderboard theo mùa + điểm rank + reset bằng Cloud Code).
> Nguồn: báo cáo Phase 3 (14/09/2026) + đối chiếu source hiện tại (commit `b221f62`).
> Đọc kèm: `.agents/memory.md` (kỹ thuật đã áp dụng) và `.agents/rules/*.md` (quy tắc bắt buộc).

## 1. Dự án là gì

Game thẻ bài 1v1 (Unity 6 — `6000.5.4f1`), chủ đề ma quỷ dân gian Việt Nam, 19 Unit card.
Mô hình **server authoritative**: Domain C# thuần quyết định luật, client chỉ gửi intent
và nhận snapshot đã lọc theo người xem.

Stack chính:

| Thành phần | Công nghệ |
|---|---|
| Netcode | Netcode for GameObjects `2.13.0`, Unity Transport |
| UGS | Authentication (OIDC), Multiplayer Services `2.3.1` (Session/Relay, Matchmaker), Cloud Code |
| Dedicated server | `com.unity.dedicated-server`, **Edgegap** (cloud hosting) qua Edgegap Unity plugin |
| Auth | Firebase (REST) → UGS OpenID Connect |
| Persistence | Cloud Firestore qua **REST** (`(default)` DB, project `unity-kltn`), Security Rules trong `Firebase/firestore.rules` |
| Render/UI | URP 2D, uGUI + TextMeshPro |
| Test | Unity Test Framework, EditMode cho Domain |

## 2. Bản đồ thư mục

```text
Assets/
├── Scripts/
│   ├── Authentication/          # IAuthService, FirebaseRestAuthService, GoogleAuthService, AuthManager
│   ├── Client/                  # UGSInitializer
│   ├── Game/
│   │   ├── Domain/              # asmdef KLTN.Game.Domain (noEngineReferences) — LÕI LUẬT
│   │   │   ├── Abilities/       # definitions, trigger resolver, target validator, effect executor, AbilityResolution
│   │   │   ├── Cards/           # CardDefinition, CardInstance, PlayerState, DeckRules (25 lá), WeightedDeckBuilder, CardContentValidator
│   │   │   ├── Economy/         # Currency, CardTier, ShopEntry, Wallet, PurchaseRules, MatchRewardPolicy
│   │   │   ├── Ranking/         # RankTier/RankPolicy, RankPointsPolicy, SeasonWindow/SeasonSchedule, SeasonStanding, UsernamePolicy
│   │   │   ├── Combat/          # RoundResolution, RoundTransition
│   │   │   └── Match/           # MatchState, MatchRulesEngine.*, MatchFactory, GameEvents, RoundHistory
│   │   ├── Content/             # Authoring (ScriptableObject) / Catalogs (+ ShopCatalog, RankCatalog, RankingSettings) / Mapping (SO → Domain)
│   │   ├── Networking/          # Bridge (NGO RPC, + .Loadout) / Contracts (DTO) / Projection (snapshot, inbox, DTO mapper)
│   │   └── Presentation/        # Cards / Combat (animation, VFX) / Match (presenter, HUD, selection)
│   ├── Infrastructure/
│   │   ├── Networking/Common/   # PersistentNetworkRoot
│   │   ├── Networking/Dedicated/# Matchmaker client, DedicatedServerBootstrap, PublicIpResolver (Edgegap path)
│   │   ├── Networking/Listen/   # ListenSessionController (Session/Relay path)
│   │   ├── SceneFlow/           # SceneNames, MainMenuController
│   │   ├── Firestore/           # FirestoreRestClient, FirestoreValueMapper (REST + typed values)
│   │   ├── Economy/             # IEconomyGateway, FirestoreEconomyGateway, PlayerProfileService (+ username)
│   │   └── Ranking/             # ILeaderboardGateway, FirestoreLeaderboardGateway, LeaderboardService
│   ├── Networking/              # UgsSessionService (singleton)
│   └── UI/                      # AppFlowManager, Login/Register, Surrender, Graveyard, Settings
│       ├── Leaderboard/         # LeaderboardController (mùa, phân trang, dòng ghim), LeaderboardRowView
│       └── Shop/                # ShopController, ShopCardItemView, ShopTopBarView, ShopListQuery, ShopScrollRect, HoverScale, ShopConfirmDialog
├── Tests/EditMode/Domain/       # asmdef KLTN.Game.Domain.Tests (Abilities/Cards/Combat/Match)
├── Editor/KLTN/                 # CardContentInstaller, ShopCatalogInstaller, ShopSceneBuilder, LeaderboardSceneBuilder, AbilityTargetLink*
├── Resources/Cards/             # 19 card asset (.asset)
├── Resources/ShopCatalog.asset  # starter/giá/tier — sinh bởi ShopCatalogInstaller
├── Prefabs/UI/ShopCardItem.prefab # sinh bởi ShopSceneBuilder
├── CloudCode/Matchmaker.ccmr    # tham chiếu module EdgegapAllocator (Cloud Code)
├── CloudCode/SeasonModule.ccmr  # tham chiếu CloudCode/SeasonModule (reset mùa)
├── Resources/RankingSettings.asset, RankCatalog.asset # sinh bởi Tools/KLTN/Install Ranking Assets
├── Sprites/Ranks/               # 2 sheet, 7 icon rank (Đồng → Thách đấu)
├── StreamingAssets/             # firebase/google config (google-auth-config.json bị gitignore)
└── Scenes/                      # LoginScene, MainMenu, HostClientMenu, FindMatch, ServerBootstrap, GameScene, ShopScene, LeaderboardScene
Firebase/firestore.rules         # (ngoài Assets) sinh bởi Tools/KLTN/Export Firestore Rules — dán vào Firebase Console
Firebase/firestore.indexes.json  # index entries (points, wins) — sinh cùng lúc với rules
Firebase/rules.test.mjs          # test rules trên emulator: cd Firebase && npm install && npm test
CloudCode/SeasonModule/          # (ngoài Assets) module C# reset mùa + README cài đặt
```

Dependency hợp lệ (mũi tên = "được phép phụ thuộc vào"):

```text
Presentation → Networking, Content
Networking   → Domain
Content      → Domain
Infrastructure/Auth/UI → Networking (qua facade), UGS/Firebase SDK
Infrastructure/Economy → Domain.Economy, Content (ShopCatalog), Firestore REST; nạp loadout qua MatchLoadoutRegistry
Infrastructure/Ranking → Domain.Ranking, Content (RankingSettings), Firestore REST
CloudCode/SeasonModule → link Domain/Ranking/SeasonWindow.cs (không tham chiếu Unity)
Networking   ↛ Economy (chỉ đọc delegate MatchLoadoutRegistry.LocalOwnedCardIds)
Tests        → Domain
Domain       → (không gì cả ngoài System.*)
```

## 3. Luồng chính cần nắm trước khi sửa code

**Command (một thao tác người chơi):**

```text
Presenter/UI → NetworkMatchBridge.Request...()          (client, CommandId tăng dần)
  → Submit...Rpc                                        (server: map clientId → SeatId, chống trùng/cũ)
  → MatchRulesEngine.Try...(state, actor, ...)          (Domain: validate + mutate atomic)
  → CommandResult (+ GameEvents / RoundResolution / AbilityResolution)
  → Publication: revision++ → MatchSnapshotBuilder.Build(viewer) cho từng seat
  → MatchClientProjection / MatchUpdateInbox (client) → Presenter render + animation
```

**Kinh tế (shop, thưởng, deck theo thẻ sở hữu):**

```text
Login → PlayerProfileService.EnsureLoadedAsync → FirestoreEconomyGateway
  → users/{uid} chưa có? :commit tạo user (0 xu/0 bạc) + 8 ownedCards starter
Vào trận: NetworkMatchBridge.OnNetworkSpawn → SubmitLoadoutRpc(ownedIds CSV)
  → server chờ đủ 2 seat (timeout 10s → starter) → WeightedDeckBuilder (25 lá, trọng số theo tier)
  → MatchFactory.Create(hostDeck, guestDeck) + matchId (Guid)
Hết trận: MatchResultPresenter → MatchRewardPolicy.Compute → ClaimMatchRewardAsync(matchId) (1 lần/trận)
Shop: ShopController → modal xác nhận → PlayerProfileService.PurchaseAsync → :commit (trừ tiền + tạo ownedCards)
```

**Xếp hạng theo mùa:**

```text
Cloud Code AdvanceSeason (Scheduler mỗi giờ) → config/season (lịch) + seasons/{id} (lịch sử)  — mùa tính theo đồng hồ
Vào trận: snapshot.isRanked (dedicated luôn true; listen theo RankingSettings.HostClientPointsEnabled)
          + matchStartedUtcMs (giờ server bắt đầu trận)
Hết trận: MatchResultPresenter → LeaderboardService.RecordRankedMatchAsync
  → RankedEligibilityPolicy (bắt đầu trong [startAt, lockAt), ghi trước endAt; giờ theo ServerClock)
  → RankPointsPolicy (+30/−20/0/−20) → :commit entries/{uid} + results/{matchId} (Rules kiểm lại)
LeaderboardScene: entries orderBy points desc, winRate desc, username asc → trang 50; vị trí của mình = 3 COUNT (điểm cao hơn; bằng điểm + tỉ lệ cao hơn; bằng cả hai + username nhỏ hơn) + 1. Hàng trống 0 điểm cho mọi tài khoản. Trước khi đổi thiết kế dữ liệu: nêu đánh đổi và hỏi user trước.
Matchmaker: ticket customData rank_tier → rule Difference ≤ 1 trên Dashboard
```

**Auth + vào trận dedicated:** Firebase ID token → UGS `SignInWithOpenIdConnectAsync("oidc-firebase")`
→ UGS PlayerId/access token → Matchmaker ticket → Cloud Code `EdgegapAllocator` → Edgegap deployment
→ `IpPortAssignment` → client connect UTP. Chi tiết và lỗ hổng: xem `memory.md` §5–6.

## 4. Quy tắc bắt buộc (tóm tắt — chi tiết trong `rules/`)

1. **Domain thuần**: không `using UnityEngine`, NGO, UGS, Firebase trong `Game/Domain`. Random qua `IRandomSource`.
2. **Server quyết định**: mọi thay đổi `MatchState` phải đi qua `MatchRulesEngine.Try...`. Client không tự sửa state.
3. **Không lộ thông tin ẩn**: DTO gửi viewer khác không chứa danh tính DrawHand, thứ tự Deck.
4. **Animation là hệ quả**: Presentation chỉ tiêu thụ `RoundResolution`/`AbilityResolution` DTO; không quyết định thắng/thua, ai chết.
5. **Ability data-driven**: không `switch` theo tên/ID card trong rules. Card mới = ScriptableObject + `CardContentInstaller`.
6. **Card visual là local object**: không tạo `NetworkObject` cho từng card.
7. **Giữ GUID**: di chuyển `.cs` luôn kèm `.meta`. Không đổi tên type/namespace MonoBehaviour khi chưa có kế hoạch migration scene/prefab.
8. **Region + XML summary**: theo `rules/coding-conventions.md`.
9. **Không thêm pattern vì tên gọi**: chỉ áp dụng pattern khi có vấn đề thật (xem `rules/design-patterns.md`).
10. **Kinh tế đi qua `IEconomyGateway`**: UI/Presentation không gọi Firestore trực tiếp. Mọi ghi tiền/thẻ là một
    `:commit` batch để Security Rules kiểm bằng `getAfter/existsAfter`. Đổi giá/tier/starter → sửa bảng trong
    `ShopCatalogInstaller.cs`, chạy lại Install + Export Rules, dán rules lên Console (xem skill `adjust-shop-economy`).
11. **Shop UI là sinh mã**: `ShopScene`/`ShopCardItem.prefab` do `ShopSceneBuilder` dựng — sửa layout/style trong builder rồi
    chạy lại menu, không sửa tay scene (sẽ bị ghi đè). Tương tự `LeaderboardScene` ← `LeaderboardSceneBuilder`.
13. **Rank là dẫn xuất từ điểm**: không lưu `tier` vào Firestore; đổi điểm/ngưỡng → sửa `RankPointsPolicy`/`RankPolicy`
    (Domain) rồi Export Firestore Rules (rules nhúng bảng điểm) và dán lên Console. Thời gian mùa chỉ do Cloud Code ghi.
12. **Tuân thủ docs Unity 6000.5.4f1**: mọi code sinh ra phải đúng Scripting API của phiên bản đang dùng; không dùng API obsolete
    (VD `FindFirstObjectByType` → `FindAnyObjectByType`, `GetInstanceID` → `GetEntityId`). Xem `rules/unity-api-compliance.md`.

## 5. Build & kiểm chứng

- Unity Test Runner → EditMode → `KLTN.Game.Domain.Tests` (hiện 120 test attribute trong 17 file) phải pass.
- Kinh tế: đăng nhập tài khoản mới → log `[Profile] Loaded ... 8 thẻ`; chơi 1 trận → `matchRewards` đúng 1 doc/người;
  mua 1 thẻ → trừ đúng giá. Rules: `cd Firebase && npm install && npm test` (emulator, có test batch).
- Xếp hạng: thêm 3 file test `Domain/Ranking`; Cloud Code: `dotnet run --project CloudCode/SeasonModule/SeasonModule.Tests`
  với emulator đang chạy. Thủ công: 2 tài khoản đánh 1 trận → log `[Leaderboard]`, `seasons/{id}/entries` +30/−20,
  LeaderboardScene hiện đúng 2 dòng; đánh lại trong < 60s → trận sau vẫn ghi (client chờ hết cooldown).
- Compile 0 warning / 0 error — đặc biệt không có `CS0618/CS0619` (API obsolete của Unity 6.5) (`.editorconfig`: CRLF, 4 space, Allman, max 90 cột).
- Thay đổi networking/presentation: test thủ công host + guest (Multiplayer Play Mode) từ Mulligan tới ≥ 2 round,
  targeted ability (confirm/cancel/retry/no-target), combat có keyword, surrender, disconnect.
- Sau khi move file: kiểm tra Missing Script ở `GameScene`, `MainMenu`, prefab card.

## 6. Nợ kỹ thuật đang mở (đừng vô tình làm tệ hơn)

| # | Vấn đề | Vị trí |
|---|---|---|
| 1 | Mulligan bypass rules engine, dùng `new System.Random()` thay vì `IRandomSource` | `NetworkMatchBridge.ServerCommands.cs` → `PlayerState.PerformMulligan` |
| 2 | Zone là `List<CardInstance>` public mutable | `PlayerState.cs` |
| 3 | Chưa có Application layer; `NetworkMatchBridge` gánh nhiều vai | `Game/Networking/Bridge` |
| 4 | Static registry/singleton | `MatchProjectionRegistry`, `MatchUpdateInboxRegistry`, `UgsSessionService.Instance`, `AuthManager.Instance`, `GoogleAuthService.Instance`, `AppFlowManager.Instance` |
| 5 | `CardPresentationCatalog` (Content) tham chiếu `CardArtworkView` (Presentation) | `Game/Content/Catalogs` |
| 6 | 29 file ngoài `Game/` còn global namespace | `Authentication`, `UI`, `Infrastructure`, ... |
| 7 | Dedicated server không xác thực danh tính client (không ConnectionApproval/UGS token check) | `DedicatedServerBootstrap.cs` |
| 8 | `Matchmaker.ccmr` trỏ đường dẫn tuyệt đối trong `Downloads` máy cá nhân | `Assets/CloudCode` |
| 9 | Scene trùng `GameScene Test Skills Animation.unity` | `Assets/Scenes` |
| 10 | Spell/stack/CounterSpell chưa triển khai (validator chủ động từ chối) | `EffectKind.CounterSpell` |
| 11 | ~~Dùng API obsolete `FindFirstObjectByType` (CS0618)~~ Đã sửa 18/10 → `FindAnyObjectByType` | `UI/SurrenderUI.cs:24` |
| 12 | Kinh tế giai đoạn 1: client tự ghi tiền, Rules chặn sai giá/sai mức thưởng nhưng client sửa đổi vẫn bịa được `matchId` | `FirestoreEconomyGateway`, `firestore.rules` → giai đoạn 2: Cloud Code |
| 13 | Server tin danh sách thẻ client gửi (chỉ lọc ID hợp lệ) | `NetworkMatchBridge.Loadout.cs` |
| 14 | Rớt mạng giữa trận → trận bị hủy, không ai nhận thưởng | `NetworkMatchBridge.Connections.cs` |
| 15 | ~~`firestore.rules` chưa chạy trên emulator~~ → đã có `Firebase/rules.test.mjs` (35 case) | `Firebase/` |
| 16 | Điểm rank do client ghi; Rules chặn sai bảng điểm/sai mùa/<60s nhưng client sửa đổi vẫn khai thắng được | `FirestoreLeaderboardGateway` → giai đoạn 2: server ghi qua Cloud Code |
| 17 | `rank_tier` trong ticket Matchmaker do client tự khai | `DedicatedMatchmakerClient.cs` |
| 18 | Hòa điểm xếp theo wins rồi uid; vị trí của mình dùng COUNT(points > mine) nên có thể lệch vài hạng khi bằng điểm | `FirestoreLeaderboardGateway.GetPositionAsync` |

## 7. Tìm code nhanh

```powershell
rg -n "RequestSurrender|SubmitSurrenderRpc|TrySurrender" Assets/Scripts   # theo 1 command end-to-end
rg -n "Phase\s*=" Assets/Scripts/Game/Domain                              # mọi mutation phase
rg -n "thien_linh_cai_play" Assets                                        # theo 1 ability ID
rg -n "Registry\.Current" Assets/Scripts                                  # nơi dùng static registry
rg -n "SubmitLoadoutRpc|WeightedDeckBuilder|ClaimMatchRewardAsync" Assets/Scripts # luồng kinh tế
rg -n "RecordRankedMatchAsync|isRanked|rank_tier|AdvanceSeason" Assets CloudCode  # luồng xếp hạng
```
