using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ViceCityHotkeys
{
    internal static class SelfTests
    {
        internal static int Run(string report)
        {
            var results = new List<string>();
            string folder = Path.Combine(Path.GetTempPath(), "ViceCityHotkeysTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                Check(Settings.IsValid(new Settings()), "Default settings", results);
                var s = new Settings(); s.Keys[1] = s.Keys[0]; Check(!Settings.IsValid(s), "Duplicate keys rejected", results);
                s = new Settings(); s.Keys[0] = 111; Check(!Settings.IsValid(s), "Out-of-range key rejected", results);
                s = new Settings(); s.Delay = 24; Check(!Settings.IsValid(s), "Too-short delay rejected", results);
                s = new Settings(); s.Delay = 151; Check(!Settings.IsValid(s), "Too-long delay rejected", results);
                foreach (string code in new[] { "", "123", "CODE\n", "LOWERcase", new string('A', 41), "ŞİFRE" })
                { s = new Settings(); s.Codes[0] = code; Check(!Settings.IsValid(s), "Invalid code rejected: " + code.Replace("\n", "\\n"), results); }
                s = new Settings(); s.Language = "de"; Check(!Settings.IsValid(s), "Unknown language rejected", results);
                s = new Settings(); s.Codes = null; Check(!Settings.IsValid(s), "Missing codes rejected", results);
                var store = new SettingsStore(Path.Combine(folder, "settings.xml"));
                Check(store.Read().Language == "en", "English default", results);
                s = new Settings { Language = "tr", Delay = 90 }; s.Codes[0] = "PANZER";
                store.Write(s);
                Check(store.Read().Language == "tr" && store.Read().Delay == 90 && store.Read().Codes[0] == "PANZER", "Settings round trip", results);
                s.Language = "en"; store.Write(s); Check(store.Read().Language == "en", "Atomic settings replacement", results);
                File.WriteAllText(store.Path, "<broken"); Check(Settings.IsValid(store.Read()), "Corrupt XML falls back safely", results);
                File.WriteAllText(store.Path, "<!DOCTYPE Settings [<!ENTITY x SYSTEM 'file:///does-not-exist'>]><Settings><Language>&x;</Language></Settings>");
                Check(store.Read().Language == "en", "External entities rejected", results);
                File.Delete(store.Path); store.Write(new Settings());
                using (var form = new MainForm(store, true, null))
                {
                    form.Show(); System.Windows.Forms.Application.DoEvents();
                    form.VerifyUi("tr"); form.VerifyUi("en");
                    Check(true, "Live EN/TR switch, persisted language, visible default hotkeys", results);
                }
                Check(Marshal.SizeOf(typeof(Native.Input)) == (IntPtr.Size == 8 ? 40 : 28), "Native input layout", results);
                Check(Native.LetterScans.Length == 26 && new HashSet<ushort>(Native.LetterScans).Count == 26, "Unique letter scan codes", results);
                Check(!Native.IsGame(IntPtr.Zero), "Non-game window rejected", results);
                results.Add("Game integration: not tested against a running copy of Vice City.");
                File.WriteAllLines(report, results); return 0;
            }
            catch (Exception error) { results.Add("FAIL: " + error); File.WriteAllLines(report, results); return 1; }
            finally
            {
                // Remove only this run's known files, never a recursive user-data path.
                string path = Path.Combine(folder, "settings.xml");
                if (File.Exists(path)) File.Delete(path);
                if (Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder);
            }
        }
        static void Check(bool condition, string name, List<string> report)
        { if (!condition) throw new Exception(name); report.Add("PASS: " + name); }
    }
}
