---
name: add-unit-card
description: Thêm hoặc sửa Unit card / ability trong KLTN bằng dữ liệu (ScriptableObject + CardContentInstaller), không sửa rules theo tên card.
---

# Thêm Unit card / ability

1. **Kiểm tra vocabulary có đủ không** — `Assets/Scripts/Game/Domain/Abilities/AbilityDefinitions.cs`
   (`AbilityTrigger`, `AbilityCondition`, `TargetRelation`, `AbilityTargetZone`, `EffectKind`, `EffectTarget`, `EffectDuration`, `UnitKeyword`).
   - Đủ → làm tiếp bước 2.
   - Thiếu effect → dừng, làm theo mục "Effect mới" bên dưới.
2. **Asset** — tạo/sửa `Assets/Resources/Cards/<Name>.asset` (schema: `Game/Content/Authoring/Card.cs`, `CardAbilityData.cs`).
3. **Installer** — khai báo ability trong `Assets/Editor/KLTN/CardContentInstaller.cs` (`Configure`, `Ability(...)`, `Target(...)`,
   `SetMaximumCopiesPerDeck`). Chạy menu **Tools/KLTN/Install Unit Card Content** (idempotent).
4. **Mapping** — xác nhận `CardDefinitionMapper` map đủ field mới (nếu thêm field authoring).
5. **Validate** — `CardContentValidator` phải chấp nhận; `CounterSpell` sẽ bị từ chối (chưa có stack).
6. **Test** — thêm EditMode test ở `Assets/Tests/EditMode/Domain/Abilities/` dùng `MatchFactory` + `SeededRandomSource`:
   trường hợp có target, không target (`MissingTargetPolicy`), cancel, và tương tác keyword nếu có.
7. **Presentation** — artwork prefab trong `CardPresentationCatalog` nếu nhân vật vượt khung; animation skill lấy từ
   `AbilityResolution` DTO (`CombatAnimationDirector.Abilities`).

8. **Shop/kinh tế** — thêm dòng cho card mới trong bảng `ShopCatalogInstaller.Table()` (starter/xu/bạc, giá, tier),
   rồi làm theo skill `adjust-shop-economy`. Thiếu dòng → card không bán được và bị tính trọng số Mid khi build deck.

## Effect mới
Sửa đồng bộ: `EffectKind` → `AbilityEffectExecutor` (dispatch + partial phù hợp) → `CardContentValidator` →
`AbilityResolutionEvent`/DTO mapper nếu cần animation → test. Ghi lại vào `.agents/memory.md` nếu đổi quyết định OCP.

## Không được
- `if (definitionId == "...")` trong Domain rules.
- Đọc ScriptableObject trực tiếp trong Domain.
