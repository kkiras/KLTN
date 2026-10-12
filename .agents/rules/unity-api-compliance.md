# Rule — Tuân thủ API Unity 6 (6000.5.4f1)

> Dự án dùng **Unity 6.5 — `6000.5.4f1`** (`ProjectSettings/ProjectVersion.txt`).
> Mọi code sinh ra/sửa đổi phải đúng với **Scripting API và Manual của phiên bản 6000.5**, không dùng API theo trí nhớ
> từ Unity 2021/2022/6.0. Khi không chắc → tra docs đúng phiên bản trước khi viết.

## 1. Nguồn tham chiếu (theo thứ tự ưu tiên)

1. Thông báo của compiler trong chính project (`CS0618` = obsolete warning, `CS0619` = obsolete error):
   message của `[Obsolete]` luôn nêu API thay thế. Xem Console hoặc `Logs/`.
2. Scripting API: `https://docs.unity3d.com/6000.5/Documentation/ScriptReference/<Type>.<Member>.html`
3. Manual — migration guide: `https://docs.unity3d.com/Manual/instanceid-to-entityid-migration.html`
4. Package docs đúng version trong `Packages/manifest.json` (NGO `2.13.0`, Multiplayer Services `2.3.1`, ...).

## 2. API đã obsolete trong 6000.5 — KHÔNG dùng

| Không dùng | Dùng thay | Ghi chú |
|---|---|---|
| `Object.FindFirstObjectByType<T>()` | `Object.FindAnyObjectByType<T>()` | Obsolete vì dựa vào thứ tự instance ID (compiler: *"Use FindAnyObjectByType instead, which does not depend on ordering"*) |
| `Object.FindObjectOfType<T>()` | `Object.FindAnyObjectByType<T>()` | |
| `Object.FindObjectsOfType<T>()` | `Object.FindObjectsByType<T>()` / `FindObjectsByType<T>(FindObjectsInactive)` | |
| `Object.FindObjectsByType<T>(FindObjectsSortMode)` | `Object.FindObjectsByType<T>()` / `(FindObjectsInactive)` | `FindObjectsSortMode` bị deprecate từ 6000.4 |
| `Object.GetInstanceID()` | `Object.GetEntityId()` | Từ 6.5, API `InstanceID` obsolete **gây lỗi compile** |
| `Resources.InstanceIDToObject(int)` | `Resources.EntityIdToObject(EntityId)` | |
| `EditorUtility.InstanceIDToObject(int)` | `EditorUtility.EntityIdToObject(EntityId)` | Editor script |
| `Selection.instanceIDs` / `Selection.activeInstanceID` | `Selection.entityIds` / `Selection.activeEntityId` | Editor script |

Bảng này **không đầy đủ** — bổ sung mỗi khi gặp `CS0618/CS0619` mới.

## 3. Hệ quả ngữ nghĩa cần hiểu (không chỉ đổi tên hàm)

- `FindAnyObjectByType` trả về **một instance bất kỳ**, không đảm bảo là instance "đầu tiên". Nếu scene có thể có
  > 1 instance (VD: `NetworkMatchBridge` khi đổi scene, bản duplicate test scene) → kết quả không xác định.
  → Ưu tiên `[SerializeField]` reference hoặc composition root; chỉ dùng `FindAnyObjectByType` cho object đảm bảo duy nhất,
  và gọi một lần (Awake/Start), cache lại — không gọi trong `Update`.
- Không lưu `EntityId` xuống disk/network như `int`; không suy ra thứ tự tạo object từ ID.
- Không dùng instance ID/EntityId của Unity làm ID gameplay — gameplay dùng `CardInstance.InstanceId` (Domain, `ulong`).

## 4. Quy trình trước khi commit code Unity

- [ ] Code mới/sửa không gây `CS0618` hoặc `CS0619` (mục tiêu: 0 warning).
- [ ] API ít gặp đã tra Scripting API bản 6000.5.
- [ ] `rg -n "FindFirstObjectByType|FindObjectOfType|FindObjectsOfType|FindObjectsSortMode|GetInstanceID|InstanceIDToObject" Assets/Scripts Assets/Editor Assets/Tests`
      → không có kết quả.

## 5. Vi phạm hiện có trong repo

| File | Dòng | Sửa |
|---|---|---|
| `Assets/Scripts/UI/SurrenderUI.cs` | 24 | `FindFirstObjectByType<NetworkMatchBridge>()` → `FindAnyObjectByType<NetworkMatchBridge>()` (tốt hơn: `[SerializeField]`) |
