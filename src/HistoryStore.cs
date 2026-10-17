using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Wincy
{
    /// <summary>
    /// Clipboard history: ordered list (pinned first, then newest first), persisted as
    /// JSON next to PNG blobs for image items.
    /// </summary>
    internal sealed class HistoryStore
    {
        private readonly object _gate = new object();
        private readonly List<ClipboardItem> _items = new List<ClipboardItem>();
        private int _limit;

        public event EventHandler Changed;

        public HistoryStore(int limit)
        {
            _limit = Math.Max(AppSettings.MinHistoryLimit, limit);
        }

        public int Limit
        {
            get { return _limit; }
            set
            {
                bool trimmed;
                lock (_gate)
                {
                    _limit = Math.Max(AppSettings.MinHistoryLimit, value);
                    trimmed = TrimLocked();
                }
                if (trimmed) Save();
                RaiseChanged();
            }
        }

        public int Count
        {
            get
            {
                lock (_gate) return _items.Count;
            }
        }

        /// <summary>Copy of the current items, safe to enumerate on the UI thread.</summary>
        public List<ClipboardItem> Snapshot()
        {
            lock (_gate) return new List<ClipboardItem>(_items);
        }

        public ClipboardItem FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            lock (_gate)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Id, id, StringComparison.Ordinal)) return _items[i];
                }
            }
            return null;
        }

        public void Load()
        {
            List<ClipboardItem> loaded = new List<ClipboardItem>();
            try
            {
                string text = Paths.ReadAllText(Paths.HistoryFile);
                if (!string.IsNullOrEmpty(text))
                {
                    JsonValue root = Json.Parse(text);
                    JsonValue arr = root.GetArray("items");
                    if (arr != null)
                    {
                        for (int i = 0; i < arr.Items.Count; i++)
                        {
                            ClipboardItem item = ClipboardItem.FromJson(arr.Items[i]);
                            if (item != null) loaded.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not read history.json, starting with an empty history", ex);
                TryBackupCorrupt(Paths.HistoryFile);
            }

            lock (_gate)
            {
                _items.Clear();
                _items.AddRange(loaded);
                NormalizeLocked();
                TrimLocked();
            }
            PruneOrphans();
        }

        public void Save()
        {
            JsonValue root = JsonValue.NewObject();
            List<ClipboardItem> copy;
            lock (_gate)
            {
                root.Set("version", 1);
                JsonValue arr = JsonValue.NewArray();
                copy = new List<ClipboardItem>(_items);
                for (int i = 0; i < copy.Count; i++) arr.Add(copy[i].ToJson());
                root.Set("items", arr);
            }
            try
            {
                Paths.WriteAllTextAtomic(Paths.HistoryFile, Json.Write(root, true) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Log.Error("Could not write history.json", ex);
            }
        }

        /// <summary>
        /// Stores a capture. Returns the item kept in the history (a previous copy when the
        /// content is a duplicate) or null when the item was rejected.
        /// </summary>
        public ClipboardItem Add(ClipboardItem item)
        {
            if (item == null) return null;
            if (item.Kind == ItemKind.Text && string.IsNullOrEmpty(item.Text)) return null;
            if (item.Kind == ItemKind.Files && (item.Files == null || item.Files.Count == 0)) return null;
            if (item.Kind == ItemKind.Image && string.IsNullOrEmpty(item.Blob)) return null;

            ClipboardItem result;
            lock (_gate)
            {
                ClipboardItem existing = null;
                if (!string.IsNullOrEmpty(item.Hash))
                {
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (string.Equals(_items[i].Hash, item.Hash, StringComparison.Ordinal))
                        {
                            existing = _items[i];
                            break;
                        }
                    }
                }
                if (existing != null)
                {
                    existing.CreatedUtc = item.CreatedUtc;
                    if (!string.IsNullOrEmpty(item.SourceApp)) existing.SourceApp = item.SourceApp;
                    if (!string.IsNullOrEmpty(item.SourcePath)) existing.SourcePath = item.SourcePath;
                    // Refresh rich-text payloads that may now be available.
                    if (!string.IsNullOrEmpty(item.Html)) existing.Html = item.Html;
                    if (!string.IsNullOrEmpty(item.Rtf)) existing.Rtf = item.Rtf;
                    MoveToGroupTopLocked(existing);
                    result = existing;
                }
                else
                {
                    _items.Insert(item.Pinned ? 0 : PinnedCountLocked(), item);
                    result = item;
                }
                TrimLocked();
            }
            Save();
            RaiseChanged();
            return result;
        }

        public bool Delete(string id)
        {
            ClipboardItem removed = null;
            lock (_gate)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Id, id, StringComparison.Ordinal))
                    {
                        removed = _items[i];
                        _items.RemoveAt(i);
                        break;
                    }
                }
            }
            if (removed == null) return false;
            DeleteBlob(removed);
            Save();
            RaiseChanged();
            return true;
        }

        public bool TogglePin(string id)
        {
            ClipboardItem item = FindById(id);
            if (item == null) return false;
            lock (_gate)
            {
                item.Pinned = !item.Pinned;
                if (item.Pinned)
                {
                    _items.Remove(item);
                    _items.Insert(0, item);
                }
                else
                {
                    _items.Remove(item);
                    _items.Insert(PinnedCountLocked(), item);
                }
                TrimLocked();
            }
            Save();
            RaiseChanged();
            return true;
        }

        public void MoveToTop(string id)
        {
            lock (_gate)
            {
                ClipboardItem item = null;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Id, id, StringComparison.Ordinal))
                    {
                        item = _items[i];
                        break;
                    }
                }
                if (item == null) return;
                item.CreatedUtc = DateTime.UtcNow;
                MoveToGroupTopLocked(item);
            }
            Save();
            RaiseChanged();
        }


        public void Clear()
        {
            List<ClipboardItem> removed;
            lock (_gate)
            {
                removed = new List<ClipboardItem>(_items);
                _items.Clear();
            }
            for (int i = 0; i < removed.Count; i++) DeleteBlob(removed[i]);
            Save();
            RaiseChanged();
        }

        public string ImageFullPath(ClipboardItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Blob)) return null;
            return Path.Combine(Paths.Root, item.Blob.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>Persist PNG bytes for an image item and point the item at the file.</summary>
        public string SaveImageBlob(string itemId, byte[] pngBytes)
        {
            Paths.EnsureDirectories();
            string relative = "images/" + itemId + ".png";
            string full = Path.Combine(Paths.Root, relative.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(full, pngBytes);
            return relative;
        }

        public void PruneOrphans()
        {
            try
            {
                if (!Directory.Exists(Paths.ImageDir)) return;
                HashSet<string> referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                lock (_gate)
                {
                    for (int i = 0; i < _items.Count; i++)
                    {
                        string blob = _items[i].Blob;
                        if (string.IsNullOrEmpty(blob)) continue;
                        referenced.Add(Path.GetFileName(blob));
                    }
                }
                DateTime cutoff = DateTime.UtcNow.AddMinutes(-2);
                foreach (string file in Directory.GetFiles(Paths.ImageDir, "*.png"))
                {
                    if (referenced.Contains(Path.GetFileName(file))) continue;
                    if (File.GetLastWriteTimeUtc(file) > cutoff) continue;
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("PruneOrphans failed", ex);
            }
        }

        private void DeleteBlob(ClipboardItem item)
        {
            string path = ImageFullPath(item);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not delete blob " + path + ": " + ex.Message);
            }
        }

        private int PinnedCountLocked()
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Pinned) count++;
            }
            return count;
        }

        private void MoveToGroupTopLocked(ClipboardItem item)
        {
            int index = _items.IndexOf(item);
            if (index < 0) return;
            _items.RemoveAt(index);
            if (item.Pinned) _items.Insert(0, item);
            else _items.Insert(PinnedCountLocked(), item);
        }

        /// <summary>Stable partition: pinned items first, everything else keeps its order.</summary>
        private void NormalizeLocked()
        {
            List<ClipboardItem> pinned = new List<ClipboardItem>();
            List<ClipboardItem> rest = new List<ClipboardItem>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Pinned) pinned.Add(_items[i]);
                else rest.Add(_items[i]);
            }
            _items.Clear();
            _items.AddRange(pinned);
            _items.AddRange(rest);
        }

        private bool TrimLocked()
        {
            List<ClipboardItem> doomed = new List<ClipboardItem>();
            int unpinned = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Pinned) continue;
                unpinned++;
                if (unpinned > _limit) doomed.Add(_items[i]);
            }
            if (doomed.Count == 0) return false;
            for (int i = 0; i < doomed.Count; i++) _items.Remove(doomed[i]);
            for (int i = 0; i < doomed.Count; i++) DeleteBlob(doomed[i]);
            return true;
        }

        private void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler == null) return;
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error("HistoryStore.Changed handler failed", ex);
            }
        }

        private static void TryBackupCorrupt(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                File.Copy(path, backup, true);
                Log.Warn("Backed up unreadable history to " + backup);
            }
            catch
            {
            }
        }

        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            lock (_gate)
            {
                sb.Append(_items.Count).Append(" items, limit ").Append(_limit);
            }
            return sb.ToString();
        }
    }
}
