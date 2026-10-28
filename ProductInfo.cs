using DDFLanguageEditor.Core;

namespace DDF___Program_Language_Editor
{
    internal static class ProductInfo
    {
        public static string Version => DdfProductVersionCatalog.Current.CurrentVersion;
        public static string DisplayName => "DDFLanguageEditor " +
            DdfProductVersionCatalog.Current.DisplayVersion;
        public static string BuildDisplay => ProductBuildIdentity.Current.ShortDisplay;
        public static string BuildDetails => ProductBuildIdentity.Current.DetailDisplay;
    }
}
