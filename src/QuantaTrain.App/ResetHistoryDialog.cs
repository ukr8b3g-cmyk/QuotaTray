using QuantaTrain.Infrastructure;

namespace QuantaTrain.App;

internal static class ResetHistoryDialog
{
    public static async Task ShowAsync(
        IWin32Window owner,
        JsonlHistoryStore historyStore,
        LocalizationService localizer,
        CancellationToken cancellationToken)
    {
        try
        {
            var history = await historyStore.ReadAllAsync(cancellationToken);
            using var form = BuildForm(localizer, history);
            form.ShowDialog(owner);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                owner,
                exception.Message,
                "QuantaTray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static Form BuildForm(
        LocalizationService localizer,
        IReadOnlyList<string> history)
    {
        var form = new Form
        {
            Text = $"{localizer.Text("History.Title")} — QuantaTray",
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(620, 480),
            MinimumSize = new Size(480, 360),
            BackColor = Theme.Window,
            ForeColor = Theme.Text,
            AutoScaleMode = AutoScaleMode.Dpi,
            MinimizeBox = false,
        };

        var title = UiFactory.Label(
            localizer.Text("History.Title"),
            new Point(18, 14),
            11F,
            FontStyle.Bold);
        title.AutoSize = false;
        title.Dock = DockStyle.Top;
        title.Height = 48;
        title.Padding = new Padding(18, 0, 18, 0);
        title.TextAlign = ContentAlignment.MiddleLeft;

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.Ui(9F),
            IntegralHeight = false,
            HorizontalScrollbar = true,
        };
        list.Items.AddRange(
            history.Count == 0
                ? [localizer.Text("History.Empty")]
                : history.Cast<object>().ToArray());

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = Theme.Window,
            Padding = new Padding(0, 10, 16, 10),
        };
        var close = UiFactory.TextButton(
            localizer.Text("Common.Close"),
            new Rectangle(0, 0, 104, 32));
        close.Dock = DockStyle.Right;
        close.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(close);

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 0, 18, 0),
            BackColor = Theme.Window,
        };
        content.Controls.Add(list);
        form.Controls.Add(content);
        form.Controls.Add(footer);
        form.Controls.Add(title);
        form.CancelButton = close;
        return form;
    }
}
