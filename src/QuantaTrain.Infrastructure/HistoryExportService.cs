using System.Text.Json;

namespace QuantaTrain.Infrastructure;

public static class HistoryExportService
{
    public static async Task ExportJsonAsync(
        string historyDirectory,
        string destination,
        CancellationToken cancellationToken)
    {
        var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous))
            {
                using var writer = new Utf8JsonWriter(
                    stream, new JsonWriterOptions { Indented = true });
                writer.WriteStartArray();
                foreach (var path in Directory.EnumerateFiles(historyDirectory, "*.jsonl")
                             .OrderBy(item => item, StringComparer.Ordinal))
                {
                    await foreach (var line in File.ReadLinesAsync(path, cancellationToken))
                    {
                        try
                        {
                            using var document = JsonDocument.Parse(line);
                            document.RootElement.WriteTo(writer);
                        }
                        catch (JsonException)
                        {
                            // Preserve valid history when individual rows are damaged.
                        }
                    }
                }
                writer.WriteEndArray();
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
