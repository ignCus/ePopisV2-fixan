using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Win32; // DODATO ZA REGISTRY
using AutoUpdaterDotNET;

namespace ePopisV2
{
    public partial class LoginFormcs : Form
    {
        private Label lblPocetniDepozit;
        public int logujemoSmenu;
        private string configPutanja = "";
        private string adminConfigPath = "";
        private static string ConfigFolderPath = "";
        private static string GlavniFolderPath = "";

        public class AdminConfig
        {
            public string TelegramChatId { get; set; }
            public string GlavniFolderPath { get; set; }
        }

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn
        (
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse
        );

        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        public LoginFormcs(int smena = 1)
        {
            InitializeComponent();
            AutoUpdater.Start("https://raw.githubusercontent.com/ignCus/ePopisV2-fixan/master/version.xml");
            UcitajAdminConfig();

            // Config folder je uvek unutar glavnog foldera
            if (!string.IsNullOrEmpty(GlavniFolderPath) && Directory.Exists(GlavniFolderPath))
            {
                ConfigFolderPath = Path.Combine(GlavniFolderPath, "Config");
            }
            else
            {
                // Ako nema glavnog foldera, pokušaj da ga kreiraš u AppData
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OktagonPopisi");
                if (!Directory.Exists(appDataFolder))
                {
                    Directory.CreateDirectory(appDataFolder);
                }
                GlavniFolderPath = appDataFolder;
                ConfigFolderPath = Path.Combine(appDataFolder, "Config");
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }
            }

            Form1.ConfigFolderPath = ConfigFolderPath;
            Form1.GlavniFolderPath = GlavniFolderPath;
            configPutanja = Path.Combine(ConfigFolderPath, "smena_config.txt");

            if (File.Exists(configPutanja))
            {
                try
                {
                    string sadrzaj = File.ReadAllText(configPutanja).Trim();
                    if (int.TryParse(sadrzaj, out int zapamcenaSmena) && (zapamcenaSmena == 1 || zapamcenaSmena == 2))
                    {
                        this.logujemoSmenu = zapamcenaSmena;
                    }
                    else
                    {
                        this.logujemoSmenu = smena;
                    }
                }
                catch
                {
                    this.logujemoSmenu = smena;
                }
            }
            else
            {
                this.logujemoSmenu = smena;
                SacuvajSmenuUFajl(smena);
            }

            SetupModerniDizajn();
        }

        // JAVNA STATICKA METODA ZA ČITANJE IZ POINTER FAJLA
        public static string CitajFolderIzPointerFajla()
        {
            string exeConfigFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ePopis",
                    "Config"
         );
            string pointerPath = Path.Combine(exeConfigFolder, "admin_config.json");

            if (File.Exists(pointerPath))
            {
                try
                {
                    string json = File.ReadAllText(pointerPath);
                    AdminConfig config = JsonSerializer.Deserialize<AdminConfig>(json);
                    if (config != null && !string.IsNullOrEmpty(config.GlavniFolderPath))
                    {
                        return config.GlavniFolderPath;
                    }
                }
                catch { }
            }

            return "";
        }

        private void SacuvajSmenuUFajl(int smena)
        {
            try
            {
                File.WriteAllText(configPutanja, smena.ToString());
            }
            catch { }
        }
        private void SetupModerniDizajn()
        {
            var kontroleZaBrisanje = this.Controls.Cast<Control>()
                .Where(c => c != username && c != password && c != btnLogin)
                .ToList();

            foreach (var c in kontroleZaBrisanje)
            {
                this.Controls.Remove(c);
            }

            this.Text = $"OKTAGON BET - Prijava (Smena {logujemoSmenu})";
            this.Size = new Size(420, 480);
            this.BackColor = Color.FromArgb(17, 24, 39);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 25, 25));

            Panel pnlTopBar = new Panel() { Size = new Size(this.Width, 40), Location = new Point(0, 0), BackColor = Color.FromArgb(24, 33, 47) };
            pnlTopBar.MouseDown += (s, e) => { dragging = true; dragCursorPoint = Cursor.Position; dragFormPoint = this.Location; };
            pnlTopBar.MouseMove += (s, e) => { if (dragging) { Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint)); this.Location = Point.Add(dragFormPoint, new Size(dif)); } };
            pnlTopBar.MouseUp += (s, e) => { dragging = false; };

            Label lblMaliNaslov = new Label() { Text = "OKTAGON BET - SISTEM PRIJAVE", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(156, 163, 175), Location = new Point(15, 12), Size = new Size(250, 20), BackColor = Color.Transparent };

            Button btnClose = new Button() { Text = "✕", Size = new Size(35, 30), Location = new Point(this.Width - 45, 5), FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, Font = new Font("Segoe UI", 10, FontStyle.Bold), BackColor = Color.Transparent };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.LightCoral;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.Gray;
            btnClose.Click += (s, e) => Application.Exit();

            pnlTopBar.Controls.Add(lblMaliNaslov);
            pnlTopBar.Controls.Add(btnClose);
            this.Controls.Add(pnlTopBar);

            Label lblNaslov = new Label() { Text = "DOBRODOŠLI", Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = Color.White, Location = new Point(30, 65), Size = new Size(360, 45), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
            Label lblPodnaslov = new Label() { Text = $"Prijava radnika za Smenu {logujemoSmenu}", Font = new Font("Segoe UI", 10, FontStyle.Regular), ForeColor = Color.FromArgb(10, 108, 255), Location = new Point(30, 110), Size = new Size(360, 25), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };

            // Pocetni depozit label (shows value from lokal_config.json if available)
            lblPocetniDepozit = new Label() { Text = "", Font = new Font("Segoe UI", 10, FontStyle.Regular), ForeColor = Color.FromArgb(156, 163, 175), Location = new Point(30, 140), Size = new Size(360, 22), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };

            Label lblUserTag = new Label() { Text = "KORISNIČKO IME", Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(156, 163, 175), Location = new Point(45, 160), Size = new Size(200, 20), BackColor = Color.Transparent };
            if (this.username != null)
            {
                this.username.Location = new Point(45, 180);
                this.username.Size = new Size(330, 35);
                this.username.Font = new Font("Segoe UI", 11);
                this.username.BackColor = Color.FromArgb(31, 41, 55);
                this.username.ForeColor = Color.White;
                this.username.BorderStyle = BorderStyle.FixedSingle;
                this.username.BringToFront();
            }

            Label lblPassTag = new Label() { Text = "LOZINKA", Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.FromArgb(156, 163, 175), Location = new Point(45, 240), Size = new Size(200, 20), BackColor = Color.Transparent };
            if (this.password != null)
            {
                this.password.Location = new Point(45, 260);
                this.password.Size = new Size(330, 35);
                this.password.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                this.password.BackColor = Color.FromArgb(31, 41, 55);
                this.password.ForeColor = Color.White;
                this.password.BorderStyle = BorderStyle.FixedSingle;
                this.password.PasswordChar = '•';
                this.password.BringToFront();
            }

            if (this.btnLogin != null)
            {
                this.btnLogin.Text = "ULOGUJ SE";
                this.btnLogin.Location = new Point(45, 345);
                this.btnLogin.Size = new Size(330, 50);
                this.btnLogin.Font = new Font("Segoe UI", 12, FontStyle.Bold);
                this.btnLogin.BackColor = Color.FromArgb(10, 108, 255);
                this.btnLogin.ForeColor = Color.White;
                this.btnLogin.FlatStyle = FlatStyle.Flat;
                this.btnLogin.FlatAppearance.BorderSize = 0;
                this.btnLogin.Cursor = Cursors.Hand;
                this.btnLogin.BringToFront();

                this.btnLogin.MouseEnter += (s, e) => this.btnLogin.BackColor = Color.FromArgb(0, 90, 230);
                this.btnLogin.MouseLeave += (s, e) => this.btnLogin.BackColor = Color.FromArgb(10, 108, 255);
            }

            Label lblFooter = new Label() { Text = "ePopis v2.0 • Sva prava zadržana", Font = new Font("Segoe UI", 8, FontStyle.Italic), ForeColor = Color.FromArgb(75, 85, 99), Location = new Point(30, 445), Size = new Size(360, 20), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };

            this.Controls.Add(lblNaslov);
            this.Controls.Add(lblPodnaslov);
            this.Controls.Add(lblPocetniDepozit);
            this.Controls.Add(lblUserTag);
            this.Controls.Add(lblPassTag);
            this.Controls.Add(lblFooter);

            this.AcceptButton = this.btnLogin;

            // Load and display pocetni depozit if available
            try
            {
                decimal poc = UcitajPocetniDepozitZaLogin();
                if (poc != 0)
                    lblPocetniDepozit.Text = $"Početni depozit smene: {poc.ToString("N0")} RSD";
                else
                    lblPocetniDepozit.Text = "";
            }
            catch { }
        }

        private decimal UcitajPocetniDepozitZaLogin()
        {
            try
            {
                // First try prenos_depozita file variants in the GlavniFolderPath/Config
                if (!string.IsNullOrEmpty(GlavniFolderPath))
                {
                    string cfg = Path.Combine(GlavniFolderPath, "Config");
                    if (Directory.Exists(cfg))
                    {
                        // common filenames
                        string[] tryNames = new[] { "prenos_depozita.txt", "prenos_depozit.txt", "prenos_depozita .txt", "prenos_depozita" };
                        foreach (var tn in tryNames)
                        {
                            var p = Path.Combine(cfg, tn);
                            if (File.Exists(p))
                            {
                                var v = ParseDecimalFromText(File.ReadAllText(p));
                                if (v.HasValue) return v.Value;
                            }
                        }

                        // fuzzy search files containing both 'prenos' and 'depozit'
                        try
                        {
                            foreach (var f in Directory.GetFiles(cfg))
                            {
                                var name = Path.GetFileName(f).ToLowerInvariant();
                                if (name.Contains("prenos") && name.Contains("depoz"))
                                {
                                    var v = ParseDecimalFromText(File.ReadAllText(f));
                                    if (v.HasValue) return v.Value;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // If not found, fallback to lokal_config.json values (Glavni folder then exe)
                if (!string.IsNullOrEmpty(GlavniFolderPath))
                {
                    string path = Path.Combine(GlavniFolderPath, "Config", "lokal_config.json");
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        using (var doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("PocetniDepozit", out var prop))
                            {
                                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out decimal val)) return val;
                                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out decimal sval)) return sval;
                            }
                        }
                    }
                }

                string exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ePopis", "Config", "lokal_config.json");
                if (File.Exists(exePath))
                {
                    string json = File.ReadAllText(exePath);
                    using (var doc = JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("PocetniDepozit", out var prop))
                        {
                            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out decimal val)) return val;
                            if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out decimal sval)) return sval;
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        private decimal? ParseDecimalFromText(string txt)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txt)) return null;
                var m = Regex.Match(txt, @"-?[0-9][0-9\s,\.]*");
                if (!m.Success) return null;
                var numStr = new string(m.Value.Where(c => char.IsDigit(c) || c == '-' || c == ',' || c == '.').ToArray());
                numStr = numStr.Replace(" ", "").Replace(",", "");
                if (decimal.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val)) return val;
            }
            catch { }
            return null;
        }

        private void UcitajAdminConfig()
        {
            string exeConfigFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ePopis",
                    "Config"
            );
            adminConfigPath = Path.Combine(exeConfigFolder, "admin_config.json");

            // ========== KORAK 1: Pokušaj iz pointer fajla ==========
            if (File.Exists(adminConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(adminConfigPath);
                    AdminConfig config = JsonSerializer.Deserialize<AdminConfig>(json);
                    if (config != null && !string.IsNullOrEmpty(config.GlavniFolderPath) && Directory.Exists(config.GlavniFolderPath))
                    {
                        GlavniFolderPath = config.GlavniFolderPath;
                        Form1.GlavniFolderPath = config.GlavniFolderPath;
                        Form1.TelegramChatId = config.TelegramChatId;

                        // Proveri da li postoji pravi config u glavnom folderu
                        string glavniConfigPath = Path.Combine(GlavniFolderPath, "Config", "admin_config.json");
                        if (!File.Exists(glavniConfigPath))
                        {
                            // Obnovi config u glavnom folderu
                            Directory.CreateDirectory(Path.Combine(GlavniFolderPath, "Config"));
                            File.WriteAllText(glavniConfigPath, json);
                        }
                        return;
                    }
                }
                catch { }
            }

            // ========== KORAK 2: Pokušaj iz Registry ==========
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\OktagonBet");
                if (key != null)
                {
                    string regFolder = key.GetValue("GlavniFolder") as string;
                    string regTelegram = key.GetValue("TelegramChatId") as string;

                    if (!string.IsNullOrEmpty(regFolder) && Directory.Exists(regFolder))
                    {
                        GlavniFolderPath = regFolder;
                        Form1.GlavniFolderPath = regFolder;
                        Form1.TelegramChatId = regTelegram ?? "";

                        // OBNOVI pointer fajl (exe/Config/admin_config.json)
                        if (!Directory.Exists(exeConfigFolder))
                            Directory.CreateDirectory(exeConfigFolder);

                        var config = new AdminConfig { GlavniFolderPath = regFolder, TelegramChatId = regTelegram ?? "" };
                        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(adminConfigPath, json);

                        // Proveri i obnovi config u glavnom folderu ako treba
                        string glavniConfigPath = Path.Combine(regFolder, "Config", "admin_config.json");
                        if (!File.Exists(glavniConfigPath))
                        {
                            Directory.CreateDirectory(Path.Combine(regFolder, "Config"));
                            File.WriteAllText(glavniConfigPath, json);
                        }

                        key.Close();
                        return;
                    }
                    key.Close();
                }
            }
            catch { }

            // ========== KORAK 3: Nema ničega - pitaj admina ==========
            DialogResult result = MessageBox.Show(
                "❌ Konfiguracija nije pronađena!\n\n" +
                "Ovo vam se dešava samo ako prvi put pokrećete aplikaciju.\n\n" +
                "Da li želite da otvorite Admin Panel i podesite folder?\n\n" +
                "• YES → Podesićete folder (preporučeno)\n" +
                "• NO → Koristiće se default folder (podaci će biti u AppData)\n" +
                "• CANCEL → Zatvori aplikaciju",
                "Prvo podešavanje",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                AdminPanelForm adminPanel = new AdminPanelForm();
                adminPanel.ShowDialog();
                UcitajAdminConfig(); // Pokušaj ponovo
                return;
            }
            else if (result == DialogResult.Cancel)
            {
                Application.Exit();
                Environment.Exit(0);
                return;
            }

            // ========== KORAK 4: Default folder u AppData ==========
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OktagonPopisi");
            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            string appDataConfigFolder = Path.Combine(appDataFolder, "Config");
            if (!Directory.Exists(appDataConfigFolder))
                Directory.CreateDirectory(appDataConfigFolder);

            GlavniFolderPath = appDataFolder;
            Form1.GlavniFolderPath = appDataFolder;
            Form1.TelegramChatId = "";

            MessageBox.Show(
                $"⚠️ Koristi se default folder:\n{appDataFolder}\n\n" +
                "Savet: Otvorite Admin Panel i podesite željeni folder za čuvanje podataka.",
                "Default folder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void btnLogin_Click_1(object sender, EventArgs e)
        {
            if (username.Text.Trim() == "admin" && password.Text.Trim() == "814613")
            {
                AdminPanelForm adminPanel = new AdminPanelForm();
                adminPanel.ShowDialog();

                // Dodaj ovo - pitaj za promenu lozinke
                DialogResult promeniLozinku = MessageBox.Show(
                    "Da li želite da promenite admin lozinku za zaštitu Config foldera?",
                    "Sigurnosna podešavanja",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (promeniLozinku == DialogResult.Yes)
                {
                    // Kreiraj privremenu Form1 samo za promenu lozinke
                    using (var tempForm = new Form1("", "", "", 1, ""))
                    {
                        tempForm.PromeniAdminLozinku();
                    }
                }

                // Ponovo učitaj konfiguraciju nakon što je admin panel sačuvao
                UcitajAdminConfig();

                // Osveži Config folder putanju
                if (!string.IsNullOrEmpty(GlavniFolderPath))
                {
                    ConfigFolderPath = Path.Combine(GlavniFolderPath, "Config");
                    if (!Directory.Exists(ConfigFolderPath))
                    {
                        Directory.CreateDirectory(ConfigFolderPath);
                    }
                    Form1.ConfigFolderPath = ConfigFolderPath;
                    configPutanja = Path.Combine(ConfigFolderPath, "smena_config.txt");
                }

                DialogResult result = MessageBox.Show("Da li želite da se prijavite kao radnik i započnete popis?",
                    "Nastavak", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    username.Clear();
                    password.Clear();
                    username.Focus();
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(username.Text) || string.IsNullOrWhiteSpace(password.Text))
            {
                MessageBox.Show("Molimo vas unesite i korisničko ime i lozinku!", "Upozorenje", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnLogin.Enabled = false;
            btnLogin.Text = "PROVERA PODATAKA...";

            string csvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vS9y0J0qwJm3sOuwIcwW1Zv7J30rmyXXEFXT7TyJJ8M6PnZVPZ_vqzDg3CthK2BgD67emjGiaNSiu1J/pub?output=csv";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string csvData = await client.GetStringAsync(csvUrl);
                    string[] redovi = csvData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                    bool nadjen = false;
                    bool proceedDespiteShortage = false;
                    bool suppressLoginError = false;

                    for (int i = 1; i < redovi.Length; i++)
                    {
                        string[] kolone = redovi[i].Split(',');

                        if (kolone.Length < 5) continue;

                        string dbUser = kolone[0].Trim();
                        string dbPass = kolone[1].Trim();
                        string dbMesto = kolone[2].Trim();
                        string dbKod = kolone[3].Trim();
                        string dbPravoIme = kolone[4].Trim();

                        if (username.Text.Trim() == dbUser && password.Text.Trim() == dbPass)
                        {
                            nadjen = true;

                            // Ne menjamo zapamćenu smenu ovde — smena će biti promenjena
                            // tek kada radnik eksplicitno klikne "Završi Smenu".
                            int sledecaSmena = (this.logujemoSmenu == 1) ? 2 : 1;

                            // Pre nego što otvorimo glavnu formu, pitaj operatera da potvrdi stanje kase
                            decimal expectedPrenos = 0;
                            try
                            {
                                expectedPrenos = UcitajPocetniDepozitZaLogin();
                            }
                            catch { expectedPrenos = 0; }

                            string msg = expectedPrenos != 0
                                ? $"Proverite da li vam je stanje kase {expectedPrenos:N0} RSD.\n\nUkoliko jeste, kliknite YES da nastavite, ukoliko nije NO da se vratite.":
                                  "Proverite stanje kase pre otvaranja smene.\n\nAko želite da nastavite, kliknite YES.";

                            if (MessageBox.Show(msg, "Provera stanja kase", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                Form1 glavna = new Form1(dbUser, dbMesto, dbKod, this.logujemoSmenu, dbPravoIme);
                                glavna.Show();
                                this.Hide();
                                break;
                            }
                            else
                            {
                                // ask to report shortage to manager
                                var report = MessageBox.Show("Prijavite poslovodji da imate manjak u kasi. \nUkoliko je poslovodja obavesten, kliknite YES da nastavite, ukoliko nije NO da se vratite.", "Obaveštenje", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                                if (report == DialogResult.Yes)
                                {
                                    // allow login despite shortage
                                    Form1 glavna = new Form1(dbUser, dbMesto, dbKod, this.logujemoSmenu, dbPravoIme);
                                    glavna.Show();
                                    this.Hide();
                                    proceedDespiteShortage = true;
                                    break;
                                }
                                else
                                {
                                    // operator did not report; return to login without showing "wrong credentials"
                                    nadjen = false;
                                    suppressLoginError = true;
                                    username.Clear(); password.Clear();
                                    username.Focus();
                                    break;
                                }
                            }
                        }
                    }

                    if (!nadjen && !proceedDespiteShortage && !suppressLoginError)
                    {
                        MessageBox.Show("Pogrešni podaci ili niste uneti u bazu!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Greška pri povezivanju sa serverom:\n" + ex.Message, "Sistemska Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnLogin.Enabled = true;
                    btnLogin.Text = "ULOGUJ SE";
                }
            }
        }
    }

    // ============================================================
    // ADMIN PANEL FORM
    // ============================================================
    public class AdminPanelForm : Form
    {
        private TextBox txtLokacija;
        private TextBox txtKodLokacije;
        private TextBox txtPocetniDepozit;
        private TextBox txtUtvrdjeniDepozit;
        private TextBox txtTelegramId;
        private TextBox txtGlavniFolder;
        private Button btnSacuvaj;
        private Button btnOdustani;
        private Button btnIzaberiFolder;
        private string exeConfigFolderPath;
        private string adminConfigPath;

        public class LokalConfig
        {
            public string NazivLokacije { get; set; }
            public string KodLokacije { get; set; }
            public decimal PocetniDepozit { get; set; }
            public decimal UtvrdjeniDepozit { get; set; }
        }

        public class AdminConfig
        {
            public string TelegramChatId { get; set; }
            public string GlavniFolderPath { get; set; }
        }



        public AdminPanelForm()
        {
            exeConfigFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ePopis", "Config");
            if (!Directory.Exists(exeConfigFolderPath))
            {
                Directory.CreateDirectory(exeConfigFolderPath);
            }

            adminConfigPath = Path.Combine(exeConfigFolderPath, "admin_config.json");

            InitializeComponent();
            UcitajPostojecePodatke();
        }

        private void InitializeComponent()
        {
            this.Text = "Admin Panel - Podešavanje Lokala";
            this.Size = new Size(500, 520);
            this.BackColor = Color.FromArgb(17, 24, 39);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lblNaslov = new Label()
            {
                Text = "ADMIN PANEL - POSTAVKE LOKALA",
                Location = new Point(20, 20),
                Size = new Size(460, 30),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblLokacija = new Label()
            {
                Text = "Naziv Lokacije:",
                Location = new Point(30, 70),
                Size = new Size(120, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };

            txtLokacija = new TextBox()
            {
                Location = new Point(160, 68),
                Size = new Size(300, 28),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };

            Label lblKodLokacije = new Label()
            {
                Text = "Kod Lokacije:",
                Location = new Point(30, 110),
                Size = new Size(120, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };

            txtKodLokacije = new TextBox()
            {
                Location = new Point(160, 108),
                Size = new Size(300, 28),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };

            Label lblPocetniDepozit = new Label()
            {
                Text = "Početni Depozit (RSD):",
                Location = new Point(30, 150),
                Size = new Size(130, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };

            txtPocetniDepozit = new TextBox()
            {
                Location = new Point(160, 148),
                Size = new Size(300, 28),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };

            Label lblUtvrdjeniDepozit = new Label()
            {
                Text = "Utvrdjeni Depozit (RSD):",
                Location = new Point(30, 185),
                Size = new Size(130, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };

            txtUtvrdjeniDepozit = new TextBox()
            {
                Location = new Point(160, 183),
                Size = new Size(300, 28),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };

            Label lblSeparator = new Label()
            {
                Text = "══════════════════════════════════════════════════",
                Location = new Point(20, 195),
                Size = new Size(460, 20),
                ForeColor = Color.FromArgb(75, 85, 99),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblAdminNaslov = new Label()
            {
                Text = "GLOBALNE POSTAVKE",
                Location = new Point(20, 220),
                Size = new Size(460, 25),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(10, 108, 255),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblTelegramId = new Label()
            {
                Text = "Telegram Chat ID:",
                Location = new Point(30, 260),
                Size = new Size(120, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };

            txtTelegramId = new TextBox()
            {
                Location = new Point(160, 258),
                Size = new Size(300, 28),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "Unesite Telegram Chat ID (npr. 6514727840)"
            };

            Label lblGlavniFolder = new Label()
            {
                Text = "Glavni Folder za Popise:",
                Location = new Point(30, 310),
                Size = new Size(130, 28),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };

            txtGlavniFolder = new TextBox()
            {
                Location = new Point(160, 308),
                Size = new Size(220, 28),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                ReadOnly = true
            };

            btnIzaberiFolder = new Button()
            {
                Text = "📁 Izaberi",
                Location = new Point(390, 306),
                Size = new Size(70, 32),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnIzaberiFolder.FlatAppearance.BorderSize = 0;
            btnIzaberiFolder.Click += BtnIzaberiFolder_Click;

            btnSacuvaj = new Button()
            {
                Text = "SAČUVAJ",
                Location = new Point(120, 370),
                Size = new Size(110, 45),
                BackColor = Color.FromArgb(10, 108, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btnSacuvaj.FlatAppearance.BorderSize = 0;
            btnSacuvaj.Click += BtnSacuvaj_Click;

            btnOdustani = new Button()
            {
                Text = "ODUSTANI",
                Location = new Point(270, 370),
                Size = new Size(110, 45),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btnOdustani.FlatAppearance.BorderSize = 0;
            btnOdustani.Click += (s, e) => this.Close();

            this.Controls.Add(lblNaslov);
            this.Controls.Add(lblLokacija);
            this.Controls.Add(txtLokacija);
            this.Controls.Add(lblKodLokacije);
            this.Controls.Add(txtKodLokacije);
            this.Controls.Add(lblPocetniDepozit);
            this.Controls.Add(txtPocetniDepozit);
            this.Controls.Add(lblUtvrdjeniDepozit);
            this.Controls.Add(txtUtvrdjeniDepozit);
            this.Controls.Add(lblSeparator);
            this.Controls.Add(lblAdminNaslov);
            this.Controls.Add(lblTelegramId);
            this.Controls.Add(txtTelegramId);
            this.Controls.Add(lblGlavniFolder);
            this.Controls.Add(txtGlavniFolder);
            this.Controls.Add(btnIzaberiFolder);
            this.Controls.Add(btnSacuvaj);
            this.Controls.Add(btnOdustani);
        }

        private void UcitajPostojecePodatke()
        {
            // Prvo učitaj iz pointer fajla ako postoji
            if (File.Exists(adminConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(adminConfigPath);
                    AdminConfig config = JsonSerializer.Deserialize<AdminConfig>(json);
                    if (config != null)
                    {
                        if (!string.IsNullOrEmpty(config.TelegramChatId))
                            txtTelegramId.Text = config.TelegramChatId;
                        if (!string.IsNullOrEmpty(config.GlavniFolderPath))
                            txtGlavniFolder.Text = config.GlavniFolderPath;
                    }
                }
                catch { }
            }

            // Zatim učitaj lokal_config iz glavnog foldera ako postoji
            if (!string.IsNullOrEmpty(txtGlavniFolder.Text) && Directory.Exists(txtGlavniFolder.Text))
            {
                string lokalConfigPath = Path.Combine(txtGlavniFolder.Text, "Config", "lokal_config.json");
                if (File.Exists(lokalConfigPath))
                {
                    try
                    {
                        string json = File.ReadAllText(lokalConfigPath);
                        LokalConfig config = JsonSerializer.Deserialize<LokalConfig>(json);
                        if (config != null)
                        {
                            txtLokacija.Text = config.NazivLokacije ?? "";
                            txtKodLokacije.Text = config.KodLokacije ?? "";
                            txtPocetniDepozit.Text = config.PocetniDepozit.ToString("N0");
                            txtUtvrdjeniDepozit.Text = config.UtvrdjeniDepozit.ToString("N0");
                        }
                    }
                    catch { }
                }
            }
        }

        private void BtnIzaberiFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Izaberite glavni folder za čuvanje popisa";
                fbd.ShowNewFolderButton = true;

                if (!string.IsNullOrWhiteSpace(txtGlavniFolder.Text) && Directory.Exists(txtGlavniFolder.Text))
                {
                    fbd.SelectedPath = txtGlavniFolder.Text;
                }

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtGlavniFolder.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnSacuvaj_Click(object sender, EventArgs e)
        {
            // --- Validacije ---
            if (string.IsNullOrWhiteSpace(txtLokacija.Text))
            {
                MessageBox.Show("Molimo unesite naziv lokacije!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtKodLokacije.Text))
            {
                MessageBox.Show("Molimo unesite kod lokacije!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Parsiranje depozita
            string depozitTekst = txtPocetniDepozit.Text
                .Replace(".", "")
                .Replace(",", "")
                .Trim();

            if (!decimal.TryParse(depozitTekst, out decimal pocetniDepozit))
            {
                MessageBox.Show("Molimo unesite ispravan iznos početnog depozita!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtTelegramId.Text))
            {
                MessageBox.Show("Molimo unesite Telegram Chat ID!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtGlavniFolder.Text))
            {
                MessageBox.Show("Molimo izaberite glavni folder za čuvanje popisa!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string mainFolder = txtGlavniFolder.Text.Trim();

            // Kreiraj glavni folder ako ne postoji
            if (!Directory.Exists(mainFolder))
            {
                try
                {
                    Directory.CreateDirectory(mainFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nemoguće kreirati folder: {ex.Message}", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // KREIRAJ JEDINI CONFIG FOLDER - unutar glavnog foldera
            string jediniConfigFolder = Path.Combine(mainFolder, "Config");
            if (!Directory.Exists(jediniConfigFolder))
            {
                try
                {
                    Directory.CreateDirectory(jediniConfigFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nemoguće kreirati Config folder: {ex.Message}", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // --- Pripremi objekte ---
            LokalConfig lokalConfig = new LokalConfig
            {
                NazivLokacije = txtLokacija.Text.Trim(),
                KodLokacije = txtKodLokacije.Text.Trim(),
                PocetniDepozit = pocetniDepozit,
                UtvrdjeniDepozit = 0
            };

            AdminConfig adminConfig = new AdminConfig
            {
                TelegramChatId = txtTelegramId.Text.Trim(),
                GlavniFolderPath = mainFolder
            };

            try
            {
                // 1. Sačuvaj lokal_config.json u JEDINI Config folder (unutar glavnog)
                // If admin provided utvrdjeni depozit parse and set it
                if (!string.IsNullOrWhiteSpace(txtUtvrdjeniDepozit.Text))
                {
                    string t = txtUtvrdjeniDepozit.Text.Replace(".", "").Replace(",", "").Trim();
                    if (decimal.TryParse(t, out decimal udep))
                        lokalConfig.UtvrdjeniDepozit = udep;
                }

                // Serialize with case-insensitive properties to match reader
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(Path.Combine(jediniConfigFolder, "lokal_config.json"),
                    JsonSerializer.Serialize(lokalConfig, opts));

                // 2. Sačuvaj admin_config.json u JEDINI Config folder (unutar glavnog)
                File.WriteAllText(Path.Combine(jediniConfigFolder, "admin_config.json"),
                    JsonSerializer.Serialize(adminConfig, new JsonSerializerOptions { WriteIndented = true }));

                // 3. Sačuvaj SAMO admin_config.json u exe/Config (pointer)
                File.WriteAllText(Path.Combine(exeConfigFolderPath, "admin_config.json"),
                    JsonSerializer.Serialize(adminConfig, new JsonSerializerOptions { WriteIndented = true }));

                // 4. Sačuvaj u Registry kao backup
                try
                {
                    RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\OktagonBet");
                    key.SetValue("GlavniFolder", mainFolder);
                    key.SetValue("TelegramChatId", txtTelegramId.Text.Trim());
                    key.Close();
                }
                catch { }

                // 5. Ažuriraj statičke promenljive
                Form1.GlavniFolderPath = mainFolder;
                Form1.TelegramChatId = adminConfig.TelegramChatId;
                Form1.ConfigFolderPath = jediniConfigFolder;

                MessageBox.Show(
                    $"✅ Podaci su uspešno sačuvani!\n\n" +
                    $"📁 Glavni folder: {mainFolder}\n" +
                    $"⚙️ Config folder: {jediniConfigFolder}\n" +
                    $"💾 Registry backup: Sačuvano\n\n" +
                    $"📌 SVI podaci se čuvaju na ovoj lokaciji i neće biti izgubljeni prilikom update-a aplikacije.",
                    "Uspešno", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Greška pri čuvanju: {ex.Message}", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}