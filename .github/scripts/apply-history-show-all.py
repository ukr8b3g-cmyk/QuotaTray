from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8-sig")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one match, found {count}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


store_method = '''    public async Task<IReadOnlyList<string>> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        var records = new List<(DateTimeOffset Time, string Display)>();
        foreach (var file in Directory.EnumerateFiles(
                     _historyDirectory,
                     "*.jsonl"))
        {
            await foreach (var line in File.ReadLinesAsync(file, cancellationToken))
            {
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var time = root.GetProperty("observedToUtc").GetDateTimeOffset();
                    var classification = root.GetProperty("classification").GetString();
                    records.Add(
                        (time, $"{time.ToLocalTime():g}  {classification}"));
                }
                catch (Exception exception) when (
                    exception is JsonException or KeyNotFoundException or
                    InvalidOperationException or FormatException)
                {
                    // Skip only the damaged or unsupported row.
                }
            }
        }

        return records
            .OrderByDescending(record => record.Time)
            .Select(record => record.Display)
            .ToArray();
    }

'''
replace_once(
    "src/QuantaTrain.Infrastructure/JsonlHistoryStore.cs",
    "    public async Task<IReadOnlyList<ResetEvent>> ReadRecentEventsAsync(\n",
    store_method + "    public async Task<IReadOnlyList<ResetEvent>> ReadRecentEventsAsync(\n",
)

replace_once(
    "src/QuantaTrain.App/DetailDashboardForm.cs",
    "    public event EventHandler? SettingsRequested;\n",
    "    public event EventHandler? SettingsRequested;\n"
    "    public event EventHandler? HistoryRequested;\n",
)
replace_once(
    "src/QuantaTrain.App/DetailDashboardForm.cs",
    "        allHistory.Bounds = new Rectangle(198, 229, 160, 28);\n"
    "        history.Controls.Add(allHistory);\n",
    "        allHistory.Bounds = new Rectangle(198, 229, 160, 28);\n"
    "        allHistory.Click += (_, _) =>\n"
    "            HistoryRequested?.Invoke(this, EventArgs.Empty);\n"
    "        history.Controls.Add(allHistory);\n",
)

replace_once(
    "src/QuantaTrain.App/QuantaTrainContext.cs",
    "            _detailForm.SettingsRequested += (_, _) => QueueShowSettings();\n"
    "            _detailForm.MoveCompleted += async (_, _) =>\n",
    "            _detailForm.SettingsRequested += (_, _) => QueueShowSettings();\n"
    "            _detailForm.HistoryRequested += async (_, _) =>\n"
    "                await ResetHistoryDialog.ShowAsync(\n"
    "                    _detailForm,\n"
    "                    _historyStore,\n"
    "                    _localizer,\n"
    "                    _lifetime.Token);\n"
    "            _detailForm.MoveCompleted += async (_, _) =>\n",
)
