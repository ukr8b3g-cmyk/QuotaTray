using System.Diagnostics;

namespace QuantaTrain.App;

internal sealed class AboutForm : Form
{
    internal const string RepositoryUrl =
        "https://github.com/ukr8b3g-cmyk/QuotaTray";

    private readonly Bitmap _informationImage;

    public AboutForm(LocalizationService localizer, string version)
    {
        Text = "QuantaTray";
        ClientSize = new Size(390, 170);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        AccessibleName = "QuantaTray information";

        _informationImage = SystemIcons.Information.ToBitmap();
        var icon = new PictureBox
        {
            Image = _informationImage,
            Location = new Point(22, 25),
            Size = new Size(36, 36),
            SizeMode = PictureBoxSizeMode.Zoom,
        };
        var product = new Label
        {
            Text = $"QuantaTray {version}",
            Location = new Point(72, 25),
            AutoSize = true,
        };
        var unofficial = new Label
        {
            Text = localizer.Text("Settings.Unofficial"),
            Location = new Point(72, 49),
            AutoSize = true,
        };
        var repository = new LinkLabel
        {
            Text = $"{localizer.Text("Settings.Repository")}: ukr8b3g-cmyk/QuotaTray",
            Location = new Point(72, 78),
            AutoSize = true,
            AccessibleName = localizer.Text("Settings.Repository"),
        };
        repository.Links.Add(0, repository.Text.Length, RepositoryUrl);
        repository.LinkClicked += (_, eventArgs) =>
        {
            var url = eventArgs.Link?.LinkData?.ToString() ?? RepositoryUrl;
            try
            {
                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    "QuantaTray",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        };

        var ok = new Button
        {
            Text = "OK",
            Bounds = new Rectangle(282, 127, 88, 28),
            DialogResult = DialogResult.OK,
        };
        Controls.AddRange([icon, product, unofficial, repository, ok]);
        AcceptButton = ok;
        CancelButton = ok;
        PanelPlacement.CenterOnPrimary(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _informationImage.Dispose();
        }
        base.Dispose(disposing);
    }
}
