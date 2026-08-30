using System.Drawing;
using System.IO;
using System.Reflection;

namespace DDF___Program_Language_Editor
{
    internal static class AppIconProvider
    {
        public static Icon LoadIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DDFLanguageEditor.AppIcon.ico"))
            {
                if (stream == null) return null;
                using (var source = new Icon(stream)) return (Icon)source.Clone();
            }
        }

        public static Image LoadHighResolutionImage()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DDFLanguageEditor.AppIcon.png"))
            {
                if (stream == null) return null;
                using (Image source = Image.FromStream(stream)) return new Bitmap(source);
            }
        }
    }
}
