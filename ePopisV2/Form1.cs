using System;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Win32;

namespace ePopisV2
{
    public partial class Form1 : Form
    {
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

        // STATICKE PROMENLJIVE KOJE SE PUNE IZ ADMIN PANELA
        public static string GlavniFolderPath = "";
        public static string TelegramChatId = "";
        public static string ConfigFolderPath = "";
        private static string adminLozinka = "admin123";
        private static string adminLozinkaPath = "";

        public int trenutnaSmena;
        private string urlLokacije = "https://docs.google.com/spreadsheets/d/e/2PACX-1vSO2iQzKUpGZPB9Y_PgHNeqD1wyL3wMn1mvsN-ZBnchR96p6CU3f0LHoML03KF5oockZwpDk8T_VvOa/pub?output=csv";

        private string telegramToken = "8993026912:AAE1bECC3oliaO1LCRDWu09XWt9rhA7X32U";
        private string telegramChatId = "";

        private bool dopunaOdobrena = false;
        private bool podizanjeOdobreno = false;
        private bool inkasacijaOdobrena = false;

        private Button btnDopunaAuth;
        private Button btnPodizanjeAuth;
        private Button btnTroskoviAuth;
        private Button btnInkasacijaAuth;

        private List<Trosak> listaTroskova = new List<Trosak>();
        private decimal ukupniTroskovi = 0;

        private decimal bazaKazino = 0, bazaKladionica = 0, bazaLbet = 0, bazaSank = 0;
        private string debugLogPath = "";

        private decimal originalKazino = 0, originalKladionica = 0, originalLbet = 0, originalSank = 0;

        private TextBox txtBrojGostijuAparati = new TextBox();
        private TextBox txtBrojGostijuKladionica = new TextBox();
        private TextBox txtBrojGostijuOnlineDepozit = new TextBox();
        private TextBox txtUkupnoGostiju = new TextBox();

        private string lokalConfigPath = "";
        private string fiksniKodLokacije = "";
        private string fiksniNazivLokacije = "";
        private decimal fiksniPocetniDepozit = 0;

        public class LokalConfig
        {
            public string NazivLokacije { get; set; }
            public string KodLokacije { get; set; }
            public decimal PocetniDepozit { get; set; }
        }

        private class Trosak
        {
            public decimal Iznos { get; set; }
            public string Opis { get; set; }
            public DateTime Vreme { get; set; }
            public override string ToString()
            {
                return $"{Iznos:N0} RSD - {Opis} ({Vreme:HH:mm})";
            }
        }

        public Form1(string radnik, string mesto, string sifra, int smena, string ime_prezime)
        {
            InitializeComponent();

            telegramChatId = TelegramChatId;

            // PROVERA: Da li postoji glavni folder i Config folder
            if (string.IsNullOrEmpty(GlavniFolderPath) || !Directory.Exists(GlavniFolderPath))
            {
                // Pokušaj da pročitaš iz pointer fajla
                string pointerFolder = LoginFormcs.CitajFolderIzPointerFajla();
                if (!string.IsNullOrEmpty(pointerFolder) && Directory.Exists(pointerFolder))
                {
                    GlavniFolderPath = pointerFolder;
                }
                else
                {
                    MessageBox.Show("Greška: Nije pronađen glavni folder za podatke!\n\nMolimo pokrenite Admin Panel (admin/123) i podesite ispravan folder.",
                        "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }
            }

            // ConfigFolderPath je uvek GlavniFolder/Config - JEDINI CONFIG FOLDER
            ConfigFolderPath = Path.Combine(GlavniFolderPath, "Config");

            // Proveri da li Config folder postoji
            if (!Directory.Exists(ConfigFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Greška: Nemoguće kreirati Config folder!\n{ConfigFolderPath}\n\n{ex.Message}",
                        "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }
            }

            lokalConfigPath = Path.Combine(ConfigFolderPath, "lokal_config.json");
            debugLogPath = Path.Combine(ConfigFolderPath, "debug_log.txt");
            adminLozinkaPath = Path.Combine(ConfigFolderPath, "admin_lozinka.txt");
            UcitajAdminLozinku();
            ZakljucajConfigFolder();

            this.Text = "OKTAGON BET BackOffice";
            this.BackColor = Color.FromArgb(17, 24, 39);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1480, 840);

            UcitajFiksnePodatkeLokala();

            txtBrojGostijuAparati.TextChanged += (s, e) => IzracunajUkupnoGostiju();
            txtBrojGostijuKladionica.TextChanged += (s, e) => IzracunajUkupnoGostiju();
            txtBrojGostijuOnlineDepozit.TextChanged += (s, e) => IzracunajUkupnoGostiju();

            SetupFormLayout();
            SetupFinansijePanel();
            SetupDepozitiPanel();
            SetupOperativaPanel();
            SetupBottomSection();
            ApplyRoundedCorners();

            if (File.Exists(debugLogPath))
                File.Delete(debugLogPath);

            WriteDebug("========== APLIKACIJA POKRENUTA ==========");
            WriteDebug($"Vreme: {DateTime.Now}");
            WriteDebug($"Smena: {smena}");
            WriteDebug($"Radnik: {ime_prezime}");
            WriteDebug($"Glavni folder: {GlavniFolderPath}");
            WriteDebug($"Config folder: {ConfigFolderPath}");
            WriteDebug($"Telegram Chat ID: {telegramChatId}");

            this.trenutnaSmena = smena;
            smena1.Text = ime_prezime;

            if (!string.IsNullOrEmpty(fiksniNazivLokacije))
                lokacija.Text = fiksniNazivLokacije;
            else
                lokacija.Text = mesto;

            if (!string.IsNullOrEmpty(fiksniKodLokacije))
                kodlokacije.Text = fiksniKodLokacije;
            else
                kodlokacije.Text = sifra;

            btnZavrsiSmenu.Text = (trenutnaSmena == 1) ? "Završi Smenu" : "Završi Popis";

            string prenosDepozitaPath = Path.Combine(ConfigFolderPath, "prenos_depozita.txt");
            if (File.Exists(prenosDepozitaPath))
            {
                string preneto = File.ReadAllText(prenosDepozitaPath);
                if (!string.IsNullOrEmpty(preneto))
                {
                    string cistBroj = new string(preneto.Where(c => char.IsDigit(c)).ToArray());
                    if (decimal.TryParse(cistBroj, out decimal pocetniBroj))
                    {
                        depozit1.Text = pocetniBroj.ToString("N0");
                        WriteDebug($"Učitano stanje iz fajla: {pocetniBroj} RSD");
                    }
                }
            }
            else
            {
                // Ako fajl ne postoji, pokušaj iz Registry
                WriteDebug("Fajl prenos_depozita.txt ne postoji, pokušavam Registry...");
                UcitajStanjeIzRegistry();
            }

            if (trenutnaSmena == 1 && string.IsNullOrWhiteSpace(depozit1.Text) && fiksniPocetniDepozit > 0)
            {
                depozit1.Text = fiksniPocetniDepozit.ToString("N0");
                WriteDebug($"Korišćen fiksni početni depozit: {fiksniPocetniDepozit} RSD");
            }

            if (trenutnaSmena == 2)
            {
                UcitajBazuIzPrveSmene();
            }

            UcitajIzTempFajla();
            NamestiDogadjaje();
            _ = UcitajUtvrdjeniDepozitSaWeba(sifra);
            IzracunajSve();

            ProveriITretirajPrethodnuAutorizaciju();
        }

        private void SacuvajStanjeURegistry()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\OktagonBet");
                if (key != null)
                {
                    key.SetValue("ZadnjeStanjeKase", GetValue(stanjedepnakraju).ToString());
                    key.SetValue("ZadnjiDepozit", depozit1.Text);
                    key.SetValue("ZadnjiDatum", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    key.Close();
                    WriteDebug($"Stanje sačuvano u Registry: {GetValue(stanjedepnakraju)} RSD");
                }
            }
            catch (Exception ex)
            {
                WriteDebug($"Greška pri čuvanju stanja u Registry: {ex.Message}");
            }
        }

        private void UcitajStanjeIzRegistry()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\OktagonBet");
                if (key != null)
                {
                    string zadnjeStanje = key.GetValue("ZadnjeStanjeKase") as string;
                    string zadnjiDepozit = key.GetValue("ZadnjiDepozit") as string;
                    string zadnjiDatum = key.GetValue("ZadnjiDatum") as string;

                    if (!string.IsNullOrEmpty(zadnjeStanje) && decimal.TryParse(zadnjeStanje, out decimal stanje))
                    {
                        stanjedepnakraju.Text = FormatujBroj(stanje);
                        if (!string.IsNullOrEmpty(zadnjiDepozit))
                            depozit1.Text = zadnjiDepozit;

                        WriteDebug($"Stanje učitano iz Registry (backup): {stanje} RSD, datum: {zadnjiDatum}");

                        // Obnovi fajl
                        string prenosPath = Path.Combine(ConfigFolderPath, "prenos_depozita.txt");
                        File.WriteAllText(prenosPath, stanje.ToString());
                        WriteDebug($"Obnovljen fajl prenos_depozita.txt: {stanje} RSD");
                    }
                    else
                    {
                        WriteDebug("Nema validnog stanja u Registry");
                    }
                    key.Close();
                }
                else
                {
                    WriteDebug("Registry ključ ne postoji");
                }
            }
            catch (Exception ex)
            {
                WriteDebug($"Greška pri učitavanju stanja iz Registry: {ex.Message}");
            }
        }

        private string GetMonthlyFolderPath()
        {
            DateTime datum = dateTimePicker1.Value;
            string mesecGodina = datum.ToString("yyyy_MM");
            string mesecNaziv = datum.ToString("MMMM");

            string monthlyFolder = Path.Combine(GlavniFolderPath, mesecGodina + "_" + mesecNaziv);

            if (!Directory.Exists(monthlyFolder))
            {
                Directory.CreateDirectory(monthlyFolder);
            }

            return monthlyFolder;
        }

        private void UcitajFiksnePodatkeLokala()
        {
            if (File.Exists(lokalConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(lokalConfigPath);
                    var config = JsonSerializer.Deserialize<LokalConfig>(json);
                    if (config != null)
                    {
                        fiksniKodLokacije = config.KodLokacije;
                        fiksniNazivLokacije = config.NazivLokacije;
                        fiksniPocetniDepozit = config.PocetniDepozit;
                    }
                }
                catch { }
            }
        }

        private void IzracunajUkupnoGostiju()
        {
            int aparati = GetIntValue(txtBrojGostijuAparati);
            int kladionica = GetIntValue(txtBrojGostijuKladionica);
            int online = GetIntValue(txtBrojGostijuOnlineDepozit);
            txtUkupnoGostiju.Text = (aparati + kladionica + online).ToString();
        }

        private void SetupFormLayout()
        {
            pictureBox1.Location = new Point(20, 10);
            pictureBox1.Size = new Size(50, 50);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            topPanel.Controls.Add(pictureBox1);
            topPanel.Height = 70;

            titleLabel.Location = new Point(80, 15);
            titleLabel.Font = new Font("Segoe UI Semibold", 16, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;

            dateTimePicker1.Location = new Point(1120, 18);
            dateTimePicker1.Size = new Size(260, 30);
            dateTimePicker1.ForeColor = Color.Black;
            topPanel.Controls.Add(dateTimePicker1);

            finansijePanel.Location = new Point(20, 85);
            finansijePanel.Size = new Size(440, 330);

            depozitiPanel.Location = new Point(480, 85);
            depozitiPanel.Size = new Size(440, 330);

            operativaPanel.Location = new Point(940, 85);
            operativaPanel.Size = new Size(500, 390);
        }

        private void SetupFinansijePanel()
        {
            int startY = 50, spacing = 65, labelWidth = 135, textBoxWidth = 210, textBoxX = 155;

            label10.Text = "Kazino:"; label10.ForeColor = Color.White; label10.Location = new Point(15, startY); label10.Size = new Size(labelWidth, 28);
            kazino.Location = new Point(textBoxX, startY); kazino.Size = new Size(textBoxWidth, 28); kazino.BackColor = Color.FromArgb(55, 65, 81); kazino.ForeColor = Color.White; kazino.BorderStyle = BorderStyle.FixedSingle;

            label12.Text = "Kladionica:"; label12.Location = new Point(15, startY + spacing); label12.Size = new Size(labelWidth, 28);
            kladionica.Location = new Point(textBoxX, startY + spacing); kladionica.Size = new Size(textBoxWidth, 28); kladionica.BackColor = Color.FromArgb(55, 65, 81); kladionica.ForeColor = Color.White; kladionica.BorderStyle = BorderStyle.FixedSingle;

            label14.Text = "L-Bet Saldo:"; label14.Location = new Point(15, startY + spacing * 2); label14.Size = new Size(labelWidth, 28);
            lbet.Location = new Point(textBoxX, startY + spacing * 2); lbet.Size = new Size(textBoxWidth, 28); lbet.BackColor = Color.FromArgb(55, 65, 81); lbet.ForeColor = Color.White; lbet.BorderStyle = BorderStyle.FixedSingle;

            label16.Text = "Sank:"; label16.Location = new Point(15, startY + spacing * 3); label16.Size = new Size(labelWidth, 28);
            sank.Location = new Point(textBoxX, startY + spacing * 3); sank.Size = new Size(textBoxWidth, 28); sank.BackColor = Color.FromArgb(55, 65, 81); sank.ForeColor = Color.White; sank.BorderStyle = BorderStyle.FixedSingle;

            finansijePanel.Controls.Add(label10); finansijePanel.Controls.Add(kazino);
            finansijePanel.Controls.Add(label12); finansijePanel.Controls.Add(kladionica);
            finansijePanel.Controls.Add(label14); finansijePanel.Controls.Add(lbet);
            finansijePanel.Controls.Add(label16); finansijePanel.Controls.Add(sank);
        }

        private void SetupDepozitiPanel()
        {
            int startY = 50, spacing = 65, labelWidth = 145, textBoxWidth = 170, textBoxX = 165, buttonX = 345, buttonWidth = 40;

            label17.Text = "Dop. Depozita:"; label17.Location = new Point(15, startY); label17.Size = new Size(labelWidth, 28);
            dopuna.Location = new Point(textBoxX, startY); dopuna.Size = new Size(textBoxWidth, 28); dopuna.BackColor = Color.FromArgb(55, 65, 81); dopuna.ForeColor = Color.White; dopuna.BorderStyle = BorderStyle.FixedSingle;

            btnDopunaAuth = new Button(); btnDopunaAuth.Text = "🔑"; btnDopunaAuth.Location = new Point(buttonX, startY); btnDopunaAuth.Size = new Size(buttonWidth, 28); btnDopunaAuth.BackColor = Color.FromArgb(10, 108, 255); btnDopunaAuth.ForeColor = Color.White; btnDopunaAuth.FlatStyle = FlatStyle.Flat; btnDopunaAuth.FlatAppearance.BorderSize = 0; btnDopunaAuth.Click += (s, e) => { PokreniAutorizaciju("Dopuna"); };

            label15.Text = "Pod. Depozita:"; label15.Location = new Point(15, startY + spacing); label15.Size = new Size(labelWidth, 28);
            podizanje.Location = new Point(textBoxX, startY + spacing); podizanje.Size = new Size(textBoxWidth, 28); podizanje.BackColor = Color.FromArgb(55, 65, 81); podizanje.ForeColor = Color.White; podizanje.BorderStyle = BorderStyle.FixedSingle;

            btnPodizanjeAuth = new Button(); btnPodizanjeAuth.Text = "🔑"; btnPodizanjeAuth.Location = new Point(buttonX, startY + spacing); btnPodizanjeAuth.Size = new Size(buttonWidth, 28); btnPodizanjeAuth.BackColor = Color.FromArgb(10, 108, 255); btnPodizanjeAuth.ForeColor = Color.White; btnPodizanjeAuth.FlatStyle = FlatStyle.Flat; btnPodizanjeAuth.FlatAppearance.BorderSize = 0; btnPodizanjeAuth.Click += (s, e) => { PokreniAutorizaciju("Podizanje"); };

            label13.Text = "Troskovi:"; label13.Location = new Point(15, startY + spacing * 2); label13.Size = new Size(labelWidth, 28);
            troskovi.Location = new Point(textBoxX, startY + spacing * 2); troskovi.Size = new Size(textBoxWidth, 28); troskovi.BackColor = Color.FromArgb(55, 65, 81); troskovi.ForeColor = Color.White; troskovi.BorderStyle = BorderStyle.FixedSingle;

            btnTroskoviAuth = new Button(); btnTroskoviAuth.Text = "🔑"; btnTroskoviAuth.Location = new Point(buttonX, startY + spacing * 2); btnTroskoviAuth.Size = new Size(buttonWidth, 28); btnTroskoviAuth.BackColor = Color.FromArgb(10, 108, 255); btnTroskoviAuth.ForeColor = Color.White; btnTroskoviAuth.FlatStyle = FlatStyle.Flat; btnTroskoviAuth.FlatAppearance.BorderSize = 0; btnTroskoviAuth.Click += (s, e) => { PokreniAutorizacijuSaOpisom(); };

            label7.Text = "Pocetni depozit:"; label7.Location = new Point(15, startY + spacing * 3); label7.Size = new Size(labelWidth, 28);
            depozit1.Location = new Point(textBoxX, startY + spacing * 3); depozit1.Size = new Size(textBoxWidth, 28); depozit1.BackColor = Color.FromArgb(55, 65, 81); depozit1.ForeColor = Color.White; depozit1.BorderStyle = BorderStyle.FixedSingle;

            depozitiPanel.Controls.Add(label17); depozitiPanel.Controls.Add(dopuna); depozitiPanel.Controls.Add(btnDopunaAuth);
            depozitiPanel.Controls.Add(label15); depozitiPanel.Controls.Add(podizanje); depozitiPanel.Controls.Add(btnPodizanjeAuth);
            depozitiPanel.Controls.Add(label13); depozitiPanel.Controls.Add(troskovi); depozitiPanel.Controls.Add(btnTroskoviAuth);
            depozitiPanel.Controls.Add(label7); depozitiPanel.Controls.Add(depozit1);
        }

        private void SetupOperativaPanel()
        {
            int startY = 45, spacing = 55, labelWidth = 145, textBoxWidth = 240, textBoxX = 175;

            label3.Text = "Utvrdjeni depozit:"; label3.Location = new Point(15, startY); label3.Size = new Size(labelWidth, 28);
            utvrdjeniDepozit.Location = new Point(textBoxX, startY); utvrdjeniDepozit.Size = new Size(textBoxWidth, 28); utvrdjeniDepozit.BackColor = Color.FromArgb(55, 65, 81); utvrdjeniDepozit.ForeColor = Color.White; utvrdjeniDepozit.BorderStyle = BorderStyle.FixedSingle; utvrdjeniDepozit.ReadOnly = true;

            label11.Text = "Depozit (Pazar):"; label11.Location = new Point(15, startY + spacing); label11.Size = new Size(labelWidth, 28);
            depozit.Location = new Point(textBoxX, startY + spacing); depozit.Size = new Size(textBoxWidth, 28); depozit.BackColor = Color.FromArgb(55, 65, 81); depozit.ForeColor = Color.White; depozit.BorderStyle = BorderStyle.FixedSingle; depozit.ReadOnly = true;

            label18.Text = "Stanje na kraju:"; label18.Location = new Point(15, startY + spacing * 2); label18.Size = new Size(labelWidth, 28);
            stanjedepnakraju.Location = new Point(textBoxX, startY + spacing * 2); stanjedepnakraju.Size = new Size(textBoxWidth, 28); stanjedepnakraju.BackColor = Color.FromArgb(55, 65, 81); stanjedepnakraju.ForeColor = Color.White; stanjedepnakraju.BorderStyle = BorderStyle.FixedSingle; stanjedepnakraju.ReadOnly = true;

            label5.Text = "Radnik:"; label5.Location = new Point(15, startY + spacing * 3); label5.Size = new Size(labelWidth, 28);
            smena1.Location = new Point(textBoxX, startY + spacing * 3); smena1.Size = new Size(textBoxWidth, 28); smena1.BackColor = Color.FromArgb(55, 65, 81); smena1.ForeColor = Color.White; smena1.BorderStyle = BorderStyle.FixedSingle; smena1.ReadOnly = true;

            label2.Text = "Lokacija:"; label2.Location = new Point(15, startY + spacing * 4); label2.Size = new Size(labelWidth, 28);
            lokacija.Location = new Point(textBoxX, startY + spacing * 4); lokacija.Size = new Size(textBoxWidth, 28); lokacija.BackColor = Color.FromArgb(55, 65, 81); lokacija.ForeColor = Color.White; lokacija.BorderStyle = BorderStyle.FixedSingle; lokacija.ReadOnly = true;

            label4.Text = "Kod Lokacije:"; label4.Location = new Point(15, startY + spacing * 5); label4.Size = new Size(labelWidth, 28);
            kodlokacije.Location = new Point(textBoxX, startY + spacing * 5); kodlokacije.Size = new Size(textBoxWidth, 28); kodlokacije.BackColor = Color.FromArgb(55, 65, 81); kodlokacije.ForeColor = Color.White; kodlokacije.BorderStyle = BorderStyle.FixedSingle; kodlokacije.ReadOnly = true;

            operativaPanel.Controls.Add(label3); operativaPanel.Controls.Add(utvrdjeniDepozit);
            operativaPanel.Controls.Add(label11); operativaPanel.Controls.Add(depozit);
            operativaPanel.Controls.Add(label18); operativaPanel.Controls.Add(stanjedepnakraju);
            operativaPanel.Controls.Add(label5); operativaPanel.Controls.Add(smena1);
            operativaPanel.Controls.Add(label2); operativaPanel.Controls.Add(lokacija);
            operativaPanel.Controls.Add(label4); operativaPanel.Controls.Add(kodlokacije);
        }

        private void SetupBottomSection()
        {
            Label lblBrojGostijuAparati = new Label() { Text = "Aparati:", ForeColor = Color.White, Location = new Point(20, 500), Size = new Size(70, 28) };
            this.Controls.Add(lblBrojGostijuAparati);
            txtBrojGostijuAparati.Location = new Point(95, 498); txtBrojGostijuAparati.Size = new Size(100, 28); txtBrojGostijuAparati.BackColor = Color.FromArgb(55, 65, 81); txtBrojGostijuAparati.ForeColor = Color.White; txtBrojGostijuAparati.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(txtBrojGostijuAparati);

            Label lblBrojGostijuKladionica = new Label() { Text = "Kladionica:", ForeColor = Color.White, Location = new Point(210, 500), Size = new Size(80, 28) };
            this.Controls.Add(lblBrojGostijuKladionica);
            txtBrojGostijuKladionica.Location = new Point(295, 498); txtBrojGostijuKladionica.Size = new Size(100, 28); txtBrojGostijuKladionica.BackColor = Color.FromArgb(55, 65, 81); txtBrojGostijuKladionica.ForeColor = Color.White; txtBrojGostijuKladionica.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(txtBrojGostijuKladionica);

            Label lblBrojGostijuOnline = new Label() { Text = "Online Dep.:", ForeColor = Color.White, Location = new Point(410, 500), Size = new Size(85, 28) };
            this.Controls.Add(lblBrojGostijuOnline);
            txtBrojGostijuOnlineDepozit.Location = new Point(500, 498); txtBrojGostijuOnlineDepozit.Size = new Size(100, 28); txtBrojGostijuOnlineDepozit.BackColor = Color.FromArgb(55, 65, 81); txtBrojGostijuOnlineDepozit.ForeColor = Color.White; txtBrojGostijuOnlineDepozit.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(txtBrojGostijuOnlineDepozit);

            Label lblUkupnoGostiju = new Label() { Text = "Ukupno:", ForeColor = Color.White, Location = new Point(620, 500), Size = new Size(60, 28) };
            this.Controls.Add(lblUkupnoGostiju);
            txtUkupnoGostiju.Location = new Point(685, 498); txtUkupnoGostiju.Size = new Size(100, 28); txtUkupnoGostiju.BackColor = Color.FromArgb(31, 41, 55); txtUkupnoGostiju.ForeColor = Color.White; txtUkupnoGostiju.BorderStyle = BorderStyle.FixedSingle; txtUkupnoGostiju.ReadOnly = true;
            this.Controls.Add(txtUkupnoGostiju);

            label20.Text = "Inkasacija u banku:"; label20.Location = new Point(20, 550); label20.ForeColor = Color.White; label20.Size = new Size(140, 30);
            this.Controls.Add(label20);
            inkasacija.Location = new Point(170, 548); inkasacija.Size = new Size(160, 32); inkasacija.BackColor = Color.FromArgb(55, 65, 81); inkasacija.ForeColor = Color.White; inkasacija.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(inkasacija);

            btnInkasacijaAuth = new Button() { Text = "🔑", Location = new Point(340, 548), Size = new Size(40, 32), BackColor = Color.FromArgb(10, 108, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnInkasacijaAuth.FlatAppearance.BorderSize = 0;
            btnInkasacijaAuth.Click += (s, e) => { if (string.IsNullOrWhiteSpace(inkasacija.Text)) { MessageBox.Show("Unesite iznos za inkasaciju pre autorizacije!", "Upozorenje", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; } PokreniAutorizacijuZaInkasaciju(GetValue(inkasacija)); };
            this.Controls.Add(btnInkasacijaAuth);

            btnZavrsiSmenu.Location = new Point(600, 600); btnZavrsiSmenu.Size = new Size(240, 55); btnZavrsiSmenu.FlatStyle = FlatStyle.Flat; btnZavrsiSmenu.BackColor = Color.FromArgb(10, 108, 255); btnZavrsiSmenu.ForeColor = Color.White; btnZavrsiSmenu.FlatAppearance.BorderSize = 0; btnZavrsiSmenu.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            this.Controls.Add(btnZavrsiSmenu);
            // Re-attach click handler (ensure single subscription)
            btnZavrsiSmenu.Click -= btnZavrsiSmenu_Click;
            btnZavrsiSmenu.Click += btnZavrsiSmenu_Click;
        }

        private void ApplyRoundedCorners()
        {
            finansijePanel.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, finansijePanel.Width, finansijePanel.Height, 15, 15));
            depozitiPanel.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, depozitiPanel.Width, depozitiPanel.Height, 15, 15));
            operativaPanel.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, operativaPanel.Width, operativaPanel.Height, 15, 15));
        }

        private void WriteDebug(string poruka)
        {
            try
            {
                File.AppendAllText(debugLogPath, poruka + Environment.NewLine);
            }
            catch { }
        }

        private void NamestiDogadjaje()
        {
            kazino.Leave += (s, e) => { if (trenutnaSmena == 2 && !string.IsNullOrWhiteSpace(kazino.Text)) { originalKazino = GetValue(kazino); IzracunajSve(); } };
            kladionica.Leave += (s, e) => { if (trenutnaSmena == 2 && !string.IsNullOrWhiteSpace(kladionica.Text)) { originalKladionica = GetValue(kladionica); IzracunajSve(); } };
            lbet.Leave += (s, e) => { if (trenutnaSmena == 2 && !string.IsNullOrWhiteSpace(lbet.Text)) { originalLbet = GetValue(lbet); IzracunajSve(); } };
            sank.Leave += (s, e) => { if (trenutnaSmena == 2 && !string.IsNullOrWhiteSpace(sank.Text)) { originalSank = GetValue(sank); IzracunajSve(); } };

            kazino.TextChanged += IzracunajSve_TextChanged; kladionica.TextChanged += IzracunajSve_TextChanged;
            lbet.TextChanged += IzracunajSve_TextChanged; sank.TextChanged += IzracunajSve_TextChanged;
            dopuna.TextChanged += IzracunajSve_TextChanged; podizanje.TextChanged += IzracunajSve_TextChanged;
            troskovi.TextChanged += IzracunajSve_TextChanged; depozit1.TextChanged += IzracunajSve_TextChanged;
        }

        private void UcitajBazuIzPrveSmene()
        {
            string prvaSmenaPodaciPath = Path.Combine(ConfigFolderPath, "prva_smena_podaci.txt");
            if (File.Exists(prvaSmenaPodaciPath))
            {
                try
                {
                    string[] linije = File.ReadAllLines(prvaSmenaPodaciPath);
                    if (linije.Length >= 16)
                    {
                        decimal.TryParse(new string(linije[5].Where(c => char.IsDigit(c) || c == '-').ToArray()), out bazaKazino);
                        decimal.TryParse(new string(linije[6].Where(c => char.IsDigit(c) || c == '-').ToArray()), out bazaKladionica);
                        decimal.TryParse(new string(linije[7].Where(c => char.IsDigit(c) || c == '-').ToArray()), out bazaLbet);
                        decimal.TryParse(new string(linije[8].Where(c => char.IsDigit(c) || c == '-').ToArray()), out bazaSank);
                    }
                }
                catch { }
            }
        }

        private decimal GetValue(TextBox tb)
        {
            if (tb == null || string.IsNullOrWhiteSpace(tb.Text)) return 0;
            string cistBroj = "";
            foreach (char c in tb.Text.Trim())
            {
                if (char.IsDigit(c)) cistBroj += c;
                else if (c == '-' && cistBroj.Length == 0) cistBroj += c;
            }
            return decimal.TryParse(cistBroj, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rezultat) ? rezultat : 0;
        }

        private int GetIntValue(TextBox tb)
        {
            if (tb == null || string.IsNullOrWhiteSpace(tb.Text)) return 0;
            string cistBroj = "";
            foreach (char c in tb.Text.Trim()) if (char.IsDigit(c)) cistBroj += c;
            return int.TryParse(cistBroj, out int rezultat) ? rezultat : 0;
        }

        private string FormatujBroj(decimal broj) => broj.ToString("N0");

        private void IzracunajSve()
        {
            if (depozit1 == null || kazino == null) return;
            decimal k, kl, l, s;
            if (trenutnaSmena == 2)
            {
                k = originalKazino - bazaKazino;
                kl = originalKladionica - bazaKladionica;
                l = originalLbet - bazaLbet;
                s = originalSank - bazaSank;
            }
            else
            {
                k = GetValue(kazino); kl = GetValue(kladionica); l = GetValue(lbet); s = GetValue(sank);
            }
            decimal dop = dopunaOdobrena ? GetValue(dopuna) : 0;
            decimal pod = podizanjeOdobreno ? GetValue(podizanje) : 0;
            decimal tro = ukupniTroskovi;
            decimal pocetniDepozit = GetValue(depozit1);
            decimal pazarSmene = (k + kl + l + s + dop) - (pod + tro);
            decimal konacnoUKasi = pocetniDepozit + pazarSmene;
            depozit.Text = FormatujBroj(pazarSmene);
            stanjedepnakraju.Text = FormatujBroj(konacnoUKasi);
            SnimiUTempFajl();

            // Sačuvaj stanje u Registry pri svakoj promeni
            SacuvajStanjeURegistry();
        }

        private void PokreniAutorizacijuSaOpisom()
        {
            decimal iznos = GetValue(troskovi);
            if (iznos <= 0) { MessageBox.Show("Molimo vas da prvo unesete iznos u polje za Troskovi pre nego što kliknete na ključ.", "Polje je prazno", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            Form opisForm = new Form() { Text = "Unos opisa troškova", Size = new Size(450, 200), BackColor = Color.FromArgb(17, 24, 39), FormBorderStyle = FormBorderStyle.FixedSingle, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblOpis = new Label() { Text = "Unesite opis za troškove:", Location = new Point(20, 25), Size = new Size(200, 25), ForeColor = Color.White, Font = new Font("Segoe UI", 10) };
            TextBox txtOpis = new TextBox() { Location = new Point(20, 55), Size = new Size(390, 30), BackColor = Color.FromArgb(55, 65, 81), ForeColor = Color.White, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
            Button btnNastavi = new Button() { Text = "Nastavi", Location = new Point(150, 105), Size = new Size(120, 35), BackColor = Color.FromArgb(10, 108, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnNastavi.FlatAppearance.BorderSize = 0;
            btnNastavi.Click += (sender, e) => { if (string.IsNullOrWhiteSpace(txtOpis.Text)) { MessageBox.Show("Molimo vas da unesete opis za troškove!", "Opis je obavezan", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; } PokreniAutorizaciju("Troskovi", iznos, txtOpis.Text.Trim()); opisForm.Close(); };
            opisForm.Controls.Add(lblOpis); opisForm.Controls.Add(txtOpis); opisForm.Controls.Add(btnNastavi); opisForm.AcceptButton = btnNastavi;
            opisForm.ShowDialog();
        }

        private void PokreniAutorizaciju(string tip, decimal? unapredIznos = null, string unapredOpis = null)
        {
            decimal iznos = 0; string opis = "";
            if (tip == "Dopuna") iznos = GetValue(dopuna);
            if (tip == "Podizanje") iznos = GetValue(podizanje);
            if (tip == "Troskovi") { iznos = unapredIznos ?? GetValue(troskovi); opis = unapredOpis ?? ""; }
            if (iznos <= 0) { MessageBox.Show($"Molimo vas da prvo unesete iznos u polje za {tip} pre nego što kliknete na ključ.", "Polje je prazno", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            Random rand = new Random();
            string generisaniKod = rand.Next(10000, 99999).ToString();
            string poruka = $"🔑 *AUTORIZACIJA ZAHTEVA*\n\n📍 *Lokacija:* {lokacija.Text}\n👤 *Radnik:* {smena1.Text}\n📋 *Tip:* {tip}\n💰 *Iznos:* {FormatujBroj(iznos)} RSD";
            if (tip == "Troskovi" && !string.IsNullOrEmpty(opis)) poruka += $"\n📝 *Opis:* {opis}";
            poruka += $"\n\n🔢 *KOD ZA UNOS:* `{generisaniKod}`";
            _ = PosaljiNaTelegram(poruka);

            Form authForm = new Form() { Text = "Unos Lozinke", Size = new Size(350, 200), BackColor = Color.FromArgb(17, 24, 39), FormBorderStyle = FormBorderStyle.FixedSingle, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblInfo = new Label() { Text = $"Unesite kod sa Telegrama za {tip}:", Location = new Point(20, 25), Size = new Size(300, 20), ForeColor = Color.White, Font = new Font("Segoe UI", 10) };
            TextBox txtKod = new TextBox() { Location = new Point(20, 55), Size = new Size(295, 30), BackColor = Color.FromArgb(55, 65, 81), ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            Button btnPotvrdi = new Button() { Text = "Potvrdi", Location = new Point(110, 105), Size = new Size(120, 35), BackColor = Color.FromArgb(10, 108, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnPotvrdi.FlatAppearance.BorderSize = 0;
            btnPotvrdi.Click += (sender, e) =>
            {
                if (txtKod.Text.Trim() == generisaniKod)
                {
                    if (tip == "Dopuna") { dopunaOdobrena = true; dopuna.ReadOnly = true; dopuna.BackColor = Color.FromArgb(31, 41, 55); btnDopunaAuth.Enabled = false; MessageBox.Show("Uspešna autorizacija! Iznos je zaključan i ubačen u računicu pazara.", "Uspešno", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    else if (tip == "Podizanje") { podizanjeOdobreno = true; podizanje.ReadOnly = true; podizanje.BackColor = Color.FromArgb(31, 41, 55); btnPodizanjeAuth.Enabled = false; MessageBox.Show("Uspešna autorizacija! Iznos je zaključan i ubačen u računicu pazara.", "Uspešno", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    else if (tip == "Troskovi") { Trosak noviTrosak = new Trosak { Iznos = iznos, Opis = opis, Vreme = DateTime.Now }; listaTroskova.Add(noviTrosak); ukupniTroskovi += iznos; troskovi.Text = ""; MessageBox.Show($"Uspešna autorizacija! Trošak od {FormatujBroj(iznos)} RSD je dodat u računicu.\n\nUkupno troškova u smeni: {FormatujBroj(ukupniTroskovi)} RSD", "Uspešno", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    authForm.DialogResult = DialogResult.OK; authForm.Close();
                }
                else MessageBox.Show("Netačan kod! Pokušajte ponovo.", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            authForm.Controls.Add(lblInfo); authForm.Controls.Add(txtKod); authForm.Controls.Add(btnPotvrdi); authForm.AcceptButton = btnPotvrdi;
            if (authForm.ShowDialog() == DialogResult.OK) IzracunajSve();
        }

        private void PokreniAutorizacijuZaInkasaciju(decimal iznos)
        {
            decimal trenutnoStanje = GetValue(stanjedepnakraju);
            if (iznos > trenutnoStanje)
            {
                if (MessageBox.Show($"Upozorenje: Iznos inkasacije ({FormatujBroj(iznos)}) je veći od trenutnog stanja kase ({FormatujBroj(trenutnoStanje)}).\n\nDa li želite da nastavite sa autorizacijom?", "Provera inkasacije", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No) return;
            }

            Random rand = new Random();
            string generisaniKod = rand.Next(10000, 99999).ToString();
            string poruka = $"🔑 *AUTORIZACIJA ZAHTEVA - INKASACIJA*\n\n📍 *Lokacija:* {lokacija.Text}\n👤 *Radnik:* {smena1.Text}\n📋 *Tip:* Inkasacija u banku\n💰 *Iznos:* {FormatujBroj(iznos)} RSD\n📊 *Trenutno stanje kase:* {FormatujBroj(trenutnoStanje)} RSD\n\n🔢 *KOD ZA UNOS:* `{generisaniKod}`";
            _ = PosaljiNaTelegram(poruka);

            Form authForm = new Form() { Text = "Unos Lozinke - Inkasacija", Size = new Size(350, 200), BackColor = Color.FromArgb(17, 24, 39), FormBorderStyle = FormBorderStyle.FixedSingle, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            Label lblInfo = new Label() { Text = "Unesite kod sa Telegrama za INKASACIJU:", Location = new Point(20, 25), Size = new Size(300, 20), ForeColor = Color.White, Font = new Font("Segoe UI", 10) };
            TextBox txtKod = new TextBox() { Location = new Point(20, 55), Size = new Size(295, 30), BackColor = Color.FromArgb(55, 65, 81), ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            Button btnPotvrdi = new Button() { Text = "Potvrdi", Location = new Point(110, 105), Size = new Size(120, 35), BackColor = Color.FromArgb(10, 108, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnPotvrdi.FlatAppearance.BorderSize = 0;
            btnPotvrdi.Click += (sender, e) => { if (txtKod.Text.Trim() == generisaniKod) { inkasacijaOdobrena = true; IzvrsiInkasaciju(iznos); authForm.DialogResult = DialogResult.OK; authForm.Close(); } else MessageBox.Show("Netačan kod! Pokušajte ponovo.", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Error); };
            authForm.Controls.Add(lblInfo); authForm.Controls.Add(txtKod); authForm.Controls.Add(btnPotvrdi); authForm.AcceptButton = btnPotvrdi;
            authForm.ShowDialog();
        }

        private void IzvrsiInkasaciju(decimal iznosInkasacije)
        {
            decimal trenutnoStanje = GetValue(stanjedepnakraju);
            decimal novoStanje = trenutnoStanje - iznosInkasacije;
            stanjedepnakraju.Text = FormatujBroj(novoStanje);

            string prenosDepozitaPath = Path.Combine(ConfigFolderPath, "prenos_depozita.txt");
            string zadnjaInkasacijaPath = Path.Combine(ConfigFolderPath, "zadnja_inkasacija.txt");

            File.WriteAllText(prenosDepozitaPath, novoStanje.ToString());
            File.WriteAllText(zadnjaInkasacijaPath, iznosInkasacije.ToString());
            depozit1.Text = FormatujBroj(novoStanje);
            SnimiUTempFajl();
            WriteDebug($"INKASACIJA: Staro stanje={trenutnoStanje}, Inkasirano={iznosInkasacije}, Novo stanje={novoStanje}");
            MessageBox.Show($"✅ Inkasacija uspešno obavljena!\n\nPrethodno stanje: {FormatujBroj(trenutnoStanje)} RSD\nInkasirano: {FormatujBroj(iznosInkasacije)} RSD\nNovo stanje: {FormatujBroj(novoStanje)} RSD", "Inkasacija", MessageBoxButtons.OK, MessageBoxIcon.Information);
            inkasacija.Text = ""; inkasacijaOdobrena = false;
        }

        private async void btnZavrsiSmenu_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(dopuna.Text) && GetValue(dopuna) > 0 && !dopunaOdobrena) { MessageBox.Show("Greška: Uneli ste iznos za Dopunu Depozita, ali niste odradili autorizaciju preko ključa!", "Autorizacija neophodna", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (!string.IsNullOrWhiteSpace(podizanje.Text) && GetValue(podizanje) > 0 && !podizanjeOdobreno) { MessageBox.Show("Greška: Uneli ste iznos za Podizanje Depozita, ali niste odradili autorizaciju preko ključa!", "Autorizacija neophodna", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (!string.IsNullOrWhiteSpace(troskovi.Text) && GetValue(troskovi) > 0) { MessageBox.Show("Greška: Uneli ste iznos za Troškove, ali niste odradili autorizaciju preko ključa!", "Autorizacija neophodna", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (!string.IsNullOrWhiteSpace(inkasacija.Text) && GetValue(inkasacija) > 0 && !inkasacijaOdobrena) { MessageBox.Show("Greška: Uneli ste iznos za Inkasaciju, ali niste odradili autorizaciju preko ključa!", "Autorizacija neophodna", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            WriteDebug("\n========== ZAVRSAVANJE SMENE ==========");

            string[] podaci = {
                DateTime.Now.ToString("dd.MM.yyyy"), smena1.Text, lokacija.Text, kodlokacije.Text,
                trenutnaSmena.ToString(),
                trenutnaSmena == 2 ? originalKazino.ToString() : kazino.Text,
                trenutnaSmena == 2 ? originalKladionica.ToString() : kladionica.Text,
                trenutnaSmena == 2 ? originalLbet.ToString() : lbet.Text,
                trenutnaSmena == 2 ? originalSank.ToString() : sank.Text,
                dopuna.Text, podizanje.Text, ukupniTroskovi.ToString(), depozit.Text, depozit1.Text,
                stanjedepnakraju.Text, utvrdjeniDepozit.Text,
                txtBrojGostijuAparati.Text, txtBrojGostijuKladionica.Text, txtBrojGostijuOnlineDepozit.Text, txtUkupnoGostiju.Text,
                dopunaOdobrena.ToString(), podizanjeOdobreno.ToString(),
                SerijalizujTroskove(), inkasacijaOdobrena.ToString()
            };

            decimal krajnjeStanje = GetValue(stanjedepnakraju);
            string prenosDepozitaPath = Path.Combine(ConfigFolderPath, "prenos_depozita.txt");
            File.WriteAllText(prenosDepozitaPath, krajnjeStanje.ToString());

            string tempStatePath = Path.Combine(ConfigFolderPath, "temp_state.txt");
            if (File.Exists(tempStatePath)) File.Delete(tempStatePath);

            ZapamtiSveUTekstualniFajl();

            // Sačuvaj krajnje stanje u Registry
            SacuvajStanjeURegistry();

            if (trenutnaSmena == 1)
            {
                WriteDebug($"ČUVANJE PRVE SMENE - Troškovi: {SerijalizujTroskove()}");
                WriteDebug($"Ukupni troškovi: {ukupniTroskovi}");
                WriteDebug($"Broj troškova u listi: {listaTroskova.Count}");

                string prvaSmenaPodaciPath = Path.Combine(ConfigFolderPath, "prva_smena_podaci.txt");
                string prvaSmenaTroskoviPath = Path.Combine(ConfigFolderPath, "prva_smena_troskovi_raw.txt");

                File.WriteAllLines(prvaSmenaPodaciPath, podaci);
                File.WriteAllText(prvaSmenaTroskoviPath, SerijalizujTroskove());

                // Zapamti da sledeća prijava treba da bude smena 2
                try
                {
                    string smenaPath = Path.Combine(ConfigFolderPath, "smena_config.txt");
                    File.WriteAllText(smenaPath, "2");
                    WriteDebug($"Sačuvana sledeća smena u fajlu: {smenaPath} -> 2");
                }
                catch (Exception ex)
                {
                    WriteDebug($"Greška pri čuvanju smene: {ex.Message}");
                }

                LoginFormcs login = new LoginFormcs(2);
                login.Show();
                this.Hide();
            }
            else
            {
                if (MessageBox.Show("⚠️ VAŽAN PODSETNIK ZA KRAJ SMENE ⚠️\n\nPre nego što završite popis i generišete dnevni izveštaj, obavezno:\n\n1️⃣ NULIRAJTE KASU na SVIM APARATIMA (Kazino aparati)\n2️⃣ NULIRAJTE L-BET SALDO (L-Bet terminal)\n\nDa li ste izvršili nuliranje kase na aparatima i L-Betu?", "Potvrda nuliranja kase", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
                { MessageBox.Show("Molimo vas da prvo nulirate kasu na aparatima i L-Betu, zatim nastavite sa završetkom popisa.", "Nuliranje neophodno", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                if (MessageBox.Show("Završiti dan i generisati izveštaj?", "Kraj", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    // Sakrij formu odmah tako da radnik ne vidi dalji proces
                    try
                    {
                        this.Invoke(new Action(() =>
                        {
                            try
                            {
                                this.Hide();
                                this.ShowInTaskbar = false;
                                this.WindowState = FormWindowState.Minimized;
                                WriteDebug("Aplikacija je sakrivena pre generisanja izveštaja.");
                            }
                            catch (Exception ex)
                            {
                                WriteDebug($"Greška pri sakrivanju forme: {ex.Message}");
                            }
                        }));
                    }
                    catch { }

                    // Pokreni generisanje i štampu u pozadini bez prikazivanja MessageBox-a
                    await SacuvajObeSmeneUExcel(false);

                    // Sačekaj još 10s dok je aplikacija sakrivena pre gašenja
                    try
                    {
                        WriteDebug("Čeka 10s nakon skrivenog rada pre gašenja...");
                        await Task.Delay(10000);
                        WriteDebug("Isteklo 10s čekanje nakon skrivenog rada.");
                    }
                    catch { }
                }

                string prvaSmenaPodaciPath = Path.Combine(ConfigFolderPath, "prva_smena_podaci.txt");
                string prvaSmenaTroskoviPath = Path.Combine(ConfigFolderPath, "prva_smena_troskovi_raw.txt");

                // Nakon završetka dana, resetuj zapamćenu smenu na 1 (prva smena za naredni dan)
                try
                {
                    string smenaPath = Path.Combine(ConfigFolderPath, "smena_config.txt");
                    File.WriteAllText(smenaPath, "1");
                    WriteDebug($"Sačuvana sledeća smena u fajlu: {smenaPath} -> 1");
                }
                catch (Exception ex)
                {
                    WriteDebug($"Greška pri čuvanju smene: {ex.Message}");
                }

                if (File.Exists(prvaSmenaPodaciPath)) File.Delete(prvaSmenaPodaciPath);
                if (File.Exists(prvaSmenaTroskoviPath)) File.Delete(prvaSmenaTroskoviPath);
                Application.Exit();
            }
        }

        private string SerijalizujTroskove()
        {
            if (listaTroskova.Count == 0) return "";
            List<string> serijalizovani = new List<string>();
            foreach (var trosak in listaTroskova) serijalizovani.Add($"{trosak.Iznos}|{trosak.Opis}|{trosak.Vreme:yyyy-MM-dd HH:mm:ss}");
            return string.Join("|||", serijalizovani);
        }

        private void DeserijalizujTroskove(string data)
        {
            listaTroskova.Clear(); ukupniTroskovi = 0;
            if (string.IsNullOrEmpty(data)) return;
            foreach (string trosakStr in data.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] delovi = trosakStr.Split('|');
                if (delovi.Length >= 3 && decimal.TryParse(delovi[0], out decimal iznos))
                { listaTroskova.Add(new Trosak { Iznos = iznos, Opis = delovi[1], Vreme = DateTime.Parse(delovi[2]) }); ukupniTroskovi += iznos; }
            }
        }

        private async Task PosaljiNaTelegram(string tekst)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://api.telegram.org/bot{telegramToken}/sendMessage";
                    string cistTekst = tekst.Replace("\r\n", "\n").Replace("\n", "\\n").Replace("\"", "\\\"");
                    string jsonPayload = $"{{\"chat_id\": \"{telegramChatId}\", \"text\": \"{cistTekst}\", \"parse_mode\": \"Markdown\"}}";
                    await client.PostAsync(url, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
                }
            }
            catch { }
        }

        private void SnimiUTempFajl()
        {
            try
            {
                string[] podaci = { kazino.Text, kladionica.Text, lbet.Text, sank.Text, dopuna.Text, podizanje.Text, troskovi.Text, stanjedepnakraju.Text,
                    trenutnaSmena.ToString(), depozit.Text, depozit1.Text, utvrdjeniDepozit.Text, dopunaOdobrena.ToString(), podizanjeOdobreno.ToString(),
                    ukupniTroskovi.ToString(), txtBrojGostijuAparati.Text, txtBrojGostijuKladionica.Text, txtBrojGostijuOnlineDepozit.Text, txtUkupnoGostiju.Text,
                    SerijalizujTroskove(), originalKazino.ToString(), originalKladionica.ToString(), originalLbet.ToString(), originalSank.ToString(), inkasacijaOdobrena.ToString() };

                string tempStatePath = Path.Combine(ConfigFolderPath, "temp_state.txt");
                File.WriteAllLines(tempStatePath, podaci);
            }
            catch (Exception ex) { WriteDebug($"Greška pri čuvanju temp fajla: {ex.Message}"); }
        }

        private void UcitajIzTempFajla()
        {
            string tempStatePath = Path.Combine(ConfigFolderPath, "temp_state.txt");
            if (File.Exists(tempStatePath))
            {
                try
                {
                    string[] linije = File.ReadAllLines(tempStatePath);
                    if (linije.Length >= 12) { kazino.Text = linije[0]; kladionica.Text = linije[1]; lbet.Text = linije[2]; sank.Text = linije[3]; dopuna.Text = linije[4]; podizanje.Text = linije[5]; troskovi.Text = linije[6]; if (string.IsNullOrWhiteSpace(depozit1.Text)) depozit1.Text = linije[10]; utvrdjeniDepozit.Text = linije[11]; }
                    if (linije.Length >= 15) { bool.TryParse(linije[12], out dopunaOdobrena); bool.TryParse(linije[13], out podizanjeOdobreno); if (linije.Length >= 16) decimal.TryParse(linije[15], out ukupniTroskovi); }
                    if (linije.Length >= 20) { txtBrojGostijuAparati.Text = linije[16]; txtBrojGostijuKladionica.Text = linije[17]; txtBrojGostijuOnlineDepozit.Text = linije[18]; txtUkupnoGostiju.Text = linije[19]; IzracunajUkupnoGostiju(); }
                    if (linije.Length >= 21) DeserijalizujTroskove(linije[20]);
                    if (linije.Length >= 25) { decimal.TryParse(linije[21], out originalKazino); decimal.TryParse(linije[22], out originalKladionica); decimal.TryParse(linije[23], out originalLbet); decimal.TryParse(linije[24], out originalSank); }
                    if (linije.Length >= 26) bool.TryParse(linije[25], out inkasacijaOdobrena);
                }
                catch (Exception ex) { WriteDebug($"Greška pri učitavanju temp fajla: {ex.Message}"); }
            }
        }

        private void ProveriITretirajPrethodnuAutorizaciju()
        {
            if (dopunaOdobrena && GetValue(dopuna) > 0) { dopuna.ReadOnly = true; dopuna.BackColor = Color.FromArgb(31, 41, 55); btnDopunaAuth.Enabled = false; }
            if (podizanjeOdobreno && GetValue(podizanje) > 0) { podizanje.ReadOnly = true; podizanje.BackColor = Color.FromArgb(31, 41, 55); btnPodizanjeAuth.Enabled = false; }
        }

        private async Task UcitajUtvrdjeniDepozitSaWeba(string trazeniKod)
        {
            try
            {
                string csvData = await new HttpClient().GetStringAsync(urlLokacije);
                foreach (string linija in csvData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] delovi = linija.Split(',');
                    if (delovi.Length >= 2 && delovi[0].Trim() == trazeniKod) { utvrdjeniDepozit.Text = delovi[1].Trim(); break; }
                }
            }
            catch { }
            IzracunajSve();
        }

        private void ZapamtiSveUTekstualniFajl()
        {
            string nazivFajla = Path.Combine(ConfigFolderPath, "Smena" + trenutnaSmena + ".txt");
            StringBuilder sadrzaj = new StringBuilder();
            sadrzaj.AppendLine($"Smena: {trenutnaSmena}\nRadnik: {smena1.Text}\nPočetno u kasi: {depozit1.Text}\nPazar smene: {depozit.Text}\nKrajnje stanje: {stanjedepnakraju.Text}");
            sadrzaj.AppendLine($"\n=== BROJ GOSTIJU ===\nAparati: {txtBrojGostijuAparati.Text}\nKladionica: {txtBrojGostijuKladionica.Text}\nOnline depozit: {txtBrojGostijuOnlineDepozit.Text}\nUkupno: {txtUkupnoGostiju.Text}");
            if (listaTroskova.Count > 0) { sadrzaj.AppendLine($"\n=== UKUPNO TROŠKOVA: {FormatujBroj(ukupniTroskovi)} RSD ===\n=== LISTA TROŠKOVA ==="); foreach (var trosak in listaTroskova) sadrzaj.AppendLine($"- {trosak}"); }
            File.WriteAllText(nazivFajla, sadrzaj.ToString());
        }

        private async Task SacuvajObeSmeneUExcel(bool showMessage = true)
        {
            try
            {
                string prvaSmenaPodaciPath = Path.Combine(ConfigFolderPath, "prva_smena_podaci.txt");
                string prvaSmenaTroskoviPath = Path.Combine(ConfigFolderPath, "prva_smena_troskovi_raw.txt");

                if (!File.Exists(prvaSmenaPodaciPath))
                {
                    MessageBox.Show("❌ Nisu pronađeni podaci za Prvu smenu! Nemoguće je generisati kompletan dnevni izveštaj.", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string[] p1 = File.ReadAllLines(prvaSmenaPodaciPath);

                List<Trosak> troskoviPrvaSmena = new List<Trosak>();
                decimal ukupniTroskoviPrva = 0;

                if (File.Exists(prvaSmenaTroskoviPath))
                {
                    string troskoviData = File.ReadAllText(prvaSmenaTroskoviPath);
                    if (!string.IsNullOrWhiteSpace(troskoviData))
                    {
                        foreach (string trosakStr in troskoviData.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string[] delovi = trosakStr.Split('|');
                            if (delovi.Length >= 3 && decimal.TryParse(delovi[0], out decimal iznos))
                            {
                                DateTime vreme;
                                if (DateTime.TryParse(delovi[2], out vreme))
                                {
                                    troskoviPrvaSmena.Add(new Trosak { Iznos = iznos, Opis = delovi[1], Vreme = vreme });
                                    ukupniTroskoviPrva += iznos;
                                }
                            }
                        }
                    }
                }

                decimal matematickiKazino = originalKazino - bazaKazino;
                decimal matematickiKladionica = originalKladionica - bazaKladionica;
                decimal matematickiLbet = originalLbet - bazaLbet;
                decimal matematickiSank = originalSank - bazaSank;

                decimal inkasiraniIznos = 0;
                string zadnjaInkasacijaPath = Path.Combine(ConfigFolderPath, "zadnja_inkasacija.txt");
                if (File.Exists(zadnjaInkasacijaPath))
                    decimal.TryParse(File.ReadAllText(zadnjaInkasacijaPath), out inkasiraniIznos);

                string[] p2 = {
            DateTime.Now.ToString("dd.MM.yyyy"), smena1.Text, lokacija.Text, kodlokacije.Text,
            trenutnaSmena.ToString(),
            matematickiKazino.ToString(), matematickiKladionica.ToString(), matematickiLbet.ToString(), matematickiSank.ToString(),
            dopuna.Text, podizanje.Text, ukupniTroskovi.ToString(), depozit.Text, depozit1.Text,
            stanjedepnakraju.Text, utvrdjeniDepozit.Text,
            txtBrojGostijuAparati.Text, txtBrojGostijuKladionica.Text, txtBrojGostijuOnlineDepozit.Text, txtUkupnoGostiju.Text,
            dopunaOdobrena.ToString(), podizanjeOdobreno.ToString(),
            SerijalizujTroskove(), inkasacijaOdobrena.ToString(), inkasiraniIznos.ToString()
        };

                string monthlyFolder = GetMonthlyFolderPath();
                string fileName = $"Dnevni_Izvestaj_{dateTimePicker1.Value.ToString("dd_MM_yyyy")}.html";
                string path = Path.Combine(monthlyFolder, fileName);

                StringBuilder html = new StringBuilder();

                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html>");
                html.AppendLine("<head>");
                html.AppendLine("<meta charset='UTF-8'>");
                html.AppendLine("<title>Dnevni Izveštaj o Popisu Kase</title>");
                html.AppendLine("<style>");
                html.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 30px; background-color: #ffffff; color: #000000; -webkit-print-color-adjust: exact; print-color-adjust: exact; }");
                html.AppendLine(".container { width: 100%; max-width: 1200px; margin: 0 auto; }");
                html.AppendLine("h1 { text-align: center; color: #1a252f; margin-bottom: 25px; text-transform: uppercase; font-size: 24px; border-bottom: 4px double #1a252f; padding-bottom: 12px; }");
                html.AppendLine("h2 { color: #2c3e50; border-bottom: 2px solid #2c3e50; padding-bottom: 4px; margin-top: 30px; font-size: 18px; text-transform: uppercase; }");
                html.AppendLine("h3 { color: #444; font-size: 14px; margin-bottom: 10px; font-weight: normal; }");
                html.AppendLine("table { width: 100%; border-collapse: collapse; margin: 15px 0; }");
                html.AppendLine("th { background-color: #f2f4f4 !important; color: #000000 !important; padding: 10px 8px; text-align: center; font-weight: bold; border: 1px solid #000000; font-size: 12px; text-transform: uppercase; }");
                html.AppendLine("td { padding: 10px 8px; text-align: center; border: 1px solid #000000; font-size: 12px; color: #000000; }");
                html.AppendLine(".ukupno { background-color: #eaeded !important; font-weight: bold; }");
                html.AppendLine(".kasa-sekcija { background-color: #f8f9f9 !important; padding: 15px; border: 2px solid #000000; border-left: 8px solid #000000; margin-top: 20px; }");
                html.AppendLine(".kasa-sekcija h3 { font-size: 16px; font-weight: bold; margin-top: 0; color: #000000; border-bottom: 1px solid #000000; padding-bottom: 5px; text-transform: uppercase; }");
                html.AppendLine(".kasa-sekcija p { margin: 8px 0; font-size: 15px; color: #000000; }");
                html.AppendLine(".inkasacija { background-color: #fff3cd !important; border-left: 4px solid #ffc107 !important; margin-top: 15px; padding: 10px; }");
                html.AppendLine("@media print { body { margin: 0; padding: 0; background: white; } .container { width: 100%; max-width: 100%; } table { page-break-inside: avoid; } @page { size: A4 landscape; margin: 12mm 10mm 12mm 10mm; } }");
                html.AppendLine("</style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");
                html.AppendLine("<div class='container'>");

                html.AppendLine("<h1>📊 DNEVNI IZVEŠTAJ O POPISU KASE</h1>");

                // ========== PRVA SMENA ==========
                html.AppendLine("<h2>🕒 PRVA SMENA (DAN)</h2>");
                html.AppendLine($"<h3>👤 <strong>Radnik:</strong> {p1[1]} &nbsp;&nbsp;|&nbsp;&nbsp; 📍 <strong>Lokal:</strong> {p1[2]} ({p1[3]}) &nbsp;&nbsp;|&nbsp;&nbsp; 📅 <strong>Vreme popisa:</strong> {p1[0]}</h3>");

                html.AppendLine("<table border='1'>");
                html.AppendLine("<thead>");
                html.AppendLine("<tr>");
                html.AppendLine("<th>Kazino</th>");
                html.AppendLine("<th>Kladionica</th>");
                html.AppendLine("<th>LBet</th>");
                html.AppendLine("<th>Šank</th>");
                html.AppendLine("<th>Dopuna</th>");
                html.AppendLine("<th>Podizanje</th>");
                html.AppendLine("<th>Troškovi</th>");
                html.AppendLine("<th>Pazar Smene</th>");
                html.AppendLine("<th>Početna Kasa</th>");
                html.AppendLine("<th>Krajnja Kasa</th>");
                html.AppendLine("<th>Aparati</th>");
                html.AppendLine("<th>Kladionica</th>");
                html.AppendLine("<th>Online Dep.</th>");
                html.AppendLine("<th>Ukupno Gostiju</th>");
                html.AppendLine("</tr>");
                html.AppendLine("</thead>");
                html.AppendLine("<tbody>");
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p1[5]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p1[6]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p1[7]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p1[8]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p1[9]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p1[10]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p1[11]))} RSD</td>");
                html.AppendLine($"<td><strong>{FormatujBroj(ParsirajBrojIzStringa(p1[12]))} RSD</strong></td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p1[13]))} RSD</td>");
                html.AppendLine($"<td><strong>{FormatujBroj(ParsirajBrojIzStringa(p1[14]))} RSD</strong></td>");
                html.AppendLine($"<td>{(p1.Length > 16 ? p1[16] : "0")}</td>");
                html.AppendLine($"<td>{(p1.Length > 17 ? p1[17] : "0")}</td>");
                html.AppendLine($"<td>{(p1.Length > 18 ? p1[18] : "0")}</td>");
                html.AppendLine($"<td><strong>{(p1.Length > 19 ? p1[19] : "0")}</strong></td>");
                html.AppendLine("</tr>");
                html.AppendLine("</tbody>");
                html.AppendLine("</table>");

                // ========== DRUGA SMENA ==========
                html.AppendLine("<h2>🕒 DRUGA SMENA (NOĆ)</h2>");
                html.AppendLine($"<h3>👤 <strong>Radnik:</strong> {p2[1]} &nbsp;&nbsp;|&nbsp;&nbsp; 📍 <strong>Lokal:</strong> {p2[2]} ({p2[3]}) &nbsp;&nbsp;|&nbsp;&nbsp; 📅 <strong>Vreme popisa:</strong> {p2[0]}</h3>");

                html.AppendLine("<table border='1'>");
                html.AppendLine("<thead>");
                html.AppendLine("<tr>");
                html.AppendLine("<th>Kazino</th>");
                html.AppendLine("<th>Kladionica</th>");
                html.AppendLine("<th>LBet</th>");
                html.AppendLine("<th>Šank</th>");
                html.AppendLine("<th>Dopuna</th>");
                html.AppendLine("<th>Podizanje</th>");
                html.AppendLine("<th>Troškovi</th>");
                html.AppendLine("<th>Pazar Smene</th>");
                html.AppendLine("<th>Početna Kasa</th>");
                html.AppendLine("<th>Krajnja Kasa</th>");
                html.AppendLine("<th>Aparati</th>");
                html.AppendLine("<th>Kladionica</th>");
                html.AppendLine("<th>Online Dep.</th>");
                html.AppendLine("<th>Ukupno Gostiju</th>");
                html.AppendLine("</tr>");
                html.AppendLine("</thead>");
                html.AppendLine("<tbody>");
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p2[5]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p2[6]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p2[7]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p2[8]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p2[9]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p2[10]))} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p2[11]))} RSD</td>");
                html.AppendLine($"<td><strong>{FormatujBroj(ParsirajBrojIzStringa(p2[12]))} RSD</strong></td>");
                html.AppendLine($"<td>{FormatujBroj(ParsirajBrojIzStringa(p2[13]))} RSD</td>");
                html.AppendLine($"<td><strong>{FormatujBroj(ParsirajBrojIzStringa(p2[14]))} RSD</strong></td>");
                html.AppendLine($"<td>{(p2.Length > 16 ? p2[16] : "0")}</td>");
                html.AppendLine($"<td>{(p2.Length > 17 ? p2[17] : "0")}</td>");
                html.AppendLine($"<td>{(p2.Length > 18 ? p2[18] : "0")}</td>");
                html.AppendLine($"<td><strong>{(p2.Length > 19 ? p2[19] : "0")}</strong></td>");
                html.AppendLine("</tr>");
                html.AppendLine("</tbody>");
                html.AppendLine("</table>");

                // ========== UKUPNA STATISTIKA ==========
                decimal kazino1 = ParsirajBrojIzStringa(p1[5]);
                decimal kladionica1 = ParsirajBrojIzStringa(p1[6]);
                decimal lbet1 = ParsirajBrojIzStringa(p1[7]);
                decimal sank1 = ParsirajBrojIzStringa(p1[8]);
                decimal dopuna1 = ParsirajBrojIzStringa(p1[9]);
                decimal podizanje1 = ParsirajBrojIzStringa(p1[10]);
                decimal troskovi1 = ParsirajBrojIzStringa(p1[11]);
                decimal pazar1 = ParsirajBrojIzStringa(p1[12]);
                decimal pocetnaKasa1 = ParsirajBrojIzStringa(p1[13]);

                decimal kazino2 = matematickiKazino;
                decimal kladionica2 = matematickiKladionica;
                decimal lbet2 = matematickiLbet;
                decimal sank2 = matematickiSank;
                decimal dopuna2 = ParsirajBrojIzStringa(p2[9]);
                decimal podizanje2 = ParsirajBrojIzStringa(p2[10]);
                decimal troskovi2 = ParsirajBrojIzStringa(p2[11]);
                decimal pazar2 = ParsirajBrojIzStringa(p2[12]);
                decimal krajnjaKasa2 = ParsirajBrojIzStringa(p2[14]);

                decimal ukupnoKazino = kazino1 + kazino2;
                decimal ukupnoKladionica = kladionica1 + kladionica2;
                decimal ukupnoLbet = lbet1 + lbet2;
                decimal ukupnoSank = sank1 + sank2;
                decimal ukupnoDopuna = dopuna1 + dopuna2;
                decimal ukupnoPodizanje = podizanje1 + podizanje2;
                decimal ukupnoTroskovi = troskovi1 + troskovi2 + ukupniTroskoviPrva + ukupniTroskovi;
                decimal ukupnoPazar = pazar1 + pazar2;

                decimal teoretskoStanje = pocetnaKasa1 + ukupnoPazar;
                decimal razlika = krajnjaKasa2 - teoretskoStanje;

                html.AppendLine("<h2>📈 UKUPNA STATISTIKA ZA CELI DAN</h2>");

                html.AppendLine("<table border='1'>");
                html.AppendLine("<thead>");
                html.AppendLine("<tr>");
                html.AppendLine("<th>Kazino</th>");
                html.AppendLine("<th>Kladionica</th>");
                html.AppendLine("<th>LBet</th>");
                html.AppendLine("<th>Šank</th>");
                html.AppendLine("<th>Dopuna</th>");
                html.AppendLine("<th>Podizanje</th>");
                html.AppendLine("<th>Troškovi</th>");
                html.AppendLine("<th>UKUPAN PAZAR</th>");
                html.AppendLine("</tr>");
                html.AppendLine("</thead>");
                html.AppendLine("<tbody>");
                html.AppendLine("<tr class='ukupno'>");
                html.AppendLine($"<td>{FormatujBroj(ukupnoKazino)} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ukupnoKladionica)} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ukupnoLbet)} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ukupnoSank)} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ukupnoDopuna)} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ukupnoPodizanje)} RSD</td>");
                html.AppendLine($"<td>{FormatujBroj(ukupnoTroskovi)} RSD</td>");
                html.AppendLine($"<td><strong>{FormatujBroj(ukupnoPazar)} RSD</strong></td>");
                html.AppendLine("</tr>");
                html.AppendLine("</tbody>");
                html.AppendLine("</table>");

                // ========== KONAČNO STANJE KASE ==========
                html.AppendLine("<div class='kasa-sekcija'>");
                html.AppendLine("<h3>💰 KONAČNO STANJE KASE ZA DAN</h3>");
                html.AppendLine($"<p>💵 <strong>Početno stanje kase (početak Prve smene):</strong> {FormatujBroj(pocetnaKasa1)} RSD</p>");
                html.AppendLine($"<p>📊 <strong>Ukupan ostvareni pazar (Smena 1 + Smena 2):</strong> {FormatujBroj(ukupnoPazar)} RSD</p>");
                html.AppendLine($"<p>➕ <strong>Teoretsko stanje (Početno + Pazar):</strong> {FormatujBroj(teoretskoStanje)} RSD</p>");

                if (inkasiraniIznos > 0)
                {
                    html.AppendLine("<div class='inkasacija'>");
                    html.AppendLine($"<p>🏦 <strong>INKASACIJA U BANKU:</strong> <strong style='color:#856404;'>{FormatujBroj(inkasiraniIznos)} RSD</strong></p>");
                    html.AppendLine($"<p>💳 <strong>Stanje kase NAKON inkasacije:</strong> {FormatujBroj(krajnjaKasa2)} RSD</p>");
                    html.AppendLine("</div>");
                }
                else
                {
                    html.AppendLine($"<p>🎯 <strong>Završno stanje kase na kraju dana:</strong> <strong>{FormatujBroj(krajnjaKasa2)} RSD</strong></p>");
                }

                if (razlika != 0)
                {
                    string razlikaBoja = razlika > 0 ? "#28a745" : "#dc3545";
                    string razlikaZnak = razlika > 0 ? "+" : "";
                    html.AppendLine($"<p style='color:{razlikaBoja};'><strong>📉 Razlika (Manjak/Višak):</strong> {razlikaZnak}{FormatujBroj(razlika)} RSD</p>");
                }
                html.AppendLine("</div>");

                // ========== LISTA TROŠKOVA PRVA SMENA ==========
                if (troskoviPrvaSmena.Count > 0)
                {
                    html.AppendLine("<div style='margin-top:25px;'>");
                    html.AppendLine($"<h3>🧾 LISTA TROŠKOVA (PRVA SMENA) - UKUPNO: {FormatujBroj(ukupniTroskoviPrva)} RSD</h3>");
                    html.AppendLine("<table border='1' style='width: auto; min-width: 400px;'>");
                    html.AppendLine("<thead>");
                    html.AppendLine("<tr>");
                    html.AppendLine("<th>Iznos</th>");
                    html.AppendLine("<th>Opis</th>");
                    html.AppendLine("<th>Vreme</th>");
                    html.AppendLine("</tr>");
                    html.AppendLine("</thead>");
                    html.AppendLine("<tbody>");
                    foreach (var trosak in troskoviPrvaSmena)
                    {
                        html.AppendLine("<tr>");
                        html.AppendLine($"<td><strong>{FormatujBroj(trosak.Iznos)} RSD</strong></td>");
                        html.AppendLine($"<td>{trosak.Opis}</td>");
                        html.AppendLine($"<td>{trosak.Vreme:HH:mm}</td>");
                        html.AppendLine("</tr>");
                    }
                    html.AppendLine("</tbody>");
                    html.AppendLine("</table>");
                    html.AppendLine("</div>");
                }

                // ========== LISTA TROŠKOVA DRUGA SMENA ==========
                if (listaTroskova.Count > 0)
                {
                    html.AppendLine("<div style='margin-top:25px;'>");
                    html.AppendLine($"<h3>🧾 LISTA TROŠKOVA (DRUGA SMENA) - UKUPNO: {FormatujBroj(ukupniTroskovi)} RSD</h3>");
                    html.AppendLine("<table border='1' style='width: auto; min-width: 400px;'>");
                    html.AppendLine("<thead>");
                    html.AppendLine("<tr>");
                    html.AppendLine("<th>Iznos</th>");
                    html.AppendLine("<th>Opis</th>");
                    html.AppendLine("<th>Vreme</th>");
                    html.AppendLine("</tr>");
                    html.AppendLine("</thead>");
                    html.AppendLine("<tbody>");
                    foreach (var trosak in listaTroskova)
                    {
                        html.AppendLine("<tr>");
                        html.AppendLine($"<td><strong>{FormatujBroj(trosak.Iznos)} RSD</strong></td>");
                        html.AppendLine($"<td>{trosak.Opis}</td>");
                        html.AppendLine($"<td>{trosak.Vreme:HH:mm}</td>");
                        html.AppendLine("</tr>");
                    }
                    html.AppendLine("</tbody>");
                    html.AppendLine("</table>");
                    html.AppendLine("</div>");
                }

                html.AppendLine("</div>");
                html.AppendLine("</body>");
                html.AppendLine("</html>");

                File.WriteAllText(path, html.ToString(), Encoding.UTF8);

                // Automatsko štampanje generisanog HTML izveštaja kada se završava druga smena
                try
                {
                    if (trenutnaSmena == 2)
                    {
                        WebBrowser wb = new WebBrowser();
                        wb.ScriptErrorsSuppressed = true;
                        wb.ScrollBarsEnabled = false;
                        wb.DocumentCompleted += (s, e) =>
                        {
                            try
                            {
                                wb.Print();
                                WriteDebug($"Automatsko štampanje pokrenuto: {path}");
                            }
                            catch (Exception ex)
                            {
                                WriteDebug($"Greška pri automatskom štampanju: {ex.Message}");
                            }
                            finally
                            {
                                wb.Dispose();
                            }
                        };
                        // Navigiraj na fajl (potrebno da bude file URI)
                        wb.Navigate(new Uri(path));
                    }
                }
                catch (Exception ex)
                {
                    WriteDebug($"Greška pri pokretanju WebBrowser za štampu: {ex.Message}");
                }
                if (showMessage)
                {
                    MessageBox.Show($"✅ Dnevni izveštaj je uspešno generisan!\n\n📍 Lokacija: {path}\n\n📎 Izveštaj možete otvoriti u bilo kom pretraživaču (Chrome, Edge, Firefox) i odštampati.", "Uspešno sačuvano", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    WriteDebug($"Dnevni izveštaj je generisan (bez prikaza poruke): {path}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Greška prilikom generisanja izveštaja: " + ex.Message, "Sistemska Greška", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private decimal ParsirajBrojIzStringa(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string cistBroj = new string(text.Where(c => char.IsDigit(c) || c == '-').ToArray());
            return decimal.TryParse(cistBroj, out decimal rez) ? rez : 0;
        }

        private void IzracunajSve_TextChanged(object sender, EventArgs e) => IzracunajSve();

        // Učitaj sačuvanu admin lozinku
        private void UcitajAdminLozinku()
        {
            try
            {
                if (File.Exists(adminLozinkaPath))
                {
                    string sacuvanaLozinka = File.ReadAllText(adminLozinkaPath);
                    if (!string.IsNullOrWhiteSpace(sacuvanaLozinka))
                    {
                        adminLozinka = sacuvanaLozinka;
                    }
                }
            }
            catch { }
        }

        // Sačuvaj admin lozinku
        private void SacuvajAdminLozinku(string novaLozinka)
        {
            try
            {
                File.WriteAllText(adminLozinkaPath, novaLozinka);
                adminLozinka = novaLozinka;
            }
            catch { }
        }

        // Provera admin lozinke
        private bool ProveriAdminLozinku(string poruka = "Unesite admin lozinku za pristup Config folderu:")
        {
            Form lozinkaForm = new Form()
            {
                Text = "ADMIN PRISTUP - AUTORIZACIJA",
                Size = new Size(380, 200),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.FromArgb(17, 24, 39),
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lbl = new Label()
            {
                Text = poruka,
                Location = new Point(20, 20),
                Size = new Size(340, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleLeft
            };

            TextBox txtLozinka = new TextBox()
            {
                Location = new Point(20, 70),
                Size = new Size(330, 35),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 12),
                PasswordChar = '*'
            };

            Button btnOk = new Button()
            {
                Text = "POTVRDI",
                Location = new Point(80, 120),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(10, 108, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            Button btnCancel = new Button()
            {
                Text = "ODUSTANI",
                Location = new Point(200, 120),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            bool odobren = false;

            btnOk.Click += (s, e) =>
            {
                if (txtLozinka.Text == adminLozinka)
                {
                    odobren = true;
                    lozinkaForm.Close();
                }
                else
                {
                    MessageBox.Show("Pogrešna lozinka! Pristup odbijen.", "Greška",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtLozinka.Clear();
                    txtLozinka.Focus();
                }
            };

            btnCancel.Click += (s, e) => { lozinkaForm.Close(); };

            lozinkaForm.Controls.AddRange(new Control[] { lbl, txtLozinka, btnOk, btnCancel });
            lozinkaForm.AcceptButton = btnOk;
            lozinkaForm.ShowDialog();

            return odobren;
        }

        // Zaključaj Config folder
        private void ZakljucajConfigFolder()
        {
            try
            {
                if (Directory.Exists(ConfigFolderPath))
                {
                    File.SetAttributes(ConfigFolderPath, FileAttributes.Hidden);
                }

                System.Threading.Timer timer = new System.Threading.Timer((state) =>
                {
                    try
                    {
                        if (Directory.Exists(ConfigFolderPath))
                        {
                            FileAttributes attrs = File.GetAttributes(ConfigFolderPath);
                            if ((attrs & FileAttributes.Hidden) != FileAttributes.Hidden)
                            {
                                if (this.InvokeRequired)
                                {
                                    this.Invoke(new Action(() =>
                                    {
                                        if (ProveriAdminLozinku("Neko pokušava da pristupi Config folderu! Unesite lozinku:"))
                                        {
                                            System.Threading.Thread.Sleep(5000);
                                            if (Directory.Exists(ConfigFolderPath))
                                            {
                                                File.SetAttributes(ConfigFolderPath, FileAttributes.Hidden);
                                            }
                                        }
                                        else
                                        {
                                            if (Directory.Exists(ConfigFolderPath))
                                            {
                                                File.SetAttributes(ConfigFolderPath, FileAttributes.Hidden);
                                            }
                                        }
                                    }));
                                }
                            }
                        }
                    }
                    catch { }
                }, null, 0, 1000);
            }
            catch (Exception ex)
            {
                WriteDebug($"Greška pri zaključavanju Config foldera: {ex.Message}");
            }
        }

        // Promena admin lozinke
        public void PromeniAdminLozinku()
        {
            Form promenaForm = new Form()
            {
                Text = "PROMENA ADMIN LOZINKE",
                Size = new Size(400, 250),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.FromArgb(17, 24, 39),
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblStara = new Label() { Text = "Stara lozinka:", Location = new Point(20, 25), Size = new Size(100, 25), ForeColor = Color.White };
            TextBox txtStara = new TextBox() { Location = new Point(130, 23), Size = new Size(230, 30), PasswordChar = '*', BackColor = Color.FromArgb(55, 65, 81), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            Label lblNova = new Label() { Text = "Nova lozinka:", Location = new Point(20, 70), Size = new Size(100, 25), ForeColor = Color.White };
            TextBox txtNova = new TextBox() { Location = new Point(130, 68), Size = new Size(230, 30), PasswordChar = '*', BackColor = Color.FromArgb(55, 65, 81), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            Label lblPotvrda = new Label() { Text = "Potvrdi novu:", Location = new Point(20, 115), Size = new Size(100, 25), ForeColor = Color.White };
            TextBox txtPotvrda = new TextBox() { Location = new Point(130, 113), Size = new Size(230, 30), PasswordChar = '*', BackColor = Color.FromArgb(55, 65, 81), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            Button btnSacuvaj = new Button()
            {
                Text = "SAČUVAJ",
                Location = new Point(100, 165),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(10, 108, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            Button btnOdustani = new Button()
            {
                Text = "ODUSTANI",
                Location = new Point(220, 165),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(55, 65, 81),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            btnSacuvaj.Click += (s, e) =>
            {
                if (txtStara.Text != adminLozinka)
                {
                    MessageBox.Show("Pogrešna stara lozinka!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (txtNova.Text != txtPotvrda.Text)
                {
                    MessageBox.Show("Nova lozinka i potvrda se ne poklapaju!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtNova.Text))
                {
                    MessageBox.Show("Lozinka ne može biti prazna!", "Greška", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SacuvajAdminLozinku(txtNova.Text);
                MessageBox.Show("Lozinka je uspešno promenjena!", "Uspešno", MessageBoxButtons.OK, MessageBoxIcon.Information);
                promenaForm.Close();
            };

            btnOdustani.Click += (s, e) => { promenaForm.Close(); };

            promenaForm.Controls.AddRange(new Control[] { lblStara, txtStara, lblNova, txtNova, lblPotvrda, txtPotvrda, btnSacuvaj, btnOdustani });
            promenaForm.ShowDialog();
        }
    }
}