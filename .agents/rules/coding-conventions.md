# Rule — Viết code: region, XML summary, bố cục

> Áp dụng cho mọi file `.cs` dưới `Assets/Scripts`, `Assets/Editor/KLTN`, `Assets/Tests`.
> Định dạng cơ bản do `.editorconfig` quy định: UTF-8, **CRLF**, 4 space, Allman brace, tối đa 90 cột,
> `using` ngoài namespace, `System.*` đứng đầu.
> API Unity phải đúng phiên bản **6000.5.4f1** — xem `unity-api-compliance.md` (không dùng API obsolete).

## 1. Namespace

- Code trong `Game/` dùng `KLTN.Game.Domain | Content | Networking | Presentation` (không bắt buộc khớp folder con).
- Code mới ngoài `Game/` nên có namespace `KLTN.<Module>`.
- **Không** đổi namespace/tên class của MonoBehaviour đang gắn trong scene/prefab nếu chưa có kế hoạch migration
  (`m_EditorClassIdentifier`, GUID). Di chuyển file luôn kèm `.meta`.

## 2. `#region` — bắt buộc cho class có từ 2 nhóm trách nhiệm trở lên

Thứ tự chuẩn (bỏ nhóm không có):

```csharp
#region Constants / Configuration      // const, static readonly, cấu hình
#region Serialized Fields              // [SerializeField] (MonoBehaviour)
#region Dependencies                   // service/component được inject hoặc GetComponent
#region Runtime State                  // field private thay đổi lúc chạy
#region Events / Properties
#region Construction / Static Lifecycle
#region Unity Lifecycle                // Awake, OnEnable, Start, Update, OnDisable, OnDestroy
#region Public API / Public Commands   // method public thay đổi state hoặc gửi intent
#region Queries
#region Resolution / <Use case>        // workflow private theo use case
#region Helpers / Validation / Cleanup
```

Quy tắc:
- Tên region dùng tiếng Anh, Title Case, lấy từ danh sách trên trước khi tự đặt tên mới
  (tên đã dùng nhiều trong repo: `Unity Lifecycle`, `Serialized Fields`, `Construction`, `Runtime State`, `Properties`, `Helpers`).
- Mỗi `#region` phải có `#endregion` cân bằng; không lồng region.
- Partial file theo use case: mỗi partial có region riêng mô tả use case đó (VD `#region Attack Declaration`).
- File ngắn (< ~60 dòng, 1 nhóm trách nhiệm) **không cần** region.
- Region không thay cho việc tách class: nếu một file cần > 8 region, cân nhắc tách trách nhiệm.

## 3. XML `/// <summary>` — bắt buộc ở các vị trí sau

1. Mọi class/struct/interface **public** trong `Game/Domain`, `Game/Networking`, và coordinator/service ở Presentation.
2. Method public **thay đổi authoritative state** (`MatchRulesEngine.Try...`, `PlayerState` mutation) và mọi `Request...`/`Submit...Rpc`.
3. Thuật toán không hiển nhiên: target enumeration/validation, trigger ordering, snapshot filtering, DTO mapping,
   combat sequencing, rollback (cancel selection), deterministic ordering.
4. Method có **invariant/side effect khó thấy từ tên** (VD: đổi zone, hoàn mana, tăng revision).

Viết thế nào:
- Câu 1: **làm gì** (động từ, ngôi thứ ba). Câu 2+: **vì sao / invariant / điều kiện thất bại**.
- Dùng `<param>`, `<returns>`, `<remarks>` khi có ràng buộc thật (VD "null nếu không có target hợp lệ").
- Dùng `<see cref="..."/>` để nối tới type liên quan.
- Không lặp lại tên hàm ("Gets the damage" cho `GetDamage`), không kể lại từng dòng code.
- Ngôn ngữ summary: tiếng Anh (thống nhất với code hiện có). Comment inline giải thích nghiệp vụ được phép tiếng Việt.

Mẫu:

```csharp
#region Public Commands

/// <summary>
/// Ends the match immediately when a player surrenders.
/// The opponent is declared the winner.
/// </summary>
/// <returns>
/// <see cref="CommandResult.Reject"/> with <c>MatchFinished</c> if the match already ended.
/// </returns>
public CommandResult TrySurrender(MatchState state, SeatId actor)
{
    ...
}

#endregion
```

## 4. Bố cục khai báo trong class

1. constants/configuration → 2. serialized fields → 3. dependencies → 4. runtime state → 5. events/properties
→ 6. constructor/static lifecycle → 7. Unity lifecycle → 8. public commands/queries → 9. private workflow
→ 10. helpers/validation/cleanup.

## 5. Quy tắc riêng theo tầng

| Tầng | Bắt buộc |
|---|---|
| Domain | Không Unity API; `sealed` mặc định; random qua `IRandomSource`; trả `CommandResult` thay vì throw cho lỗi nghiệp vụ; throw chỉ cho lỗi lập trình (null argument, invariant hỏng). |
| Networking | RPC handler: `TryBeginCommand` → gọi `MatchRulesEngine` → publish. DTO là class `[Serializable]` field public camelCase. |
| Presentation | Unsubscribe event trong `OnDisable/OnDestroy`; không giữ tham chiếu `CardInstance` Domain; animation dùng dữ liệu resolution đã chụp. |
| Async (UGS/Firebase) | `async Task` thay vì `async void` (trừ Unity event handler); bắt exception ở biên, dịch sang message cho UI; hỗ trợ `CancellationToken` khi chờ lâu (matchmaking). |
| Test | Mỗi rule/effect mới có EditMode test ở `Tests/EditMode/Domain/<Nhóm>/`; tạo match qua `MatchFactory` + `SeededRandomSource`. |

## 6. Comment

- Giải thích **vì sao** và **invariant**, không giải thích **cái gì** nếu code đã rõ.
- `// TODO(<tên>): ...` kèm lý do; không để TODO trống.
- `#pragma warning disable` chỉ quanh đúng đoạn cần (VD CS0649 cho JSON DTO), có comment lý do.
