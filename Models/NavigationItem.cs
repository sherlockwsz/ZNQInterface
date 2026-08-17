namespace ZNQInterface.Models
{
    public sealed class NavigationItem
    {
        public NavigationItem(string title, string navigationKey)
        {
            Title = title;
            NavigationKey = navigationKey;
        }

        public string Title { get; }

        public string NavigationKey { get; }
    }
}