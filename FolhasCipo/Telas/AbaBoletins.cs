using FolhasCipo.Servicos;

namespace FolhasCipo.Telas;

/// <summary>Importação dos BIs semanais (PDF) e lista dos já importados.</summary>
public class AbaBoletins : UserControl
{
    readonly Database _db;
    readonly DataGridView _grid = Ui.Grade();
    readonly Label _pasta = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 140 };

    public AbaBoletins(Database db)
    {
        _db = db;
        _grid.Columns.AddRange(
            Ui.Col("Numero", "BI Nº", 60), Ui.Col("Ano", "Ano", 55), Ui.Col("Data", "Data", 90), Ui.Col("SeiId", "Id SEI", 90),
            Ui.Col("QtdPublicacoes", "Publicações", 85), Ui.Col("ImportadoEm", "Importado em", 130), Ui.Col("Arquivo", "Arquivo", 400, true));
        _grid.Columns[2].DefaultCellStyle.Format = "dd/MM/yyyy";

        var barra = Ui.Barra(
            Ui.Botao("Importar BI (PDF)...", async (_, _) => await ImportarEscolhendo()),
            Ui.Botao("Verificar pasta de BIs", (_, _) => VerificarPastaDeBis(perguntar: false)),
            Ui.Botao("Definir pasta de BIs...", (_, _) => DefinirPasta()),
            Ui.Botao("Reprocessar selecionado", async (_, _) => await Reprocessar()),
            Ui.Botao("Excluir BI", (_, _) => Excluir()),
            _pasta);
        Controls.Add(_grid);
        Controls.Add(_log);
        Controls.Add(barra);
    }

    public void Recarregar()
    {
        _grid.DataSource = _db.ListarBoletins();
        var cfg = _db.LerConfig();
        _pasta.Text = string.IsNullOrEmpty(cfg.PastaBIs) ? "(pasta de BIs não definida)" : "Pasta: " + cfg.PastaBIs;
    }

    void Log(string s)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => Log(s))); return; }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}{Environment.NewLine}");
    }

    async Task ImportarEscolhendo()
    {
        using var dlg = new OpenFileDialog { Filter = "Boletim em PDF (*.pdf)|*.pdf", Multiselect = true, Title = "Selecione o(s) BI(s)" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        await Importar(dlg.FileNames);
    }

    bool ConfirmarSubstituicao(Boletim b, int editadas)
    {
        var msg = $"O BI Nº {b.Numero:00}/{b.Ano} já foi importado.\n\nReprocessar? As publicações automáticas desse BI serão refeitas.";
        if (editadas > 0) msg += $"\n\n{editadas} publicação(ões) editada(s) ou incluída(s) manualmente serão mantidas.";
        return (bool)Invoke(new Func<bool>(() =>
            MessageBox.Show(this, msg, "BI já importado", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes));
    }

    async Task Importar(IEnumerable<string> arquivos)
    {
        UseWaitCursor = true;
        Enabled = false;
        try
        {
            foreach (var arq in arquivos)
            {
                Log("Lendo " + Path.GetFileName(arq) + "...");
                try
                {
                    var resumo = await Task.Run(() => new ImportadorBi(_db).Importar(arq, ConfirmarSubstituicao));
                    foreach (var l in resumo.Linhas) Log(l);
                }
                catch (Exception ex) { Log("ERRO: " + ex.Message); }
            }
            Log("Concluído. Confira as publicações na aba \"Publicações por policial\".");
        }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
            Recarregar();
        }
    }

    void DefinirPasta()
    {
        using var dlg = new FolderBrowserDialog { Description = "Pasta onde os BIs (PDF) são salvos toda semana" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var cfg = _db.LerConfig();
        cfg.PastaBIs = dlg.SelectedPath;
        _db.SalvarConfig(cfg);
        Recarregar();
    }

    /// <summary>Procura PDFs na pasta de BIs que ainda não foram importados.</summary>
    public async void VerificarPastaDeBis(bool perguntar)
    {
        var cfg = _db.LerConfig();
        if (string.IsNullOrEmpty(cfg.PastaBIs) || !Directory.Exists(cfg.PastaBIs))
        {
            if (!perguntar) Ui.Erro("Defina primeiro a pasta onde os BIs são salvos.");
            return;
        }
        var importados = _db.ListarBoletins().Select(b => Path.GetFullPath(b.Arquivo)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var novos = Directory.GetFiles(cfg.PastaBIs, "*.pdf").Where(f => !importados.Contains(Path.GetFullPath(f))).OrderBy(f => File.GetLastWriteTime(f)).ToList();
        if (novos.Count == 0)
        {
            if (!perguntar) MessageBox.Show("Nenhum BI novo na pasta.", "Pasta de BIs");
            return;
        }
        var lista = string.Join("\n", novos.Select(Path.GetFileName));
        if (MessageBox.Show($"Há {novos.Count} BI(s) novo(s) na pasta:\n\n{lista}\n\nImportar agora?", "BIs novos",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            await Importar(novos);
    }

    Boletim? Selecionado => _grid.CurrentRow?.DataBoundItem as Boletim;

    async Task Reprocessar()
    {
        if (Selecionado is not { } b) return;
        if (!File.Exists(b.Arquivo)) { Ui.Erro("O arquivo original não foi encontrado:\n" + b.Arquivo); return; }
        await Importar(new[] { b.Arquivo });
    }

    void Excluir()
    {
        if (Selecionado is not { } b) return;
        if (MessageBox.Show($"Excluir o BI Nº {b.Numero:00}/{b.Ano} e todas as suas publicações (inclusive as editadas)?", "Excluir BI",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _db.ExcluirBoletim(b.Id);
        Recarregar();
    }
}
