namespace ShotLink.Core;

/// <param name="Text">QR の中身そのまま</param>
/// <param name="ReadAt">読んだ時刻</param>
/// <param name="Photo">どの写真から読んだか</param>
public sealed record HistoryEntry(string Text, DateTimeOffset ReadAt, string Photo);

/// <summary>読んだ QR の履歴（新しい順）。メニューの「最近読んだ QR」と history.json の元</summary>
public sealed class History
{
    /// <summary>
    /// 残すのは 100 件まで。メニューに出すのは 10 件で、残りは history.json を開いて見返すため。
    /// 1件は百数十バイトなので、100 件でも読み書きは一瞬
    /// </summary>
    public const int Capacity = 100;

    /// <summary>メニューに出す件数（仕様書 5章）</summary>
    public const int MenuCount = 10;

    readonly JsonFileStore<HistoryEntry[]> _store;
    readonly Lock _lock = new();
    List<HistoryEntry> _entries;

    public History(string path)
    {
        _store = new JsonFileStore<HistoryEntry[]>(path, () => []);
        // 手で直した JSON の null の行は捨てる
        _entries = _store.Load().Where(e => e is { Text: not null }).Take(Capacity).ToList();
    }

    public event Action? Changed;

    public IReadOnlyList<HistoryEntry> Recent(int count)
    {
        lock (_lock)
        {
            return _entries.Take(count).ToArray();
        }
    }

    public void Add(HistoryEntry entry)
    {
        lock (_lock)
        {
            // 同じ中身は1件にまとめて先頭へ。メニューに同じ URL が並ぶと、ほかの物が押し出される
            _entries.RemoveAll(e => e.Text == entry.Text);
            _entries.Insert(0, entry);
            if (_entries.Count > Capacity)
            {
                _entries.RemoveRange(Capacity, _entries.Count - Capacity);
            }

            _store.Save(_entries.ToArray());
        }

        Changed?.Invoke();
    }
}
