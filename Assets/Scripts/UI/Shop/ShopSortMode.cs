namespace KLTN.UI.Shop
{
    public enum ShopSortMode
    {
        SilverAscending = 0,
        SilverDescending = 1,
        CoinsAscending = 2,
        CoinsDescending = 3,
        SilverOnly = 4,
        CoinsOnly = 5,
        NotOwnedOnly = 6,
    }

    public static class ShopSortModeLabels
    {
        public static readonly string[] All =
        {
            "Bạc tăng dần",
            "Bạc giảm dần",
            "Xu tăng dần",
            "Xu giảm dần",
            "Chỉ bạc",
            "Chỉ xu",
            "Chỉ thẻ chưa có",
        };
    }
}
