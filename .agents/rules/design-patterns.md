# Rule — Design Patterns trong KLTN

> Design pattern = cách một nhóm class cộng tác để giải **một vấn đề cục bộ**.
> Gọi đúng tên mức độ áp dụng ("Builder-style", "Command-style") — không thổi phồng thành GoF đầy đủ.

## 1. Pattern đang dùng — giữ đúng vai trò

| Pattern | Ở đâu | Quy tắc khi sửa |
|---|---|---|
| **Simple Factory** | `MatchFactory.Create` | Mọi `MatchState` mới (runtime và test) phải tạo qua đây để cùng invariant: validate deck → mana → instance ID → shuffle (`IRandomSource`) → opening hand. Không dựng `MatchState` thủ công trong Bridge. |
| **Builder-style Assembler** | `DefaultDeckBuilder.Build`, `MatchSnapshotBuilder.Build` | One-shot, không fluent, không Director. Chỉ nâng lên GoF Builder khi có nhiều representation thật. |
| **Command-style Intent** | `Request...` → `Submit...Rpc` → `MatchRulesEngine.Try...` → `CommandResult` | Command mới phải đi đủ 4 bước + CommandId + rejection reason. Không mutate state trong RPC handler. |
| **Explicit FSM** | `MatchPhase` + guard trong `MatchRulesEngine.Validation` | Phase mới = enum + guard + transition + test. Không đổi `Phase` ngoài Domain. |
| **Observer** | C# event ở `MatchClientProjection`, `MatchUpdateInbox`; presenter subscribe | Luôn unsubscribe trong `OnDisable/OnDestroy`. Không dùng global event bus. |
| **Strategy** | `IRandomSource` / `SeededRandomSource` | Mọi random ở Domain đi qua đây. |
| **Behavior Composition** | `AbilityDefinition` = trigger + condition + target requirements + ordered effects | Card mới = tổ hợp data. Không subclass theo card. |
| **Adapter** | `FirebaseRestAuthService : IAuthService`; boundary NGO/UGS | Lỗi SDK/HTTP được dịch thành `AuthResultData`, không rò exception SDK ra UI. |
| **Data Mapper** | `CardDefinitionMapper` (SO→Domain), `RoundResolutionDtoMapper`, `AbilityResolutionDtoMapper` (Domain→DTO) | Mapper không chứa luật. Mapper DTO phải lọc thông tin ẩn theo viewer. |
| **Catalog / Lookup** | `CardAssetCatalog`, `CardPresentationCatalog` | Tra theo stable card ID. Không phải Repository (không persistence). |
| **Null Object** | `UnitPassiveRules.None` | Card không passive vẫn có policy hợp lệ — không trả `null`. |
| **Facade / Gateway** | API public của `NetworkMatchBridge` | Presentation chỉ gọi facade, không gọi NGO trực tiếp. Không mở rộng facade thêm trách nhiệm không liên quan. |
| **Snapshot (value capture)** | `AbilityCardState.Capture` trong `AbilityResolution` | Chụp giá trị trước khi card đổi zone/máu để animation không đọc state đã thay đổi. |
| **Singleton** (nợ) | `AuthManager`, `GoogleAuthService`, `AppFlowManager`, `UgsSessionService` | Đang tồn tại, không nhân rộng. |

## 2. Những thứ KHÔNG gọi là pattern

- `partial class` → kỹ thuật chia file.
- Method tên `Build` → không tự động là Builder.
- `switch (EffectKind)` → dispatch đơn giản, chưa phải Strategy registry.
- FIFO ability queue → lựa chọn cấu trúc dữ liệu cho deterministic ordering.

## 3. Khi nào được thêm pattern mới

Chỉ khi trả lời được cả 3:
1. **Vấn đề thật** đang gây đau là gì (bug lặp lại, sửa 1 chỗ phải sửa N chỗ, không test được)?
2. Pattern giảm chi phí đó **ở đâu** và tăng chi phí gì (indirection, số file, lifecycle)?
3. Có cách đơn giản hơn (hàm, data, enum) không?

Ứng viên đã biết (chưa làm):
- **GoF Command + dispatcher** khi thêm spell stack → command object có thể xếp hàng/undo/replay.
- **Strategy registry cho effect** khi `EffectKind` > ~25 hoặc team thêm effect hàng tuần.
- **Composition root** thay static registry khi cần PlayMode test cô lập.
