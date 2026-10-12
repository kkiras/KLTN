# Rule — Architectural Patterns

> Pattern kiến trúc trả lời: **các subsystem lớn phân vai và phụ thuộc nhau theo khuôn nào?**

## 1. Clean Architecture boundary (một phần)

```text
Presentation ──► Networking (bridge + projection) ──► Domain (pure C#)
      └────────► Content (catalog, mapper) ───────────┘
Infrastructure (Firebase / UGS / Relay / Matchmaker / Edgegap) ──► Networking
Tests ──► Domain
```

Quy tắc:
- Domain không biết Unity, transport, scene, prefab, Firebase, UGS.
- **Chưa có Application layer**: orchestration use case mạng nằm trong `NetworkMatchBridge`, validation/transition trong
  `MatchRulesEngine`. Khi bắt đầu spell stack hoặc cần dùng chung logic giữa hai topology → tạo asmdef
  `KLTN.Game.Application` (use case + port), Bridge chỉ còn là adapter NGO.
- Không gọi đây là "Clean Architecture đầy đủ" trong tài liệu/báo cáo.

## 2. Ports-and-Adapters (Hexagonal)

| Port | Adapter hiện có |
|---|---|
| `IAuthService` | `FirebaseRestAuthService` (Firebase Auth REST: email/password, Google IdP, refresh token) |
| `IRandomSource` | `SeededRandomSource` (prod), fake trong test |
| Wire contract (DTO) | NGO RPC trong `NetworkMatchBridge.*` |
| Authoring content | `CardDefinitionMapper` (ScriptableObject → Domain definition) |
| Hosting/matchmaking | `DedicatedMatchmakerClient` (UGS Matchmaker → Edgegap), `ListenSessionController` (Session/Relay) |

Quy tắc:
- External system mới (analytics, leaderboard, Cloud Save...) → định nghĩa port nhỏ theo nhu cầu consumer, adapter ở
  `Infrastructure/`. Domain không bao giờ thấy SDK.
- Không tạo port cho helper một dòng nếu không có ≥ 2 implementation hoặc test seam thật.

## 3. MVP-style Presentation

- **Model**: `MatchClientProjection` (snapshot DTO đã lọc) + `MatchUpdateInbox` (resolution/rejection).
- **Presenter**: `MatchBoardPresenter`, `MatchHudPresenter`, `MulliganPresenter`, `MatchResultPresenter`,
  `AbilityTargetSelectionController`, `TurnBannerCoordinator`...
- **View**: `NetworkCardVisual`, `CardArtworkView`, drop zone, `CombatCardAnimator`, VFX view.

Quy tắc:
- View không đọc `MatchState` hay gọi RPC. Presenter gửi intent qua facade `NetworkMatchBridge`.
- Presenter không quyết định luật; nó chỉ dựng view state và staging local (có thể hoàn tác).

## 4. Authoritative Command + Filtered Projection

```text
Command side:  intent ─► RPC ─► Try...() ─► mutate MatchState ─► CommandResult + events
Query side:    MatchState ─► MatchSnapshotBuilder.Build(viewer) ─► DTO riêng từng seat (revision++)
Side channel:  RoundResolution / AbilityResolution ─► DtoMapper (lọc theo viewer) ─► animation
```

Quy tắc:
- **Không phải CQRS đầy đủ** (không event store, không DB tách). Gọi là "command + filtered projection".
- Mọi field DTO mới phải trả lời: viewer đối thủ có được thấy không? Mặc định là **không**.
- Snapshot luôn tăng `revision`; client bỏ qua revision cũ.

## 5. Typed Domain Events (nội bộ)

- Domain phát event có thứ tự (`GameEvent`/`GameEventBatch` (`GameEvents.cs`), `AbilityResolutionEvent.Sequence`) để trigger resolver và để
  Networking/Presentation chuyển tiếp kết quả.
- Không dùng global event bus / static `EventManager`.
