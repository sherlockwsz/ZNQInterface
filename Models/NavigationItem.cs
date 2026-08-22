namespace ZNQInterface.Models
{
    /// <summary>页面导航项。</summary>
    public sealed class NavigationItem
    {
        public NavigationItem(string title, string navigationKey)
        {
            Title = title;
            NavigationKey = navigationKey;
        }

        // 导航显示标题。
        public string Title { get; }

        // Prism 导航键。
        public string NavigationKey { get; }
    }
}