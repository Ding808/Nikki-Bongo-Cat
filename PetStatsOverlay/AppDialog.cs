namespace PetStatsOverlay;

/// <summary>Product dialogs use the selected language, including their action buttons.</summary>
internal static class AppDialog
{
    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => Show(null, text, caption, buttons, icon);

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        using var dialog = new Form
        {
            Text = caption,
            ClientSize = new Size(540, 250),
            MinimumSize = new Size(420, 240),
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            TopMost = true,
            Font = new Font("Microsoft YaHei UI", 9F),
            AutoScaleMode = AutoScaleMode.Dpi,
            BackColor = Color.FromArgb(255, 247, 250),
            ForeColor = Color.FromArgb(126, 70, 92)
        };
        var message = new TextBox
        {
            Text = text,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            BackColor = dialog.BackColor,
            ForeColor = dialog.ForeColor,
            Location = new Point(22, 20),
            Size = new Size(496, 164),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Padding(10),
            FlowDirection = FlowDirection.RightToLeft
        };
        if (buttons == MessageBoxButtons.OKCancel)
        {
            var cancel = new Button { Text = L.Pick("Cancel", "取消"), Width = 100, Height = 30, DialogResult = DialogResult.Cancel };
            actions.Controls.Add(cancel);
            dialog.CancelButton = cancel;
        }
        var ok = new Button { Text = L.Pick("OK", "确定"), Width = 100, Height = 30, DialogResult = DialogResult.OK };
        actions.Controls.Add(ok);
        dialog.AcceptButton = ok;
        dialog.Controls.AddRange([message, actions]);
        dialog.Shown += (_, _) => ok.Focus();
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}
