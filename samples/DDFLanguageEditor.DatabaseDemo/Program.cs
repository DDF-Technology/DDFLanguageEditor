using System;
using System.Windows.Forms;

namespace DDFLanguageEditor.DatabaseDemo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new DatabaseDemoForm());
        }
    }
}
