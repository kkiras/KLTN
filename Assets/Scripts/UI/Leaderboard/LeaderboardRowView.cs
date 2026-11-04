using System.Globalization;
using KLTN.Game.Content;
using KLTN.Game.Domain.Ranking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KLTN.UI.Leaderboard
{
    /// <summary>
    /// One leaderboard row: STT | Username | Rank (icon + name) | Điểm | Tỉ lệ thắng.
    /// Top 1-3 get medal tones; the viewer's own row is light yellow with dark text.
    /// </summary>
    public sealed class LeaderboardRowView : MonoBehaviour
    {
        #region Palette

        private static readonly Color NormalBackground = new Color(0.16f, 0.13f, 0.15f, 0.92f);
        private static readonly Color AltBackground = new Color(0.13f, 0.11f, 0.12f, 0.92f);
        private static readonly Color GoldBackground = new Color(0.62f, 0.46f, 0.12f, 0.95f);
        private static readonly Color SilverBackground = new Color(0.42f, 0.45f, 0.50f, 0.95f);
        private static readonly Color BronzeBackground = new Color(0.50f, 0.29f, 0.15f, 0.95f);
        private static readonly Color SelfBackground = new Color(1f, 0.93f, 0.62f, 1f);

        private static readonly Color GoldBadge = new Color(1f, 0.82f, 0.25f, 1f);
        private static readonly Color SilverBadge = new Color(0.86f, 0.89f, 0.94f, 1f);
        private static readonly Color BronzeBadge = new Color(0.86f, 0.55f, 0.30f, 1f);

        private static readonly Color LightText = new Color(0.96f, 0.93f, 0.88f, 1f);
        private static readonly Color DarkText = new Color(0.16f, 0.11f, 0.06f, 1f);

        #endregion

        #region Layout

        private const float RankIconGap = 12f;
        private const float CellPadding = 8f;

        #endregion

        [SerializeField] private Image background;
        [SerializeField] private Outline highlight;
        [SerializeField] private Image positionBadge;
        [SerializeField] private TextMeshProUGUI positionText;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private Image rankIcon;
        [SerializeField] private TextMeshProUGUI rankNameText;
        [SerializeField] private TextMeshProUGUI pointsText;
        [SerializeField] private TextMeshProUGUI winRateText;

        public RectTransform Rect => (RectTransform)transform;

        public void Bind(int position, SeasonStanding standing, bool isSelf, RankCatalog catalog)
        {
            RankTier tier = standing.Tier;

            positionText.text = position > 0 ? position.ToString(CultureInfo.InvariantCulture) : "—";
            usernameText.text = isSelf ? standing.Username + "  (Bạn)" : standing.Username;
            rankNameText.text = catalog != null ? catalog.NameOf(tier) : RankCatalog.FallbackName(tier);
            pointsText.text = standing.Points.ToString("N0", CultureInfo.InvariantCulture);
            winRateText.text = (standing.WinRate * 100d).ToString("0.#", CultureInfo.InvariantCulture) + "%";

            Sprite icon = catalog != null ? catalog.IconOf(tier) : null;
            rankIcon.sprite = icon;
            rankIcon.enabled = icon != null;

            ApplyStyle(position, isSelf);
            CenterUsername();
            CenterRank();
        }

        /// <summary>Username (with or without "(Bạn)") centred in its column.</summary>
        private void CenterUsername()
        {
            RectTransform rect = usernameText.rectTransform;
            rect.offsetMin = new Vector2(CellPadding, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-CellPadding, rect.offsetMax.y);
            usernameText.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// Icon + rank name centred as one group: the group width follows the name length, so
        /// "Đồng" and "Thách Đấu" both sit in the middle of the column. Positions are relative to the
        /// cell centre, so this works before the layout group has given the row its width.
        /// </summary>
        private void CenterRank()
        {
            RectTransform cell = (RectTransform)rankNameText.rectTransform.parent;
            float iconWidth = rankIcon.enabled ? rankIcon.rectTransform.sizeDelta.x : 0f;
            float gap = rankIcon.enabled ? RankIconGap : 0f;
            float textWidth = rankNameText.GetPreferredValues(rankNameText.text).x;
            float available = cell.rect.width - iconWidth - gap - 2f * CellPadding;

            if (available > 0f)
            {
                textWidth = Mathf.Min(textWidth, available);
            }

            float left = -(iconWidth + gap + textWidth) * 0.5f;
            Vector2 middle = new Vector2(0.5f, 0.5f);

            RectTransform iconRect = rankIcon.rectTransform;
            iconRect.anchorMin = middle;
            iconRect.anchorMax = middle;
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(left, 0f);

            RectTransform nameRect = rankNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 0f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0f, 0.5f);
            nameRect.sizeDelta = new Vector2(textWidth, 0f);
            nameRect.anchoredPosition = new Vector2(left + iconWidth + gap, 0f);
            rankNameText.alignment = TextAlignmentOptions.MidlineLeft;
            rankNameText.textWrappingMode = TextWrappingModes.NoWrap;
            rankNameText.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void ApplyStyle(int position, bool isSelf)
        {
            bool podium = position >= 1 && position <= 3;
            Color medal = position == 1 ? GoldBadge : position == 2 ? SilverBadge : BronzeBadge;

            if (isSelf)
            {
                background.color = SelfBackground;
            }
            else if (podium)
            {
                background.color = position == 1 ? GoldBackground : position == 2 ? SilverBackground : BronzeBackground;
            }
            else
            {
                background.color = position % 2 == 0 ? AltBackground : NormalBackground;
            }

            // Medal badge behind the number for the podium; plain number otherwise.
            positionBadge.enabled = podium;
            positionBadge.color = medal;
            positionText.fontStyle = podium ? FontStyles.Bold : FontStyles.Normal;
            positionText.color = podium ? DarkText : (isSelf ? DarkText : LightText);

            // Own row inside the podium keeps the medal and gets a medal-coloured outline.
            highlight.enabled = podium || isSelf;
            highlight.effectColor = podium ? medal : new Color(0.85f, 0.62f, 0.12f, 1f);

            Color text = isSelf ? DarkText : LightText;
            usernameText.color = text;
            rankNameText.color = text;
            pointsText.color = text;
            winRateText.color = text;
            usernameText.fontStyle = isSelf || podium ? FontStyles.Bold : FontStyles.Normal;
        }

#if UNITY_EDITOR
        public void EditorWire(
            Image background,
            Outline highlight,
            Image positionBadge,
            TextMeshProUGUI positionText,
            TextMeshProUGUI usernameText,
            Image rankIcon,
            TextMeshProUGUI rankNameText,
            TextMeshProUGUI pointsText,
            TextMeshProUGUI winRateText
        )
        {
            this.background = background;
            this.highlight = highlight;
            this.positionBadge = positionBadge;
            this.positionText = positionText;
            this.usernameText = usernameText;
            this.rankIcon = rankIcon;
            this.rankNameText = rankNameText;
            this.pointsText = pointsText;
            this.winRateText = winRateText;
        }
#endif
    }
}
