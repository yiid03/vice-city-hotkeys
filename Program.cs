using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace ViceCityHotkeys
{
    public sealed class Settings
    {
        public string Language = "en";
        public string[] Codes = { "ASPIRINE", "PRECIOUSPROTECTION", "THUGSTOOLS", "LEAVEMEALONE", "PROFESSIONALTOOLS", "NUTTERTOOLS", "PANZER" };
        public int[] Keys = { 117, 118, 119, 120, 121, 122, 123 };
        public int Delay = 45;

        public static bool IsValid(Settings value)
        {
            if (value == null || (value.Language != "en" && value.Language != "tr") ||
                value.Codes == null || value.Keys == null || value.Codes.Length != 7 || value.Keys.Length != 7 ||
                value.Delay < 25 || value.Delay > 150) return false;
            var used = new HashSet<int>();
            for (int i = 0; i < 7; i++)
                if (value.Keys[i] < 112 || value.Keys[i] > 123 || !used.Add(value.Keys[i]) ||
                    value.Codes[i] == null || !Regex.IsMatch(value.Codes[i], @"\A[A-Z]{1,40}\z")) return false;
            return true;
        }
    }

    public sealed class SettingsStore
    {
        public readonly string Path;
        public SettingsStore(string path) { Path = path; }
        public Settings Read()
        {
            try
            {
                using (var reader = XmlReader.Create(Path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32768 }))
                {
                    var result = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(reader);
                    if (Settings.IsValid(result)) return result;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidOperationException) { }
            catch (XmlException) { }
            return new Settings();
        }
        public void Write(Settings value)
        {
            if (!Settings.IsValid(value)) throw new ArgumentException("Invalid settings.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temporary)) new XmlSerializer(typeof(Settings)).Serialize(stream, value);
                if (File.Exists(Path)) File.Replace(temporary, Path, null);
                else File.Move(temporary, Path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput { internal ushort Key, Scan; internal uint Flags, Time; internal UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal UIntPtr Extra; }
        [StructLayout(LayoutKind.Explicit)] internal struct InputUnion { [FieldOffset(0)] internal KeyboardInput Keyboard; [FieldOffset(0)] internal MouseInput Mouse; }
        [StructLayout(LayoutKind.Sequential)] internal struct Input { internal uint Type; internal InputUnion Data; }
        [StructLayout(LayoutKind.Sequential)] internal struct HookData { internal uint Key, Scan, Flags, Time; internal UIntPtr Extra; }
        internal delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] internal static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);

        // Physical US-letter positions used by the classic game's cheat input.
        internal static readonly ushort[] LetterScans = { 0x1e,0x30,0x2e,0x20,0x12,0x21,0x22,0x23,0x17,0x24,0x25,0x26,0x32,0x31,0x18,0x19,0x10,0x13,0x1f,0x14,0x16,0x2f,0x11,0x2d,0x15,0x2c };
        internal static bool Send(ushort scan, bool release)
        {
            var input = new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Scan = scan, Flags = (uint)(8 | (release ? 2 : 0)) } } };
            return SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input))) == 1;
        }
        internal static bool IsGame(IntPtr window)
        {
            try
            {
                uint id; GetWindowThreadProcessId(window, out id);
                using (var process = Process.GetProcessById((int)id))
                {
                    string name = process.ProcessName.ToLowerInvariant();
                    return name == "gta-vc" || name == "gta_vc";
                }
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }
        internal static bool HasModifier()
        {
            foreach (int key in new[] { 16, 17, 18, 91, 92 }) if (GetAsyncKeyState(key) < 0) return true;
            return false;
        }
    }

    public sealed class MainForm : Form
    {
        readonly SettingsStore store;
        Settings settings;
        readonly TextBox[] codes = new TextBox[7];
        readonly ComboBox[] keys = new ComboBox[7];
        readonly Label[] rowLabels = new Label[7];
        readonly List<Action> translations = new List<Action>();
        readonly CheckBox enabled = new CheckBox();
        readonly ComboBox language = new ComboBox();
        readonly Label status = new Label();
        readonly NumericUpDown delay = new NumericUpDown();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Native.HookProc hookCallback;
        IntPtr hook;
        readonly bool[] pressed = new bool[12], captured = new bool[12];
        string pending = "";
        int position;
        bool keyDown, initializing = true, closing;
        ushort heldScan;
        IntPtr target;
        string statusId = "ready";
        static readonly Color Pink = Color.FromArgb(255, 111, 180), Cyan = Color.FromArgb(93, 219, 226);

        internal MainForm(SettingsStore settingsStore, bool preview, string previewLanguage)
        {
            store = settingsStore; settings = store.Read();
            if (previewLanguage != null) settings.Language = previewLanguage;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(740, 662); MinimumSize = Size;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(23, 25, 37); ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            LabelText("VICE CITY", 28, 22, 440, 42, 25, Pink);
            LabelTranslated("Cheats at your fingertips", "Şifreler bir tuş uzağında", 30, 73, 660, 30, 14, Color.White);
            LabelTranslated("Leave this app open, switch to the game and tap a hotkey.", "Uygulamayı açık bırak, oyuna geç ve kısayola basıp bırak.", 30, 112, 680, 27, 10, Color.LightGray);
            LabelTranslated("Language", "Dil", 500, 24, 95, 25, 10, Color.LightGray);
            SetupCombo(language, 590, 20, 120);
            language.Items.AddRange(new object[] { "English", "Türkçe" });
            language.SelectedIndex = settings.Language == "tr" ? 1 : 0;
            language.SelectedIndexChanged += LanguageChanged;
            LabelTranslated("ACTION", "ETKİ", 30, 158, 180, 25, 9, Cyan);
            LabelTranslated("HOTKEY", "KISAYOL", 240, 158, 90, 25, 9, Cyan);
            LabelTranslated("CHEAT CODE", "ŞİFRE", 370, 158, 320, 25, 9, Cyan);
            string[] en = { "Full health", "Full armor", "Weapon set 1", "Clear wanted level", "Weapon set 2", "Weapon set 3", "Spawn tank" };
            string[] tr = { "Can doldur", "Zırh doldur", "Silah seti 1", "Polisten kurtul", "Silah seti 2", "Silah seti 3", "Tank getir" };
            for (int i = 0; i < 7; i++)
            {
                int y = 195 + i * 40;
                rowLabels[i] = LabelTranslated(en[i], tr[i], 30, y + 3, 200, 29, 10, Color.White);
                keys[i] = new ComboBox(); SetupCombo(keys[i], 240, y, 100);
                for (int j = 1; j <= 12; j++) keys[i].Items.Add("F" + j);
                keys[i].SelectedIndex = settings.Keys[i] - 112;
                codes[i] = new TextBox { Left = 370, Top = y, Width = 340, Text = settings.Codes[i], CharacterCasing = CharacterCasing.Upper, MaxLength = 40, ForeColor = Color.Black, BackColor = Color.White };
                codes[i].AccessibleName = en[i] + " cheat code"; keys[i].AccessibleName = en[i] + " hotkey";
                Controls.Add(codes[i]);
            }
            enabled.SetBounds(30, 492, 195, 30); enabled.Checked = true; Controls.Add(enabled);
            Translate(enabled, "Hotkeys enabled", "Kısayollar etkin");
            LabelTranslated("Key delay (ms)", "Harf süresi (ms)", 240, 495, 122, 26, 9, Color.LightGray);
            delay.SetBounds(370, 491, 72, 30); delay.Minimum = 25; delay.Maximum = 150; delay.Value = settings.Delay;
            delay.ForeColor = Color.Black; delay.BackColor = Color.White; Controls.Add(delay);
            var save = new Button { Left = 485, Top = 487, Width = 225, Height = 37, BackColor = Pink, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat };
            Controls.Add(save); Translate(save, "Save changes", "Değişiklikleri kaydet"); save.Click += delegate { SaveEdits(); };
            status.SetBounds(30, 541, 680, 48); status.ForeColor = Cyan; Controls.Add(status);
            LabelTranslated("Use during gameplay, outside menus or chat. Closing this app disables hotkeys.\nClassic PC version only. No game files are changed.", "Menü veya sohbet dışında, oyun sırasında kullan. Kapatınca kısayollar durur.\nYalnızca klasik PC sürümü içindir. Oyun dosyaları değiştirilmez.", 30, 603, 680, 48, 9, Color.LightGray);
            timer.Interval = settings.Delay; timer.Tick += OnTick;
            if (!preview)
            {
                hookCallback = OnKeyboard;
                hook = Native.SetWindowsHookEx(13, hookCallback, Native.GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero) { enabled.Checked = false; enabled.Enabled = false; statusId = "hookError"; }
            }
            enabled.CheckedChanged += delegate { if (!enabled.Checked) CancelTyping(); statusId = enabled.Checked ? "ready" : "paused"; UpdateStatus(); };
            FormClosing += delegate { closing = true; CancelTyping(); if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook); timer.Dispose(); };
            initializing = false; ApplyLanguage();
        }
        string L(string en, string tr) { return settings.Language == "tr" ? tr : en; }
        Label LabelText(string text, int x, int y, int width, int height, int size, Color color)
        {
            var label = new Label { Text = text, Left = x, Top = y, Width = width, Height = height, Font = new Font("Segoe UI", size), ForeColor = color };
            Controls.Add(label); return label;
        }
        Label LabelTranslated(string en, string tr, int x, int y, int width, int height, int size, Color color)
        {
            var label = LabelText("", x, y, width, height, size, color); Translate(label, en, tr); return label;
        }
        void Translate(Control control, string en, string tr) { translations.Add(delegate { control.Text = L(en, tr); }); }
        void SetupCombo(ComboBox combo, int x, int y, int width)
        {
            combo.SetBounds(x, y, width, 28); combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.ForeColor = Color.Black; combo.BackColor = Color.White; Controls.Add(combo);
        }
        void ApplyLanguage()
        {
            Text = L("Vice City Hotkeys", "Vice City Hotkeys — Şifre Kısayolları");
            foreach (var translate in translations) translate(); UpdateStatus();
        }
        void LanguageChanged(object sender, EventArgs args)
        {
            if (initializing) return;
            settings.Language = language.SelectedIndex == 1 ? "tr" : "en"; ApplyLanguage();
            try { store.Write(settings); }
            catch (Exception error) { ShowSaveError(error); }
        }
        void UpdateStatus()
        {
            switch (statusId)
            {
                case "saved": status.Text = L("Saved. Switch to Vice City to use your hotkeys.", "Kaydedildi. Kısayollarını kullanmak için Vice City'ye geç."); break;
                case "sent": status.Text = L("Cheat typed. Ready for the next hotkey.", "Şifre yazıldı. Sonraki kısayol için hazır."); break;
                case "paused": status.Text = L("Paused. Enable hotkeys to continue.", "Duraklatıldı. Devam etmek için kısayolları etkinleştir."); break;
                case "cancelled": status.Text = L("Typing stopped because focus or modifier keys changed.", "Pencere veya yardımcı tuşlar değiştiği için yazma durdu."); break;
                case "hookError": status.Text = L("Could not start hotkeys. Close and reopen the app.", "Kısayollar başlatılamadı. Uygulamayı kapatıp yeniden aç."); break;
                case "inputError": status.Text = L("Input failed. If the game runs as administrator, run this app the same way.", "Girdi gönderilemedi. Oyun yönetici olarak açıksa bu uygulamayı da öyle aç."); break;
                default: status.Text = L("Ready. Hotkeys work only when classic Vice City is in the foreground.", "Hazır. Kısayollar yalnızca klasik Vice City öndeyken çalışır."); break;
            }
        }
        void SaveEdits()
        {
            var edited = new Settings { Language = settings.Language, Delay = (int)delay.Value };
            for (int i = 0; i < 7; i++) { edited.Keys[i] = keys[i].SelectedIndex + 112; edited.Codes[i] = codes[i].Text.Trim().ToUpperInvariant(); }
            if (!Settings.IsValid(edited))
            {
                MessageBox.Show(this, L("Choose a different F1–F12 key for each row. Codes must contain 1–40 letters, A–Z only.", "Her satır için farklı bir F1–F12 tuşu seç. Şifreler yalnızca A–Z harflerinden oluşmalı (1–40 harf)."), L("Check your settings", "Ayarları kontrol et"), MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }
            try { store.Write(edited); CancelTyping(); settings = edited; timer.Interval = settings.Delay; statusId = enabled.Checked ? "saved" : "paused"; UpdateStatus(); }
            catch (Exception error) { ShowSaveError(error); }
        }
        void ShowSaveError(Exception error)
        {
            MessageBox.Show(this, L("Could not save settings.\n", "Ayarlar kaydedilemedi.\n") + error.Message, L("Save failed", "Kayıt başarısız"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && !closing)
            {
                var value = (Native.HookData)Marshal.PtrToStructure(data, typeof(Native.HookData));
                // Ignore synthesized input and inspect only the twelve function keys.
                if ((value.Flags & 16) == 0 && value.Key >= 112 && value.Key <= 123)
                {
                    int index = (int)value.Key - 112, msg = message.ToInt32();
                    if (msg == 257 || msg == 261)
                    {
                        pressed[index] = false;
                        if (captured[index]) { captured[index] = false; return (IntPtr)1; }
                    }
                    if (msg == 256 || msg == 260)
                    {
                        bool repeated = pressed[index]; pressed[index] = true;
                        if (captured[index]) return (IntPtr)1;
                        int row = Array.IndexOf(settings.Keys, (int)value.Key);
                        if (row >= 0 && enabled.Checked && !Native.HasModifier())
                        {
                            IntPtr window = Native.GetForegroundWindow();
                            if (Native.IsGame(window))
                            {
                                captured[index] = true;
                                if (!repeated && pending.Length == 0)
                                { target = window; pending = settings.Codes[row]; position = 0; timer.Start(); }
                                return (IntPtr)1;
                            }
                        }
                    }
                }
            }
            return Native.CallNextHookEx(hook, code, message, data);
        }
        void OnTick(object sender, EventArgs args)
        {
            if (pending.Length == 0) { timer.Stop(); return; }
            if (target != Native.GetForegroundWindow() || !Native.IsGame(target) || !enabled.Checked || Native.HasModifier())
            { CancelTyping(); statusId = "cancelled"; UpdateStatus(); return; }
            if (keyDown)
            {
                if (!Native.Send(heldScan, true)) { InputFailed(); return; }
                keyDown = false; position++;
                if (position == pending.Length) { timer.Stop(); pending = ""; statusId = "sent"; UpdateStatus(); }
                return;
            }
            foreach (int key in settings.Keys) if (Native.GetAsyncKeyState(key) < 0) return;
            heldScan = Native.LetterScans[pending[position] - 'A'];
            if (!Native.Send(heldScan, false)) { InputFailed(); return; }
            keyDown = true;
        }
        void InputFailed() { CancelTyping(); statusId = "inputError"; UpdateStatus(); }
        void CancelTyping() { timer.Stop(); if (keyDown) { Native.Send(heldScan, true); keyDown = false; } pending = ""; }

        // Used by the local test runner; never installs a keyboard hook.
        internal void VerifyUi(string locale)
        {
            language.SelectedIndex = locale == "tr" ? 1 : 0;
            if (rowLabels[0].Text != (locale == "tr" ? "Can doldur" : "Full health")) throw new Exception("Row translation failed.");
            if (enabled.Text != (locale == "tr" ? "Kısayollar etkin" : "Hotkeys enabled")) throw new Exception("Toggle translation failed.");
            for (int i = 0; i < 7; i++) if (keys[i].SelectedIndex != settings.Keys[i] - 112 || keys[i].Text.Length == 0) throw new Exception("Hotkey selection failed.");
            if (store.Read().Language != locale) throw new Exception("Language persistence failed.");
        }
    }

    internal static class Program
    {
        [STAThread] internal static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--self-test") return SelfTests.Run(args[1]);
            if (args.Length == 3 && args[0] == "--preview" && (args[1] == "en" || args[1] == "tr"))
            {
                using (var form = new MainForm(new SettingsStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.xml")), true, args[1]))
                {
                    form.Show(); Application.DoEvents();
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save(args[2]); }
                }
                return 0;
            }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var store = new SettingsStore(System.IO.Path.Combine(local, "ViceCityHotkeys", "settings.xml"));
            bool created;
            using (var mutex = new Mutex(true, @"Local\ViceCityHotkeys", out created))
            {
                if (!created) { MessageBox.Show(store.Read().Language == "tr" ? "Vice City Hotkeys zaten açık." : "Vice City Hotkeys is already running.", "Vice City Hotkeys"); return 0; }
                try
                {
                    // Preserve preferences from the original portable version on first launch.
                    string legacy = System.IO.Path.Combine(local, "ViceCityKisayol", "settings.xml");
                    if (!File.Exists(store.Path) && File.Exists(legacy))
                    { try { store.Write(new SettingsStore(legacy).Read()); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                    using (var form = new MainForm(store, false, null)) Application.Run(form);
                }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        }
    }
}
