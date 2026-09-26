using System;
using System.Windows.Forms;

namespace DDFLanguageEditor.HostDemo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new DdfHostDemoForm());
        }
    }
}
