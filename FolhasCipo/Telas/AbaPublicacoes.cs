namespace FolhasCipo.Telas;

/// <summary>Revisão das publicações extraídas de cada policial (editar, excluir, incluir manualmente).</summary>
public class AbaPublicacoes : UserControl
{
    readonly Database _db;
    readonly ListBox _lista = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly TextBox _filtro = new() { Dock = DockStyle.Top, PlaceholderText = "Filtrar policial" };
    readonly DataGridView _grid = Ui.Grade();
    readonly TextBox _previa = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f) };
    readonly ComboBox _ano = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    List<Policial> _policiais = new();
    List<Publicacao> _pubs = new();

    public AbaPublicacoes(Database db)
    {
        _db = db;
        _grid.Columns.AddRange(
            Ui.Col("BiNumero", "BI", 45), Ui.Col("BiData", "Data", 85), Ui.Col("Rotulo", "Item", 190),
            Ui.Col("Previa", "Conteúdo", 500, true));
        _grid.Columns[1].DefaultCellStyle.Format = "dd/MM/yyyy";
        _grid.SelectionChanged += (_, _) => MostrarPrevia();
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Editar(); };
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _pubs.Count && _grid.Rows[e.RowIndex].DataBoundItem is Publicacao p && (p.Manual || p.Editada))
                e.CellStyle!.ForeColor = Color.DarkBlue;
        };
        _lista.SelectedIndexChanged += (_, _) => CarregarPublicacoes();
        _filtro.TextChanged += (_, _) => FiltrarLista();
        _ano.SelectedIndexChanged += (_, _) => CarregarPublicacoes();

        var esquerda = new Panel { Dock = DockStyle.Left, Width = 290, Padding = new Padding(4) };
        esquerda.Controls.Add(_lista);
        esquerda.Controls.Add(_filtro);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 330 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_previa);

        var barra = Ui.Barra(
            new Label { Text = "Ano:", AutoSize = true }, _ano,
            Ui.Botao("Nova publicação manual", (_, _) => Nova()),
            Ui.Botao("Editar", (_, _) => Editar()),
            Ui.Botao("Excluir", (_, _) => Excluir()),
            new Label { Text = "Em azul: editadas ou incluídas à mão (não são apagadas ao reprocessar o BI).", AutoSize = true, ForeColor = Color.DarkBlue });

        var direita = new Panel { Dock = DockStyle.Fill };
        direita.Controls.Add(split);
        direita.Controls.Add(barra);

        Controls.Add(direita);
        Controls.Add(esquerda);
    }

    public void Recarregar()
    {
        var anos = _db.ListarBoletins().Select(b => b.Ano).Distinct().OrderByDescending(a => a).ToList();
        if (anos.Count == 0) anos.Add(DateTime.Today.Year);
        var atual = _ano.SelectedItem;
        _ano.Items.Clear();
        _ano.Items.Add("Todos");
        foreach (var a in anos) _ano.Items.Add(a);
        _ano.SelectedItem = atual != null && _ano.Items.Contains(atual) ? atual : anos[0];
        _policiais = _db.ListarPoliciais();
        FiltrarLista();
    }

    void FiltrarLista()
    {
        var sel = _lista.SelectedItem as Policial;
        var f = Texto.Norm(_filtro.Text);
        _lista.DataSource = _policiais.Where(p => f.Length == 0 || Texto.Norm(p.Nome + " " + p.NomeGuerra + " " + p.Matricula).Contains(f)).ToList();
        if (sel != null)
            foreach (var item in _lista.Items)
                if (item is Policial p && p.Id == sel.Id) { _lista.SelectedItem = item; break; }
    }

    Policial? PolicialAtual => _lista.SelectedItem as Policial;
    Publicacao? PubAtual => _grid.CurrentRow?.DataBoundItem as Publicacao;

    void CarregarPublicacoes()
    {
        if (PolicialAtual is not { } p) { _grid.DataSource = null; return; }
        _pubs = _db.ListarPublicacoes(p.Id);
        if (_ano.SelectedItem is int ano) _pubs = _pubs.Where(x => x.BiAno == ano).ToList();
        _grid.DataSource = _pubs;
        MostrarPrevia();
    }

    void MostrarPrevia()
    {
        if (PubAtual is not { } pub) { _previa.Text = ""; return; }
        _previa.Text = $"BI Nº {pub.BiNumero:00}/{pub.BiAno} de {pub.BiData:dd/MM/yyyy} — {pub.Rotulo}\r\n\r\n" +
                       FormPublicacao.BlocosParaTexto(pub.Blocos).Replace("\n", "\r\n");
    }

    void Nova()
    {
        if (PolicialAtual is not { } p) { Ui.Erro("Selecione o policial."); return; }
        var bis = _db.ListarBoletins();
        if (bis.Count == 0) { Ui.Erro("Importe ao menos um BI antes."); return; }
        var pub = new Publicacao { PolicialId = p.Id, Manual = true, Rotulo = "PUBLICAÇÃO" };
        using var f = new FormPublicacao(pub, bis, p);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        _db.SalvarPublicacao(pub);
        CarregarPublicacoes();
    }

    void Editar()
    {
        if (PolicialAtual is not { } p || PubAtual is not { } pub) return;
        using var f = new FormPublicacao(pub, _db.ListarBoletins(), p);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        if (!pub.Manual) pub.Editada = true;
        _db.SalvarPublicacao(pub);
        CarregarPublicacoes();
    }

    void Excluir()
    {
        if (PubAtual is not { } pub) return;
        if (MessageBox.Show("Excluir esta publicação da folha do policial?", "Excluir", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _db.ExcluirPublicacao(pub.Id);
        CarregarPublicacoes();
    }
}
