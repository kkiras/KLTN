# Rule — SOLID trong KLTN

> Áp dụng SOLID như **công cụ giảm chi phí thay đổi**, không phải checklist để thêm interface.
> Mỗi nguyên tắc dưới đây gồm: quy tắc → áp dụng hiện tại → cấm/không nên.

## S — Single Responsibility

**Quy tắc:** một class có một *lý do thay đổi*. Đo bằng "ai yêu cầu sửa nó", không bằng số dòng.

Áp dụng hiện tại:
- Domain tách: `AbilityTargetValidator` (target hợp lệ) / `AbilityTriggerResolver` (thu thập trigger, FIFO queue)
  / `AbilityEffectExecutor` (thực thi effect) / `UnitPassiveRules` (passive) / `MatchSnapshotBuilder` (read model).
- `partial class` theo use case (`MatchRulesEngine.Attack/Block/Summoning/Surrender/...`) — đây là **tổ chức file**,
  không phải tách trách nhiệm runtime.

Làm:
- Use case mới của match → partial mới `MatchRulesEngine.<UseCase>.cs`, không nhét vào file có sẵn.
- Logic hiển thị mới → presenter/view riêng, không thêm vào `MatchDebugControls` hay `NetworkMatchBridge`.

Không:
- Thêm trách nhiệm mới cho `NetworkMatchBridge` (đã là god-object tiềm năng: connection, seat, init match, command, publish).
- Tách file chỉ vì > N dòng khi nội dung vẫn cohesive.

## O — Open/Closed

**Quy tắc:** mở rộng hành vi bằng **dữ liệu/composition** trước, sửa code trung tâm sau cùng.

Áp dụng hiện tại:
- Card mới dùng trigger/effect có sẵn → chỉ cần ScriptableObject + `CardContentInstaller`, không sửa rules.
- Effect mới hoàn toàn → phải sửa `EffectKind`, `switch` trong `AbilityEffectExecutor`, `CardContentValidator`, test.
  Đây là trade-off **chấp nhận được** khi số effect còn ~15.

Không:
- `if (card.DefinitionId == "ma_da")` trong rules. Hành vi đặc thù → keyword/passive/effect data.
- Tạo Strategy registry cho effect trước khi số effect hoặc tần suất thêm effect thực sự gây đau.

## L — Liskov Substitution

**Quy tắc:** implementation của một abstraction phải giữ cùng contract (pre/post-condition, lỗi, side effect).

Áp dụng hiện tại:
- `IAuthService` (`FirebaseRestAuthService`): `AuthResultData.Success=false` + message thay vì throw cho lỗi nghiệp vụ;
  `IdToken` rỗng khi thất bại. Implementation mới phải giữ đúng hành vi này.
- `IRandomSource`: `Next(min, maxExclusive)` — fake trong test phải tôn trọng khoảng.
- Domain ưu tiên `sealed` + composition → gần như không có cây kế thừa.

Không:
- Implementation mới throw ở chỗ implementation cũ trả `Success=false` (caller `AuthManager` sẽ vỡ).

## I — Interface Segregation

**Quy tắc:** interface nhỏ, đặt theo nhu cầu của consumer.

Áp dụng hiện tại: `IAuthService`, `IRandomSource` — không có mega interface.

Không:
- Tạo interface cho catalog/helper chỉ có một implementation và không có test seam thật.
- Gộp auth + session + matchmaking vào một interface "IOnlineService".

## D — Dependency Inversion

**Quy tắc:** module chính sách (Domain) không phụ thuộc chi tiết (Unity/NGO/UGS/Firebase).

Áp dụng hiện tại:
- `KLTN.Game.Domain.asmdef` bật `noEngineReferences` — compiler chặn `UnityEngine`.
- Random, auth phụ thuộc abstraction.

Còn nợ:
- Presentation/Networking lấy dependency qua static registry/singleton
  (`MatchProjectionRegistry.Current`, `MatchUpdateInboxRegistry.Current`, `UgsSessionService.Instance`, `AuthManager.Instance`).
  Code mới **không thêm singleton mới**; ưu tiên `[SerializeField]` hoặc composition root trong scene.
- Mulligan dùng `new System.Random()` trực tiếp → vi phạm DIP và mất determinism. Sửa khi đụng vào Mulligan.

## Checklist review nhanh

- [ ] File Domain có `using UnityEngine`/`Unity.Netcode`? → reject.
- [ ] Rules có so sánh với ID/tên card cụ thể? → chuyển thành data.
- [ ] Thêm `static Instance` mới? → giải thích vì sao không inject được.
- [ ] Interface mới có ≥ 2 implementation hoặc test seam thật?
