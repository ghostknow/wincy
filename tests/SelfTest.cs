using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Wincy.Tests
{
    /// <summary>
    /// Headless checks for the platform-independent half of Wincy (JSON, settings,
    /// history store, search ranking, shortcut parsing). Run with build.ps1 -Test.
    /// </summary>
    internal static class SelfTest
    {
        private static int _passed;
        private static int _failed;
        private static string _scratch;

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            _scratch = Path.Combine(Path.GetTempPath(), "wincy-selftest-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Paths.UseRoot(_scratch);
            Log.Enabled = false;
            I18n.SetLanguage(I18n.English);

            Section("json", TestJson);
            Section("settings", TestSettings);
            Section("hotkey", TestHotkey);
            Section("fuzzy", TestFuzzy);
            Section("text", TestText);
            Section("history", TestHistory);
            Section("items", TestItems);

            Console.WriteLine();
            Console.WriteLine(_failed == 0
                ? string.Format("PASS — {0} checks", _passed)
                : string.Format("FAIL — {0} passed, {1} failed", _passed, _failed));

            try
            {
                Directory.Delete(_scratch, true);
            }
            catch
            {
            }
            return _failed == 0 ? 0 : 1;
        }

        private static void Section(string name, Action body)
        {
            Console.WriteLine("[" + name + "]");
            try
            {
                body();
            }
            catch (Exception ex)
            {
                Fail("section threw " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Check(bool condition, string label)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("  ok   " + label);
            }
            else
            {
                Fail(label);
            }
        }

        private static void Equal<T>(T expected, T actual, string label)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
            {
                _passed++;
                Console.WriteLine("  ok   " + label);
            }
            else
            {
                Fail(label + " — expected <" + expected + "> got <" + actual + ">");
            }
        }

        private static void Fail(string label)
        {
            _failed++;
            Console.WriteLine("  FAIL " + label);
        }

        private static void TestJson()
        {
            string[] samples = new string[]
            {
                "plain",
                "quote \" and backslash \\",
                "line\nbreak\ttab\r",
                "control \u0001 char",
                "emoji 😀 中文 Ελληνικά",
                "  padded  ",
                "",
                "{\"like\":\"json\"}"
            };
            for (int i = 0; i < samples.Length; i++)
            {
                JsonValue v = JsonValue.FromString(samples[i]);
                string encoded = Json.Write(v, false);
                JsonValue back = Json.Parse(encoded);
                Equal(samples[i], back.Text, "string round trip #" + i);
            }

            JsonValue root = JsonValue.NewObject();
            root.Set("a", 1);
            root.Set("b", true);
            root.Set("c", JsonValue.NewArray().Add(JsonValue.FromNumber(1.5)).Add(JsonValue.FromString("x")));
            root.Set("d", JsonValue.Null());
            JsonValue nested = JsonValue.NewObject();
            nested.Set("inner", "值");
            root.Set("e", nested);

            JsonValue parsed = Json.Parse(Json.Write(root, true));
            Equal(1, parsed.GetInt("a", -1), "object int member");
            Equal(true, parsed.GetBool("b", false), "object bool member");
            Equal("x", parsed.GetArray("c").Items[1].Text, "array member");
            Equal(JsonKind.Null, parsed.Get("d").Kind, "null member");
            Equal("值", parsed.Get("e").GetString("inner", null), "nested member");
            Equal("fallback", parsed.GetString("missing", "fallback"), "missing key fallback");

            bool threw = false;
            try
            {
                Json.Parse("{\"a\": }");
            }
            catch (FormatException)
            {
                threw = true;
            }
            Check(threw, "malformed json throws FormatException");

            threw = false;
            try
            {
                Json.Parse("{\"a\":1} trailing");
            }
            catch (FormatException)
            {
                threw = true;
            }
            Check(threw, "trailing content rejected");
        }

        private static void TestSettings()
        {
            AppSettings settings = new AppSettings();
            settings.HistoryLimit = 123;
            settings.AutoPaste = false;
            settings.PastePlainByDefault = true;
            settings.Theme = "dark";
            settings.Language = I18n.English;
            settings.IgnoreApps = new List<string>(new string[] { "keepass", "1password" });
            settings.Hotkey = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.F9);
            settings.Save();

            AppSettings loaded = AppSettings.Load();
            Equal(123, loaded.HistoryLimit, "history limit persisted");
            Equal(false, loaded.AutoPaste, "autoPaste persisted");
            Equal(true, loaded.PastePlainByDefault, "pastePlainByDefault persisted");
            Equal("dark", loaded.Theme, "theme persisted");
            Equal("Ctrl+Alt+F9", loaded.Hotkey.Display, "hotkey persisted");
            Equal(2, loaded.IgnoreApps.Count, "ignore list persisted");
            Check(loaded.IsAppIgnored("KeePass.exe"), "ignore list is case-insensitive");
            Check(!loaded.IsAppIgnored("notepad.exe"), "ignore list does not over-match");

            File.WriteAllText(Paths.SettingsFile, "{ this is not json");
            AppSettings fallback = AppSettings.Load();
            Equal(200, fallback.HistoryLimit, "corrupt settings fall back to defaults");
            Check(File.Exists(Directory.GetFiles(Paths.Root, "settings.json.corrupt-*")[0]), "corrupt settings backed up");
        }

        private static void TestHotkey()
        {
            HotkeySpec spec = HotkeySpec.Parse("Ctrl+Shift+V");
            Check(spec != null && spec.IsValid, "parse Ctrl+Shift+V");
            Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, spec.Modifiers, "modifiers parsed");
            Equal(Keys.V, spec.Key, "key parsed");
            Equal("Ctrl+Shift+V", spec.Display, "display is canonical");

            HotkeySpec relaxed = HotkeySpec.Parse(" ctrl + alt + f5 ");
            Check(relaxed != null, "parse relaxed spacing/case");
            Equal("Ctrl+Alt+F5", relaxed.Display, "relaxed display");

            Check(HotkeySpec.Parse("V") == null, "modifier-less shortcut rejected");
            Check(HotkeySpec.Parse("Ctrl+Shift") == null, "key-less shortcut rejected");
            Check(HotkeySpec.Parse("") == null, "empty shortcut rejected");

            HotkeySpec num = HotkeySpec.Parse("Win+Num5");
            Check(num != null && num.Key == Keys.NumPad5, "numpad key parsed");

            HotkeySpec arrow = new HotkeySpec(HotkeyModifiers.Control, Keys.Down);
            Equal("Ctrl+↓", arrow.Display, "arrow glyph display");
            HotkeySpec reparsed = HotkeySpec.Parse(arrow.Serialize());
            Check(reparsed != null && reparsed.Key == Keys.Down, "arrow serializes and parses");

            HotkeySpec fromData = HotkeySpec.FromKeyData(Keys.Control | Keys.Shift | Keys.K);
            Equal("Ctrl+Shift+K", fromData.Display, "FromKeyData strips modifier bits");

            uint mods = new HotkeySpec(HotkeyModifiers.Control | HotkeyModifiers.Alt, Keys.V).NativeModifiers;
            Equal(3u, mods, "native modifier bits match MOD_CONTROL|MOD_ALT");
        }

        private static void TestFuzzy()
        {
            MatchResult empty = Fuzzy.Match("anything", "");
            Check(empty.Matched, "empty query matches");

            MatchResult exact = Fuzzy.Match("hello", "hello");
            MatchResult prefix = Fuzzy.Match("hello world", "hello");
            MatchResult middle = Fuzzy.Match("say hello now", "hello");
            MatchResult loose = Fuzzy.Match("Clipboard Manager", "cbm");
            Check(loose.Matched, "subsequence matches");
            Check(exact.Score > prefix.Score, "exact outranks prefix");
            Check(prefix.Score > middle.Score, "prefix outranks substring");
            Check(middle.Score > loose.Score, "substring outranks subsequence");
            Check(!Fuzzy.Match("hello", "zzz").Matched, "no match for absent text");
            Check(Fuzzy.Match("HELLO", "hello").Matched, "case-insensitive");
            Check(Fuzzy.Match("重新整理視窗", "整理").Matched, "CJK substring");

            MatchResult highlight = Fuzzy.Match("say hello now", "hello");
            Equal(4, highlight.Positions[0], "highlight start position");
            Equal(8, highlight.Positions[highlight.Positions.Length - 1], "highlight end position");
        }

        private static void TestText()
        {
            Equal("a b c", TextUtil.SingleLine("a\n\t b   c", 100), "whitespace collapsed");
            Equal("abc…", TextUtil.SingleLine("abcdef", 3), "elision");
            Equal("", TextUtil.SingleLine("   \r\n ", 10), "blank text");
            Equal(40, TextUtil.Sha1Hex("x").Length, "sha1 hex length");
            Equal(TextUtil.Sha1Hex("dup"), TextUtil.Sha1Hex("dup"), "sha1 stable");
        }

        private static void TestHistory()
        {
            Paths.UseRoot(_scratch);
            File.Delete(Paths.HistoryFile);
            HistoryStore store = new HistoryStore(10);

            ClipboardItem a = MakeText("first");
            ClipboardItem b = MakeText("second");
            store.Add(a);
            store.Add(b);
            Equal(2, store.Count, "two items stored");
            Equal("second", store.Snapshot()[0].Text, "newest item first");

            // Duplicate of an older item bubbles to the top instead of being added twice.
            ClipboardItem duplicate = MakeText("first");
            ClipboardItem kept = store.Add(duplicate);
            Equal(2, store.Count, "duplicate not stored twice");
            Equal(a.Id, kept.Id, "duplicate returns existing item");
            Equal("first", store.Snapshot()[0].Text, "duplicate moves to top");

            // Pinning keeps the item above newer unpinned ones and survives trimming.
            store.TogglePin(a.Id);
            Equal(true, store.FindById(a.Id).Pinned, "item pinned");
            for (int i = 0; i < 15; i++) store.Add(MakeText("filler " + i));
            Equal(11, store.Count, "history trimmed to limit plus pinned");
            Equal(a.Id, store.Snapshot()[0].Id, "pinned item stays on top");
            Check(store.FindById(a.Id) != null, "pinned item survived trimming");

            // Order is oldest-last among unpinned items.
            List<ClipboardItem> snap = store.Snapshot();
            Equal("filler 14", snap[1].Text, "newest unpinned right after pinned block");

            // Persistence.
            store.Save();
            HistoryStore reloaded = new HistoryStore(10);
            reloaded.Load();
            Equal(store.Count, reloaded.Count, "history reloaded from disk");
            Equal("filler 14", reloaded.Snapshot()[1].Text, "history order preserved");

            // Delete removes the item.
            string id = reloaded.Snapshot()[1].Id;
            Check(reloaded.Delete(id), "delete returns true");
            Check(reloaded.FindById(id) == null, "deleted item gone");

            // Image blobs are written and removed with the item.
            ClipboardItem image = new ClipboardItem();
            image.Kind = ItemKind.Image;
            image.ImageWidth = 4;
            image.ImageHeight = 4;
            image.Blob = reloaded.SaveImageBlob(image.Id, new byte[] { 137, 80, 78, 71 });
            image.Hash = "img-1";
            reloaded.Add(image);
            string path = reloaded.ImageFullPath(image);
            Check(File.Exists(path), "image blob written");
            reloaded.Delete(image.Id);
            Check(!File.Exists(path), "image blob deleted with item");

            // Lowering the limit trims immediately.
            HistoryStore big = new HistoryStore(100);
            for (int i = 0; i < 40; i++) big.Add(MakeText("bulk " + i));
            Equal(40, big.Count, "bulk items stored");
            big.Limit = 12;
            Equal(12, big.Count, "limit change trims unpinned items");

            reloaded.Clear();
            Equal(0, reloaded.Count, "clear empties history");
            HistoryStore clearedReload = new HistoryStore(10);
            clearedReload.Load();
            Equal(0, clearedReload.Count, "clear persisted");

            File.Delete(Paths.HistoryFile);
        }

        private static void TestItems()
        {
            ClipboardItem text = new ClipboardItem();
            text.Kind = ItemKind.Text;
            text.Text = "{\"json\":\"inside the clipboard\"}\nsecond line";
            text.SourceApp = "chrome";
            ClipboardItem back = ClipboardItem.FromJson(Json.Parse(Json.Write(text.ToJson(), true)));
            Check(back != null, "text item round trip");
            Equal(text.Text, back.Text, "text payload preserved");
            Equal("chrome", back.SourceApp, "source app preserved");
            Check(back.Title.IndexOf("json", StringComparison.Ordinal) >= 0, "title derives from text");

            ClipboardItem files = new ClipboardItem();
            files.Kind = ItemKind.Files;
            files.Files.Add(@"C:\Users\example\Desktop\report.pdf");
            ClipboardItem filesBack = ClipboardItem.FromJson(Json.Parse(Json.Write(files.ToJson(), true)));
            Equal(1, filesBack.Files.Count, "file list round trip");
            Check(filesBack.Title.IndexOf("report.pdf", StringComparison.Ordinal) >= 0, "file title shows name");

            ClipboardItem image = new ClipboardItem();
            image.Kind = ItemKind.Image;
            image.ImageWidth = 1920;
            image.ImageHeight = 1080;
            image.Blob = "images/x.png";
            ClipboardItem imageBack = ClipboardItem.FromJson(Json.Parse(Json.Write(image.ToJson(), true)));
            Equal(1920, imageBack.ImageWidth, "image dimensions round trip");

            Check(ClipboardItem.FromJson(JsonValue.NewObject()) == null, "empty object rejected");

            I18n.SetLanguage(I18n.Chinese);
            Equal(ItemKind.Text, text.Kind, "kind preserved");
            Check(text.Title.Length > 0, "chinese title non-empty");
            Equal("3 個項目", I18n.Count(3), "chinese item count");
            I18n.SetLanguage(I18n.English);
            Equal("1 item", I18n.Count(1), "english singular");
            Equal("2 items", I18n.Count(2), "english plural");
        }

        private static ClipboardItem MakeText(string text)
        {
            ClipboardItem item = new ClipboardItem();
            item.Kind = ItemKind.Text;
            item.Text = text;
            item.Hash = "T" + TextUtil.Sha1Hex(text);
            return item;
        }
    }
}
