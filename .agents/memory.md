# memory.md — Kỹ thuật đã áp dụng trong KLTN

> Mục đích: bộ nhớ dài hạn cho agent/người mới — **đã chọn gì, vì sao, giới hạn ở đâu**.
> Khác với `rules/` (quy tắc phải làm theo), file này ghi **quyết định và hiện trạng**.
> Cập nhật khi một quyết định kiến trúc thay đổi. Lần cuối: 05/10/2026 (Shop + kinh tế + Firestore).
> Nhãn: `[Đã kiểm chứng trong code]`, `[Chưa kiểm chứng]` (cần xác minh ngoài repo, VD dashboard UGS/Edgegap).

---

## 1. Ba cấp độ — đừng trộn

| Cấp | Câu hỏi | KLTN |
|---|---|---|
| Architectural style | Hình dạng/topology toàn hệ thống | Modular monolith; layered organization; client–server server-authoritative |
| Architectural pattern | Phân vai subsystem | Clean Architecture boundary (một phần), Ports-and-Adapters, MVP-style Presentation, Authoritative Command + Filtered Projection, Typed Domain Events |
| Design pattern | Cộng tác class cục bộ | Simple Factory, Builder-style Assembler, Command-style Intent, Explicit FSM, Observer, Strategy, Behavior Composition, Adapter, Data Mapper, Catalog, Null Object, Facade, Snapshot capture |

## 2. Architectural styles

- **Modular Monolith** — một app Unity, module chia theo `Game/{Domain,Content,Networking,Presentation}` + `Infrastructure`.
  Ranh giới thật duy nhất bằng asmdef: `KLTN.Game.Domain` (`noEngineReferences`). Lý do: game 1v1, state 1 process, deterministic.
- **Layered organization** — Domain trong cùng; Content/Networking; Presentation/Infrastructure/UI ngoài cùng. Không strict layering.
- **Client–Server, Server Authoritative** — server giữ `MatchState`, client gửi intent. Hai topology: listen (Session/Relay) và dedicated (Edgegap).

## 3. Architectural patterns

- **Clean Architecture boundary (một phần)** — dependency hướng vào Domain; chưa có Application layer, orchestration ở `NetworkMatchBridge`.
- **Ports-and-Adapters** — port `IAuthService`, `IRandomSource`, `IEconomyGateway`; adapter Firebase REST, Firestore REST, NGO RPC, UGS Matchmaker, Session/Relay, mapper content.
- **MVP-style Presentation** — model = `MatchClientProjection`/`MatchUpdateInbox`; presenter = `Match*Presenter`, `AbilityTargetSelectionController`; view = card visual, drop zone, animator.
- **Authoritative Command + Filtered Projection** — command side mutate `MatchState`; `MatchSnapshotBuilder.Build(viewer)` dựng read model riêng mỗi seat. Không phải CQRS đầy đủ.
- **Typed Domain Events** — `GameEvent`/`GameEventBatch` (`GameEvents.cs`), `AbilityResolutionEvent` (có `Sequence`) để trigger resolver và chuyển cho animation. Không có global event bus.

## 4. Design patterns (implementation thật)

| Pattern | Code | Ghi chú giới hạn |
|---|---|---|
| Simple Factory | `MatchFactory.Create` | Không phải Factory Method/Abstract Factory (không có product family/subclass) |
| Builder-style Assembler | `WeightedDeckBuilder.Build`, `DefaultDeckBuilder.Build` (fallback/test), `MatchSnapshotBuilder.Build` | One-shot, không fluent/Director |
| Command-style Intent | `Request...` → `Submit...Rpc` → `MatchRulesEngine.Try...` → `CommandResult` | Chưa có `GameCommand` object/dispatcher; Mulligan còn bypass |
| Explicit FSM | `MatchPhase`: Mulligan, RoundStart, Priority, AbilitySelection, BlockDeclaration, CombatResolution, RoundEnd, Finished | Enum + guard, không phải GoF State |
| Observer | C# event trong projection/inbox; presenter subscribe | Static registry làm lifecycle khó test |
| Strategy | `IRandomSource` / `SeededRandomSource` | — |
| Behavior Composition | `AbilityDefinition` = trigger + condition + target requirement + ordered effects | Không phải Composite GoF |
| Gateway/Adapter | `FirestoreEconomyGateway : IEconomyGateway` | Giai đoạn 2 thay bằng gateway Cloud Code, UI không đổi |
| Adapter | `FirebaseRestAuthService : IAuthService` | Hiện chỉ có adapter REST (Firebase SDK DLL vẫn nằm trong `Assets/FirebaseRef`) |
| Data Mapper | `CardDefinitionMapper`, `RoundResolutionDtoMapper`, `AbilityResolutionDtoMapper` | DTO mapper lọc thông tin ẩn |
| Catalog | `CardAssetCatalog`, `CardPresentationCatalog`, `ShopCatalog` | Không phải Repository |
| Policy (pure function) | `MatchRewardPolicy`, `PurchaseRules` | Domain thuần, có test; Rules Firestore là bản kiểm tra phía server |
| Null Object | `UnitPassiveRules.None` | — |
| Facade/Gateway | API public `NetworkMatchBridge` | Đang ôm nhiều trách nhiệm |
| Snapshot capture | `AbilityCardState.Capture` | Chụp state trước khi card đổi zone để animation đúng |
| Singleton (nợ) | `AuthManager`, `GoogleAuthService`, `AppFlowManager`, `UgsSessionService`, `Match*Registry`, `PlayerProfileService` | Không nhân rộng |

Kỹ thuật không phải pattern nhưng quan trọng: `partial class` theo use case; FIFO ability queue + giới hạn số lần resolve;
`#region`/XML summary chuẩn hóa; di chuyển script kèm `.meta` để giữ GUID.

## 5. SOLID — hiện trạng

| Nguyên tắc | Mức | Bằng chứng / nợ |
|---|---|---|
| SRP | Tốt một phần | Domain service tách rõ; `NetworkMatchBridge` còn nhiều vai; `PlayerState` zone public mutable |
| OCP | Tốt ở cấp card, một phần ở cấp effect | Card mới = data; effect mới phải sửa `EffectKind` + executor + validator |
| LSP | Ít rủi ro | Sealed + composition; contract `IAuthService` cần contract test |
| ISP | Đạt | `IAuthService`, `IRandomSource` nhỏ |
| DIP | Mạnh ở Domain, yếu ngoài Domain | asmdef `noEngineReferences`; static registry/singleton; Mulligan `new System.Random()` |

## 6. Hosting: Edgegap (cloud dedicated server)

`[Đã kiểm chứng trong code]`
- Package `com.edgegap.unity-servers-plugin` (Git) trong `Packages/manifest.json` — dùng để build Linux server + đóng container + push lên Edgegap.
- Module SDK: `com.unity.dedicated-server`, `com.unity.sdk.linux-x86_64`, toolchain `win-x86_64-linux`.
- `DedicatedServerBootstrap` (scene `ServerBootstrap`): chạy khi `UNITY_SERVER` hoặc graphics device `Null`;
  listen `0.0.0.0:7777`; đọc env `MATCH_ID`; đủ **2** client → load `GameScene`; client thừa/đến muộn bị disconnect;
  không có ai trong **120s** → `Application.Quit`.
- `Assets/CloudCode/Matchmaker.ccmr` tham chiếu Cloud Code module **`EdgegapAllocator`**
  (từ repo Unity `matchmaker-hosting-providers`) — UGS Matchmaker gọi module này để cấp phát deployment Edgegap.
- `DedicatedMatchmakerClient` gửi `player_ip` (lấy qua `PublicIpResolver`) trong custom data của ticket
  — Edgegap dùng IP người chơi để chọn vị trí server gần nhất `[Chưa kiểm chứng: logic chọn vùng nằm trong module, ngoài repo]`.
- Client nhận `IpPortAssignment` → `UnityTransport.SetConnectionData(ip, port)` → `StartClient`.

`[Chưa kiểm chứng]` cấu hình queue/pool Matchmaker, secret Edgegap API token trong Cloud Code, app version/port mapping trên dashboard Edgegap.

Rủi ro đã biết:
- `modulePath` trong `.ccmr` là đường dẫn tuyệt đối tới `Downloads` máy cá nhân → máy khác không deploy được module.
- Listen path (Session/Relay) vẫn song song → 2 bộ lifecycle/disconnect cần test riêng.

## 7. Auth chain: Firebase → UGS → UGS token → Edgegap

```text
[1] Firebase Auth (REST, FirebaseRestAuthService : IAuthService)
    ├─ Email/password: signUp / signInWithPassword
    ├─ Google: GoogleAuthService — OAuth 2.0 Authorization Code + PKCE (S256),
    │          loopback redirect http://127.0.0.1:<port>/ (HttpListener) → Google id_token
    │          → Firebase signInWithIdp(providerId=google.com)
    └─ Auto-login: refresh token (PlayerPrefs) → securetoken refresh
    ⇒ Firebase ID token (JWT)

[2] UGS Authentication — AuthManager.LoginFirebaseToUGS
    UGSInitializer.Initialize() → AuthenticationService.SignInWithOpenIdConnectAsync("oidc-firebase", firebaseIdToken)
    ⇒ UGS PlayerId + UGS access token (SDK giữ, tự gắn vào mọi call UGS)

[3] UGS Matchmaker — DedicatedMatchmakerClient
    CreateTicketAsync(Player(PlayerId, {player_ip}), queue)  ← xác thực bằng UGS access token
    → poll GetTicketAsync tới IpPortAssignment (Found/Failed/Timeout)

[4] Cloud Code EdgegapAllocator → Edgegap API → deployment server Linux (container)
    ⇒ IP:port trả về qua assignment

[5] Client → UTP connect thẳng tới Edgegap server (DedicatedServerBootstrap)
```

Logout: xóa PlayerPrefs (login method, refresh token) → `IAuthService.Logout` → `AuthenticationService.SignOut` → `GoogleAuthService.ClearSession`.

Điểm cần hiểu đúng (giả định ẩn):
- **UGS token không đi tới Edgegap server.** Token chỉ xác thực bước [3] với UGS. Server trên Edgegap hiện **không có
  ConnectionApproval** và không kiểm tra danh tính → ai biết IP:port trong khoảng chờ đều có thể chiếm slot.
  Hướng sửa: gửi UGS access token/PlayerId trong `ConnectionData`, server bật ConnectionApproval và so với danh sách
  player của match (lấy từ allocation payload/env Edgegap) `[Chưa kiểm chứng: cơ chế payload của module]`.
- Firebase project phải được khai báo làm OIDC provider `oidc-firebase` trong UGS Dashboard (issuer
  `https://securetoken.google.com/<project-id>`) `[Chưa kiểm chứng: cấu hình dashboard]`.
- `google-auth-config.json` (chứa `clientSecret`) bị gitignore; `firebase-auth-config.json` chỉ chứa Web API key
  (công khai theo thiết kế Firebase, an toàn nhờ Security Rules/authorized domains, không phải secret).

## 8. Thay đổi so với báo cáo Phase 3 (14/09 → 04/10/2026)

- Script: 98 → **110** file, ~19.5k → **~23.5k** dòng. Test attribute: 97 → **103** (14 file).
- Mới: `MatchRulesEngine.Surrender` (`TrySurrender` đi đúng command pipeline), `SurrenderUI`, `MatchResultPresenter`
  (màn hình thắng/thua), `GraveyardUI`, `GameSettingsUI` + audio.
- Mới: `AbilityResolution` (semantic event + `AbilityCardState`), `AbilityResolutionDtoMapper`,
  `AbilityEffectExecutor.Resolution`, `CombatAnimationDirector.Abilities`, `CardReviveFeedback`,
  `AbilityTargetSelectionController`, `AbilityTargetLinkGraphic` (+ editor installer).
- Mới: `Infrastructure/SceneFlow` (`SceneNames`, `MainMenuController`), `Infrastructure/Networking/Common/PersistentNetworkRoot`,
  `PublicIpResolver`; UI auth tách `UI/Authentication`, `UI/Common/LoadingSpinner`.
- Login UX một bước (commit `b04b476`).
- Còn nguyên nợ: Mulligan bypass, zone mutable, chưa Application layer, static registry, Content→Presentation.
- Scene thêm `GameScene Test Skills Animation.unity` (bản duplicate khi giải conflict).

## 9. Phiên bản Unity và chính sách API

- Unity 6.5 — `6000.5.4f1`. Quyết định: **mọi code phải tuân thủ docs Unity của đúng phiên bản này**.
- Lý do: 6.5 chuyển từ `InstanceID` (int 32-bit) sang `EntityId` (64-bit) và bỏ mọi API dựa vào thứ tự ID.
  API `InstanceID` obsolete gây **lỗi compile**; `FindFirstObjectByType`, `FindObjectOfType`, `FindObjectsSortMode` bị deprecate.
- Thay thế đã chốt: `FindFirstObjectByType`/`FindObjectOfType` → `FindAnyObjectByType`; `FindObjectsByType(..., SortMode)` →
  `FindObjectsByType<T>()`/`(FindObjectsInactive)`; `GetInstanceID` → `GetEntityId`. Chi tiết: `rules/unity-api-compliance.md`.
- Vi phạm còn lại: `UI/SurrenderUI.cs:24` (`FindFirstObjectByType`) — compiler đã báo CS0618.

## 10. Case/bug đang theo dõi

- Âm Binh bị kill trong Reserve bởi skill của card mới summon từ DrawHand: logic đã đúng, **thiếu animation hồi sinh**
  (kiểm tra lại với `CardReviveFeedback` mới).

## 11. Shop và kinh tế xu/bạc (05/10/2026)

`[Đã kiểm chứng trong code]` — Domain + 24 test liên quan pass (compile ngoài Unity bằng .NET 8). Rules: `[Chưa kiểm chứng trên emulator]`.

**Luật kinh tế**
- Tài khoản mới: 0 xu, 0 bạc, 8 thẻ starter. 11 thẻ còn lại bán trong Shop (8 thẻ xu, 3 thẻ bạc).
- Thưởng: thắng +75 xu +1 bạc; thua/hòa +25 xu; tự đầu hàng trước vòng 3 → 0 (chống farm). Đối thủ đầu hàng → người thắng vẫn đủ thưởng.
- Rớt mạng giữa trận → trận hủy, không thưởng.

**Sức mạnh thẻ và giá** — `P = I × clamp(√R, 0.8, 1.3)`, `I = Damage + Health + K + A`, `R = I / (2·mana + 1)`
(K: Fearsome +1, Lifesteal +2, CannotBlock −1, Ephemeral −2; A = giá trị skill ước lượng theo deck "đồng minh chết").

| Nhóm | Thẻ (P) | Giá |
|---|---|---|
| Starter (Weak) | MaDa 3.6, MaDoi 5.2, MaGa 6.5, MaLon 6.5, QuyMotDo 6.6, VongNhi 7.0, OngKe 7.5, QuyCau 8.3 | — |
| Xu | MaMatMam 8.3 (Weak) 150, MaLai 8.6 (Weak) 200, ThienLinhCai 9.0 200, AmBinh 10.2 350, MaCo 10.2 350, MaCangSung 12.5 550, MaTroi 12.7 550, ThanTrung 13.0 600 (Mid) | 150–600 xu |
| Bạc (Strong) | LinhMieu 15.0 → 10, MaTranh 20.1 → 12, QuyDaXoa 34.0 → 15 | 10–15 bạc |

Nhận xét đã biết: bạc là nút thắt (37 bạc ≈ 74 trận ở 50% thắng); Quỷ Dạ Xoa lệch cân bằng; starter tối đa 4 mana.

**Deck** — `DeckRules.RequiredCardCount = 25`. `WeightedDeckBuilder`: trọng số Weak 3 / Mid 2 / Strong 1 (`CardTierWeights`),
mỗi thẻ sở hữu ≥ 1 lá, tối đa 4 (Âm Binh 1), chia phần dư bằng largest remainder, deterministic theo tập thẻ; MatchFactory shuffle.

**Firestore (REST, không dùng SDK)**
```text
users/{uid}                        coins, silver, schemaVersion, createdAt, updatedAt, lastPurchase, lastRewardMatchId
users/{uid}/ownedCards/{cardId}    acquiredAt, source: starter|coins|silver
users/{uid}/matchRewards/{matchId} result: win|loss|draw|surrender_early, coins, silver, createdAt
```
- Mọi ghi là một `:commit` batch, tiền đổi bằng transform `increment` (không ghi đè số dư cũ → không lost update).
- Rules nhúng bảng giá + danh sách starter (không cần collection `shopCatalog`); kiểm `getAfter/existsAfter`:
  mua phải trừ đúng giá và thẻ chưa sở hữu; thưởng chỉ (75,1)/(25,0)/(0,0) và mỗi `matchId` một lần; starter chỉ tạo cùng lúc tạo user.
- `AuthManager.GetValidIdTokenAsync()` tự refresh ID token khi còn < 5 phút.

**Đưa thẻ sở hữu vào trận** — một cơ chế cho cả listen-host và dedicated: client gửi `SubmitLoadoutRpc(csv)` lúc bridge spawn;
server lọc ID hợp lệ, thiếu thì dùng starter, chờ tối đa 10s. Networking không phụ thuộc Economy: đọc qua `MatchLoadoutRegistry`.

**Giai đoạn 2 (chưa làm)** — server ghi thưởng qua UGS Cloud Code (giữ service account), Rules đổi `isReward()` → `false`,
server xác minh ownedCards bằng token người chơi. Client không đổi nhờ `IEconomyGateway`.

**Shop UI** — sinh bởi `ShopSceneBuilder` (menu Tools/KLTN/Build Shop Scene + Main Menu Button): lưới thẻ căn trái-trên,
cuộn chỉ bằng con lăn (`ShopScrollRect`: không kéo, clamped, không quán tính), overlay tối khi hover/đã sở hữu,
nút Mua đỏ chữ vàng có `HoverScale` 1.0→1.2, modal `ShopConfirmDialog` "Bạn có đồng ý mua thẻ {tên} không?".
7 chế độ: Bạc ↑/↓, Xu ↑/↓, Chỉ bạc, Chỉ xu, Chỉ thẻ chưa có (`ShopListQuery`).
