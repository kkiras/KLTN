---
name: adjust-shop-economy
description: Đổi giá, tier, bộ starter, thêm thẻ vào Shop hoặc đổi mức thưởng trận trong KLTN, giữ đồng bộ ShopCatalog, Domain và Firestore Security Rules.
---

# Điều chỉnh kinh tế Shop

Nguồn sự thật: bảng `Table()` trong `Assets/Editor/KLTN/ShopCatalogInstaller.cs`.
Từ đó sinh ra `Assets/Resources/ShopCatalog.asset` (client + server đọc) và `Firebase/firestore.rules` (giá nhúng trong rules).

## Đổi giá / tier / starter / thêm thẻ
1. Sửa dòng `E("<CardId>", Currency.<None|Coins|Silver>, <giá>, CardTier.<Weak|Mid|Strong>, <P>)`.
   - `Currency.None` = starter. Đổi số lượng starter → kiểm lại `WeightedDeckBuilderTests` và ngưỡng fallback trong `NetworkMatchBridge.Loadout.cs`.
   - Tier quyết định trọng số deck (Weak 3 / Mid 2 / Strong 1). Tính P theo công thức trong `.agents/memory.md` §11.
2. Unity: **Tools/KLTN/Install Shop Catalog** (cập nhật asset và tự xuất rules).
3. Firebase Console → Firestore → Rules: dán toàn bộ `Firebase/firestore.rules` → thử Rules Playground → **Publish**.
   Quên bước này → mua thẻ bị `PERMISSION_DENIED` vì giá trong rules khác giá client.
4. Người chơi đã có thẻ không bị ảnh hưởng (ownedCards giữ nguyên).

## Đổi mức thưởng
Sửa đồng bộ 3 nơi: `MatchRewardPolicy` (hằng số) → `MatchRewardPolicyTests` → hàm `validReward` trong `RulesTemplate`
của `ShopCatalogInstaller.cs`; rồi Export Firestore Rules và Publish như bước 3.

## Kiểm chứng
- EditMode: `KLTN.Game.Domain.Tests` pass.
- Trong game: Shop hiện giá mới; mua 1 thẻ trừ đúng giá; hết trận nhận đúng mức thưởng (xem tab Data trên Console).

## Không được
- Sửa tay `ShopCatalog.asset` hoặc `firestore.rules` (bị ghi đè lần chạy installer sau).
- Gọi Firestore trực tiếp từ UI — dùng `PlayerProfileService` / `IEconomyGateway`.
