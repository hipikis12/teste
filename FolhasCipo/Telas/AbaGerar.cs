using System.Diagnostics;
using FolhasCipo.Servicos;

namespace FolhasCipo.Telas;

/// <summary>Geração das folhas (.docx) por período.</summary>
public class AbaGerar : UserControl
{
    readonly Database _db;
    readonly NumericUpDown _ano = new() { Minimum = 2000, Maximum = 2100, Width = 80 };
    readonly ComboBox _quad = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    readonly ComboBox _modo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    readonly CheckedListBox _lista = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    readonly TextBox _saida = new() { Width = 420 };
    readonly TextBox _chefe = new(), _cargo = new(), _local = new(), _unidade = new();
    readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };

    public AbaGerar(Database db)
    {
        _db = db;
        _quad.Items.AddRange(new object[] { "1º quadrimestre (01/fev a 31/mai)", "2º quadrimestre (01/jun a 30/set)", "3º quadrimestre (01/out a 31/jan)" });
        _modo.Items.AddRange(new object[]
        {
            "Automático (cabos/soldados anual; oficiais/sargentos quadrimestral)",
            "Anual para todos os selecionados",
            "Quadrimestral para todos os selecionados",
        });
        _modo.SelectedIndex = 0;
        var (a, q) = Periodo.QuadrimestreDe(DateTime.Today);
        _ano.Value = a;
        _quad.SelectedIndex = q - 1;

        var linha1 = Ui.Barra(new Label { Text = "Ano:", AutoSize = true }, _ano, new Label { Text = "Quadrimestre:", AutoSize = true }, _quad,
                              new Label { Text = "Tipo:", AutoSize = true }, _modo);
        var linha2 = Ui.Barra(new Label { Text = "Salvar em:", AutoSize = true }, _saida,
                              Ui.Botao("...", (_, _) => EscolherPasta(), 40),
                              Ui.Botao("Gerar folhas", async (_, _) => await Gerar(), 140),
                              Ui.Botao("Abrir pasta", (_, _) => AbrirPasta()),
                              Ui.Botao("Conferir com folhas da P1 (PDF)...", async (_, _) => await Conferir()));

        var selecao = new Panel { Dock = DockStyle.Left, Width = 380, Padding = new Padding(6) };
        var botoesSel = Ui.Barra(
            Ui.Botao("Todos", (_, _) => Marcar(_ => true)),
            Ui.Botao("Nenhum", (_, _) => Marcar(_ => false)),
            Ui.Botao("Só oficiais/sargentos", (_, _) => Marcar(p => p.FolhaQuadrimestral)),
            Ui.Botao("Só cabos/soldados", (_, _) => Marcar(p => !p.FolhaQuadrimestral)));
        botoesSel.Height = 74;
        botoesSel.WrapContents = true;
        selecao.Controls.Add(_lista);
        selecao.Controls.Add(botoesSel);

        var cfgBox = new GroupBox { Text = "Assinatura e dados da folha", Dock = DockStyle.Top, Height = 190 };
        var form = Ui.Formulario(("Nome - posto (P1)", _chefe), ("Função", _cargo), ("Local/data", _local), ("Unidade (cabeçalho)", _unidade));
        cfgBox.Controls.Add(form);
        var salvarCfg = Ui.Botao("Salvar assinatura", (_, _) => SalvarConfig());
        var painelCfg = new Panel { Dock = DockStyle.Top, Height = 40 };
        painelCfg.Controls.Add(salvarCfg);

        var direita = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        direita.Controls.Add(_log);
        direita.Controls.Add(painelCfg);
        direita.Controls.Add(cfgBox);

        Controls.Add(direita);
        Controls.Add(selecao);
        Controls.Add(linha2);
        Controls.Add(linha1);
    }

    public void Recarregar()
    {
        var cfg = _db.LerConfig();
        _chefe.Text = cfg.ChefeP1Nome;
        _cargo.Text = cfg.ChefeP1Cargo;
        _local.Text = cfg.LocalData;
        _unidade.Text = cfg.Unidade;
        if (string.IsNullOrEmpty(_saida.Text))
            _saida.Text = string.IsNullOrEmpty(cfg.PastaSaida)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Folhas de Alteracao")
                : cfg.PastaSaida;
        var marcados = _lista.CheckedItems.Cast<Policial>().Select(p => p.Id).ToHashSet();
        _lista.Items.Clear();
        foreach (var p in _db.ListarPoliciais(somenteAtivos: true))
            _lista.Items.Add(p, marcados.Count == 0 || marcados.Contains(p.Id));
    }

    void Marcar(Func<Policial, bool> criterio)
    {
        for (int i = 0; i < _lista.Items.Count; i++)
            _lista.SetItemChecked(i, criterio((Policial)_lista.Items[i]));
    }

    void SalvarConfig()
    {
        var cfg = _db.LerConfig();
        cfg.ChefeP1Nome = _chefe.Text.Trim();
        cfg.ChefeP1Cargo = _cargo.Text.Trim();
        cfg.LocalData = _local.Text.Trim();
        cfg.Unidade = _unidade.Text.Trim();
        cfg.PastaSaida = _saida.Text.Trim();
        _db.SalvarConfig(cfg);
        MessageBox.Show("Assinatura salva.", "Configuração");
    }

    void EscolherPasta()
    {
        using var dlg = new FolderBrowserDialog { Description = "Pasta onde as folhas serão salvas" };
        if (dlg.ShowDialog(this) == DialogResult.OK) _saida.Text = dlg.SelectedPath;
    }

    void AbrirPasta()
    {
        if (Directory.Exists(_saida.Text)) Process.Start(new ProcessStartInfo { FileName = _saida.Text, UseShellExecute = true });
    }

    /// <summary>Compara folhas feitas pela P1 (PDF do SEI) com o que o sistema lança.</summary>
    async Task Conferir()
    {
        using var dlg = new OpenFileDialog { Filter = "Folhas de alteração (*.pdf)|*.pdf", Multiselect = true, Title = "Folhas feitas pela P1" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _log.Clear();
        _log.AppendText("Conferindo " + dlg.FileNames.Length + " folha(s)..." + Environment.NewLine);
        Enabled = false;
        UseWaitCursor = true;
        try
        {
            var arquivos = dlg.FileNames;
            var relatorio = await Task.Run(() => new Conferencia(_db).Conferir(arquivos));
            _log.AppendText(relatorio);
        }
        catch (Exception ex) { Ui.Erro("Erro na conferência: " + ex.Message); }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }

    async Task Gerar()
    {
        var sel = _lista.CheckedItems.Cast<Policial>().ToList();
        if (sel.Count == 0) { Ui.Erro("Marque ao menos um policial."); return; }
        if (string.IsNullOrWhiteSpace(_saida.Text)) { Ui.Erro("Informe a pasta de saída."); return; }
        var cfg = _db.LerConfig();
        cfg.PastaSaida = _saida.Text.Trim();
        _db.SalvarConfig(cfg);

        int ano = (int)_ano.Value, quad = _quad.SelectedIndex + 1;
        string modo = _modo.SelectedIndex switch { 1 => "ANUAL", 2 => "QUADRIMESTRAL", _ => "AUTO" };
        var pasta = _saida.Text.Trim();
        _log.Clear();
        Enabled = false;
        UseWaitCursor = true;
        try
        {
            var prog = new Progress<string>(s => _log.AppendText(s + Environment.NewLine));
            var arquivos = await Task.Run(() => new ServicoFolhas(_db).Gerar(sel, ano, quad, modo, pasta, prog));
            _log.AppendText($"{Environment.NewLine}{arquivos.Count} folha(s) salvas em {pasta}{Environment.NewLine}");
        }
        catch (Exception ex) { Ui.Erro("Erro ao gerar: " + ex.Message); }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }
}
