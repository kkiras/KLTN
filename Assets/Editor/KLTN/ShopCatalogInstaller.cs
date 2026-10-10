#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using KLTN.Game.Content;
using KLTN.Game.Domain.Economy;
using UnityEditor;
using UnityEngine;

namespace KLTN.Game.Editor
{
    /// <summary>
    /// Creates Resources/ShopCatalog.asset from the balancing table and exports
    /// Firebase/firestore.rules with the price table embedded.
    /// </summary>
    [InitializeOnLoad]
    public static class ShopCatalogInstaller
    {
        private const string AssetPath = "Assets/Resources/ShopCatalog.asset";
        private const string RulesPath = "Firebase/firestore.rules";

        static ShopCatalogInstaller()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<ShopCatalog>(AssetPath) == null)
                {
                    Install();
                }
            };
        }

        // P = I x clamp(sqrt(I / (2*cost + 1)), 0.8, 1.3). See claude/shop-economy-plan.md.
        private static List<ShopCatalogEntry> Table()
        {
            return new List<ShopCatalogEntry>
            {
                E("MaDa", Currency.None, 0, CardTier.Weak, 3.6f),
                E("MaDoi", Currency.None, 0, CardTier.Weak, 5.2f),
                E("MaGa", Currency.None, 0, CardTier.Weak, 6.5f),
                E("MaLon", Currency.None, 0, CardTier.Weak, 6.5f),
                E("QuyMotDo", Currency.None, 0, CardTier.Weak, 6.6f),
                E("VongNhi", Currency.None, 0, CardTier.Weak, 7.0f),
                E("OngKe", Currency.None, 0, CardTier.Weak, 7.5f),
                E("QuyCau", Currency.None, 0, CardTier.Weak, 8.3f),

                E("MaMatMam", Currency.Coins, 150, CardTier.Weak, 8.3f),
                E("MaLai", Currency.Coins, 200, CardTier.Weak, 8.6f),
                E("ThienLinhCai", Currency.Coins, 200, CardTier.Mid, 9.0f),
                E("AmBinh", Currency.Coins, 350, CardTier.Mid, 10.2f),
                E("MaCo", Currency.Coins, 350, CardTier.Mid, 10.2f),
                E("MaCangSung", Currency.Coins, 550, CardTier.Mid, 12.5f),
                E("MaTroi", Currency.Coins, 550, CardTier.Mid, 12.7f),
                E("ThanTrung", Currency.Coins, 600, CardTier.Mid, 13.0f),

                E("LinhMieu", Currency.Silver, 10, CardTier.Strong, 15.0f),
                E("MaTranh", Currency.Silver, 12, CardTier.Strong, 20.1f),
                E("QuyDaXoa", Currency.Silver, 15, CardTier.Strong, 34.0f),
            };
        }

        [MenuItem("Tools/KLTN/Install Shop Catalog")]
        public static void Install()
        {
            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(AssetPath);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ShopCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            Undo.RecordObject(catalog, "Install Shop Catalog");
            catalog.ReplaceEntries(Table());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            foreach (ShopCatalogEntry entry in Table())
            {
                if (Resources.Load($"Cards/{entry.cardId}") == null)
                {
                    Debug.LogWarning($"[ShopCatalog] Card asset not found: Resources/Cards/{entry.cardId}");
                }
            }

            ExportRules();
            Debug.Log($"[ShopCatalog] Installed {Table().Count} entries at {AssetPath}.");
        }

        [MenuItem("Tools/KLTN/Export Firestore Rules")]
        public static void ExportRules()
        {
            var prices = new StringBuilder();
            var starters = new StringBuilder();

            foreach (ShopCatalogEntry e in Table())
            {
                if (e.currency == Currency.None)
                {
                    starters.Append(starters.Length == 0 ? "" : ", ").Append($"'{e.cardId}'");
                    continue;
                }

                string currency = e.currency == Currency.Coins ? "coins" : "silver";
                prices.Append(prices.Length == 0 ? "" : ",\n        ")
                    .Append($"'{e.cardId}': ['{currency}', {e.price}]");
            }

            string rules = RulesTemplate
                .Replace("__PRICES__", prices.ToString())
                .Replace("__STARTERS__", starters.ToString());

            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, RulesPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, rules.Replace("\r\n", "\n"));
            Debug.Log($"[ShopCatalog] Firestore rules written to {RulesPath}. Paste it into Firebase Console > Firestore > Rules.");
        }

        // Prices are embedded in the rules, so no /shopCatalog collection needs seeding.
        // Every economy write is a :commit batch; rules check the batch result with getAfter/existsAfter.
        private const string RulesTemplate = @"rules_version = '2';

// GENERATED by Tools/KLTN/Export Firestore Rules (ShopCatalogInstaller.cs). Do not edit by hand.
// Phase 1: clients write their own economy, rules enforce exact prices and reward amounts.
// Phase 2 (server via Cloud Code): make isReward() return false so only the server can grant rewards.
service cloud.firestore {
  match /databases/{database}/documents {

    function prices() {
      return {
        __PRICES__
      };
    }

    function starters() {
      return [__STARTERS__];
    }

    function isOwner(uid) {
      return request.auth != null && request.auth.uid == uid;
    }

    function userPath(uid) {
      return /databases/$(database)/documents/users/$(uid);
    }

    function cardPath(uid, cardId) {
      return /databases/$(database)/documents/users/$(uid)/ownedCards/$(cardId);
    }

    function rewardPath(uid, matchId) {
      return /databases/$(database)/documents/users/$(uid)/matchRewards/$(matchId);
    }

    function isPurchase(uid) {
      let d = request.resource.data;
      let o = resource.data;
      let card = d.get('lastPurchase', '');
      return d.diff(o).affectedKeys().hasOnly(['coins', 'silver', 'lastPurchase', 'updatedAt'])
        && card is string
        && card in prices()
        && !exists(cardPath(uid, card))
        && existsAfter(cardPath(uid, card))
        && d.coins >= 0 && d.silver >= 0
        && ((prices()[card][0] == 'coins'
              && d.coins == o.coins - prices()[card][1]
              && d.silver == o.silver)
          || (prices()[card][0] == 'silver'
              && d.silver == o.silver - prices()[card][1]
              && d.coins == o.coins));
    }

    function isReward(uid) {
      let d = request.resource.data;
      let o = resource.data;
      let m = d.get('lastRewardMatchId', '');
      return d.diff(o).affectedKeys().hasOnly(['coins', 'silver', 'lastRewardMatchId', 'updatedAt'])
        && m is string && m.size() > 0 && m.size() <= 64
        && !exists(rewardPath(uid, m))
        && existsAfter(rewardPath(uid, m))
        && d.coins == o.coins + getAfter(rewardPath(uid, m)).data.coins
        && d.silver == o.silver + getAfter(rewardPath(uid, m)).data.silver;
    }

    function validReward(r) {
      return (r.result == 'win' && r.coins == 75 && r.silver == 1)
        || (r.result in ['loss', 'draw'] && r.coins == 25 && r.silver == 0)
        || (r.result == 'surrender_early' && r.coins == 0 && r.silver == 0);
    }

    match /users/{uid} {
      allow read: if isOwner(uid);

      allow create: if isOwner(uid)
        && request.resource.data.keys().hasOnly(['coins', 'silver', 'schemaVersion', 'createdAt', 'updatedAt'])
        && request.resource.data.coins == 0
        && request.resource.data.silver == 0;

      allow update: if isOwner(uid) && (isPurchase(uid) || isReward(uid));
      allow delete: if false;

      match /ownedCards/{cardId} {
        allow read: if isOwner(uid);

        allow create: if isOwner(uid)
          && request.resource.data.keys().hasOnly(['acquiredAt', 'source'])
          && (
            (request.resource.data.source == 'starter'
              && cardId in starters()
              && !exists(userPath(uid))
              && existsAfter(userPath(uid)))
            || (cardId in prices()
              && request.resource.data.source == prices()[cardId][0]
              && getAfter(userPath(uid)).data.get('lastPurchase', '') == cardId)
          );

        allow update, delete: if false;
      }

      match /matchRewards/{matchId} {
        allow read: if isOwner(uid);

        allow create: if isOwner(uid)
          && request.resource.data.keys().hasOnly(['result', 'coins', 'silver', 'createdAt'])
          && validReward(request.resource.data)
          && getAfter(userPath(uid)).data.get('lastRewardMatchId', '') == matchId;

        allow update, delete: if false;
      }
    }
  }
}
";

        private static ShopCatalogEntry E(string id, Currency currency, int price, CardTier tier, float power)
        {
            return new ShopCatalogEntry
            {
                cardId = id,
                currency = currency,
                price = price,
                tier = tier,
                powerScore = power,
            };
        }
    }
}
#endif
