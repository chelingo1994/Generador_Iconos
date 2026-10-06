using System;
using System.Windows.Forms;

namespace GeneradorIconos
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
#if NETCOREAPP
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }
}
