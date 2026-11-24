using FolhasCipo.Servicos;

namespace FolhasCipo.Telas;

/// <summary>Cadastro do efetivo: incluir, editar, excluir e importar a planilha do sistema.</summary>
public class AbaPoliciais : UserControl
{
    readonly Database _db;
    readonly DataGridView _grid = Ui.Grade();
    readonly TextBox _filtro = new() { Width = 260, PlaceholderText = "Filtrar por nome, guerra ou matrícula" };
    readonly CheckBox _soAtivos = new() { Text = "Somente ativos", AutoSize = true, Checked = true };
    List<Policial> _todos = new();

    public AbaPoliciais(Database db)
    {
        _db = db;
        _grid.Columns.AddRange(
            Ui.Col("Antiguidade", "Antig.", 55), Ui.Col("PostoGrad", "Posto/Grad.", 90), Ui.Col("Quadro", "Quadro", 65),
            Ui.Col("Matricula", "Matrícula", 90), Ui.Col("Nome", "Nome", 300), Ui.Col("NomeGuerra", "Nome de guerra", 130),
            Ui.Col("Comportamento", "Comportamento", 110), Ui.Col("TipoFolha", "Folha", 110), Ui.Col("Ativo", "Ativo", 50),
            Ui.Col("InclusaoData", "Chegada", 85), Ui.Col("ExclusaoData", "Saída", 85),
            Ui.Col("Observacao", "Observação", 200));
        _grid.Columns[9].DefaultCellStyle.Format = "dd/MM/yyyy";
        _grid.Columns[10].DefaultCellStyle.Format = "dd/MM/yyyy";
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Editar(); };

        var barra = Ui.Barra(
            Ui.Botao("Novo", (_, _) => Novo()),
            Ui.Botao("Editar", (_, _) => Editar()),
            Ui.Botao("Excluir", (_, _) => Excluir()),
            Ui.Botao("Importar planilha do efetivo (.xlsx)", (_, _) => ImportarPlanilha(), 260),
            _filtro, _soAtivos);
        _filtro.TextChanged += (_, _) => Filtrar();
        _soAtivos.CheckedChanged += (_, _) => Filtrar();

        Controls.Add(_grid);
        Controls.Add(barra);
    }

    public void Recarregar()
    {
        _todos = _db.ListarPoliciais();
        Filtrar();
    }

    void Filtrar()
    {
        var f = Texto.Norm(_filtro.Text);
        _grid.DataSource = _todos
            .Where(p => !_soAtivos.Checked || p.Ativo)
            .Where(p => f.Length == 0 || Texto.Norm(p.Nome + " " + p.NomeGuerra + " " + p.Matricula).Contains(f))
            .ToList();
    }

    Policial? Selecionado => _grid.CurrentRow?.DataBoundItem as Policial;

    void Novo()
    {
        var p = new Policial { Antiguidade = _todos.Count == 0 ? 1 : _todos.Max(x => x.Antiguidade) + 1 };
        using var f = new FormPolicial(p);
        if (f.ShowDialog(this) == DialogResult.OK) { Salvar(p); }
    }

    void Editar()
    {
        if (Selecionado is not { } p) return;
        using var f = new FormPolicial(p);
        if (f.ShowDialog(this) == DialogResult.OK) Salvar(p);
    }

    void Salvar(Policial p)
    {
        try { _db.SalvarPolicial(p); Recarregar(); }
        catch (Exception ex) { Ui.Erro("Não foi possível salvar: " + ex.Message); }
    }

    void Excluir()
    {
        if (Selecionado is not { } p) return;
        if (MessageBox.Show($"Excluir {p.Descricao}?\n\nTodas as publicações desse policial também serão apagadas.\n" +
                            "Se ele apenas saiu da unidade, prefira desmarcar \"Ativo\" na edição.",
                "Excluir policial", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _db.ExcluirPolicial(p.Id);
        Recarregar();
    }

    void ImportarPlanilha()
    {
        using var dlg = new OpenFileDialog { Filter = "Planilha Excel (*.xlsx)|*.xlsx", Title = "Relatório do efetivo" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var (novos, atual) = new ServicoFolhas(_db).ImportarEfetivo(dlg.FileName);
            Recarregar();
            MessageBox.Show($"{novos} policiais incluídos e {atual} atualizados (pela matrícula).", "Importação concluída");
        }
        catch (Exception ex) { Ui.Erro(ex.Message); }
    }
}
