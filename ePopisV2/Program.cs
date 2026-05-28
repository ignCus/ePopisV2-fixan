using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ePopisV2
{
    static class Program
    {
        // Uvoz funkcije iz user32.dll koja popravlja mutne fontove na visokim rezolucijama
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            // Prvo palimo DPI awareness da aplikacija bude kristalno jasna
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            int pocetnaSmena = 1;
            string configPutanja = "smena_config.txt";

            // Ako fajl već postoji na disku, pročitaj iz njega koja je smena sledeća na redu
            if (File.Exists(configPutanja))
            {
                try
                {
                    string sadrzaj = File.ReadAllText(configPutanja).Trim();
                    if (int.TryParse(sadrzaj, out int zapamcenaSmena) && (zapamcenaSmena == 1 || zapamcenaSmena == 2))
                    {
                        pocetnaSmena = zapamcenaSmena;
                    }
                }
                catch { }
            }

            // Pokrećemo login formu sa ispravnom, zapamćenom smenom
            Application.Run(new LoginFormcs(pocetnaSmena));

            // Ensure the process fully terminates when the message loop ends
            // (prevents the app from lingering in Task Manager if background threads are alive)
            Environment.Exit(0);
        }
    }
}