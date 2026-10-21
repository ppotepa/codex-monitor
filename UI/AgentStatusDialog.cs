namespace CodexMonitor;

internal sealed class AgentStatusDialog : Form
{
    public AgentStatusDialog(AgentCatalogSnapshot catalog, AgentSelection selection)
    {
        Text = "Installed AI agents";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScroll = true;
        ClientSize = new Size(680, Math.Min(620, 150 + catalog.Agents.Count * 52));
        Font = new Font("Segoe UI", 9.5f);

        Controls.Add(new Label
        {
            Text = "Detected command-line agents and the data source Codex Monitor would use",
            Bounds = new Rectangle(18, 16, 640, 26),
            Font = new Font(Font, FontStyle.Bold)
        });

        var y = 56;
        foreach (var agent in catalog.Agents)
        {
            var selected = agent.IsInstalled && string.Equals(agent.Definition.Id,
                selection.Plugin.Info.Id, StringComparison.OrdinalIgnoreCase);
            var state = agent.IsInstalled
                ? selected ? "Selected · supported" : agent.IsSupported ? "Detected · supported" : "Detected · adapter not available"
                : "Not detected";
            var color = selected ? Color.DarkGreen : agent.IsInstalled && !agent.IsSupported ? Color.DarkOrange : Color.DimGray;
            Controls.Add(new Label
            {
                Text = agent.Definition.DisplayName,
                Bounds = new Rectangle(18, y, 175, 23),
                Font = new Font(Font, FontStyle.Bold)
            });
            Controls.Add(new Label
            {
                Text = state,
                ForeColor = color,
                Bounds = new Rectangle(198, y, 235, 23)
            });
            Controls.Add(new Label
            {
                Text = agent.IsInstalled ? $"{agent.ExecutablePath}\n{agent.Definition.DataSource}" : agent.Definition.DataSource,
                ForeColor = Color.DimGray,
                Bounds = new Rectangle(438, y - 3, 218, 40),
                AutoEllipsis = true
            });
            y += 52;
        }

        Controls.Add(new Label
        {
            Text = $"Selection: {selection.Plugin.Info.DisplayName} · {selection.Reason}",
            ForeColor = Color.DimGray,
            Bounds = new Rectangle(18, y + 6, 640, 34),
            AutoEllipsis = true
        });
        var close = new Button { Text = "Close", DialogResult = DialogResult.OK, Bounds = new Rectangle(556, y + 48, 100, 32) };
        Controls.Add(close);
        AcceptButton = close;
    }
}
