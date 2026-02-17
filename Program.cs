using System;
using System.Windows.Forms;

namespace DiskBurner
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new DiskBurnerForm());
        }
    }
}
