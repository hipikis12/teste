namespace FolhasCipo.Telas;

/// <summary>Atalhos para montar telas em código (sem designer).</summary>
static class Ui
{
    public static DataGridView Grade() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window,
    };

    public static DataGridViewColumn Col(string prop, string titulo, int largura, bool preencher = false)
    {
        var c = new DataGridViewTextBoxColumn { DataPropertyName = prop, HeaderText = titulo, Width = largura };
        if (preencher) c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        return c;
    }

    public static Button Botao(string texto, EventHandler click, int largura = 0)
    {
        var b = new Button { Text = texto, AutoSize = largura == 0, Height = 30, Margin = new Padding(3) };
        if (largura > 0) b.Width = largura;
        b.Click += click;
        return b;
    }

    public static FlowLayoutPanel Barra(params Control[] cs)
    {
        var f = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(4), WrapContents = false, AutoScroll = true };
        foreach (var c in cs)
        {
            c.Margin = new Padding(3, 5, 3, 3);
            f.Controls.Add(c);
        }
        return f;
    }

    public static void Erro(string msg) => MessageBox.Show(msg, "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>Formulário simples em duas colunas (rótulo + campo).</summary>
    public static TableLayoutPanel Formulario(params (string rotulo, Control campo)[] campos)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10), AutoScroll = true };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (r, c) in campos)
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(new Label { Text = r, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) });
            c.Dock = DockStyle.Fill;
            t.Controls.Add(c);
        }
        return t;
    }
}
