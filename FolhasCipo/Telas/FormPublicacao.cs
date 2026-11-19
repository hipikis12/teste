using System.Text;

namespace FolhasCipo.Telas;

/// <summary>Edição de uma publicação (item da 3ª parte da folha).</summary>
public class FormPublicacao : Form
{
    readonly Publicacao _pub;
    readonly ComboBox _bi = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox _rotulo = new() { DropDownStyle = ComboBoxStyle.DropDown };
    readonly TextBox _texto = new()
    {
        Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = true, AcceptsReturn = true, AcceptsTab = false,
        Font = new Font("Consolas", 10f), Dock = DockStyle.Fill,
    };

    static readonly string[] Rotulos =
    {
        "FÉRIAS INÍCIO / RETORNO", "ELOGIO", "PORTARIA", "COMPORTAMENTO", "DISPENSA COMO RECOMPENSA", "LICENÇA ESPECIAL",
        "LICENÇA PATERNIDADE", "LICENÇA NUPCIAL", "TRÂNSITO / INSTALAÇÃO", "INSPEÇÃO DE SAÚDE", "AFASTAMENTO", "APRESENTAÇÃO",
        "OFÍCIO", "REQUERIMENTO", "DECISÃO", "DESPACHO", "PARECER", "MEMORANDO", "SINDICÂNCIA", "SOLUÇÃO DE ATESTADO DE ORIGEM",
        "HOMOLOGAÇÃO", "NOTA DE PUNIÇÃO", "CURSOS", "ERRATA", "PUBLICAÇÃO",
    };

    public FormPublicacao(Publicacao pub, List<Boletim> bis, Policial p)
    {
        _pub = pub;
        Text = $"Publicação – {p.PostoGrad} {p.NomeGuerra}";
        Width = 980;
        Height = 680;
        StartPosition = FormStartPosition.CenterParent;

        foreach (var b in bis.OrderBy(b => b.Data)) _bi.Items.Add(b);
        _bi.SelectedItem = bis.FirstOrDefault(b => b.Id == pub.BoletimId) ?? bis.OrderBy(b => b.Data).Last();
        _rotulo.Items.AddRange(Rotulos);
        _rotulo.Text = pub.Rotulo;
        _texto.Text = BlocosParaTexto(pub.Blocos).Replace("\n", "\r\n");

        var topo = new TableLayoutPanel { Dock = DockStyle.Top, Height = 74, ColumnCount = 2, Padding = new Padding(8, 8, 8, 0) };
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topo.Controls.Add(new Label { Text = "Boletim (BI)", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        _bi.Dock = DockStyle.Fill;
        topo.Controls.Add(_bi);
        topo.Controls.Add(new Label { Text = "Item (título)", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        _rotulo.Dock = DockStyle.Fill;
        topo.Controls.Add(_rotulo);

        var ajuda = new Label
        {
            Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 4, 10, 0), ForeColor = SystemColors.GrayText,
            Text = "Teor publicado. Parágrafos separados por linha em branco. Tabela: entre [TABELA] e [/TABELA], " +
                   "uma linha por registro com as colunas separadas por \" | \"; a linha que começa com # é o cabeçalho.",
        };
        var meio = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        meio.Controls.Add(_texto);

        var ok = new Button { Text = "Salvar", Width = 100 };
        var cancelar = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 100 };
        ok.Click += (_, _) => Confirmar();
        var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        botoes.Controls.Add(cancelar);
        botoes.Controls.Add(ok);
        CancelButton = cancelar;

        Controls.Add(meio);
        Controls.Add(ajuda);
        Controls.Add(topo);
        Controls.Add(botoes);
    }

    void Confirmar()
    {
        if (_bi.SelectedItem is not Boletim b) { Ui.Erro("Escolha o BI."); return; }
        var blocos = TextoParaBlocos(_texto.Text);
        if (blocos.Count == 0) { Ui.Erro("O teor está vazio."); return; }
        _pub.BoletimId = b.Id;
        _pub.Rotulo = string.IsNullOrWhiteSpace(_rotulo.Text) ? "PUBLICAÇÃO" : _rotulo.Text.Trim().ToUpperInvariant();
        _pub.Blocos = blocos;
        DialogResult = DialogResult.OK;
        Close();
    }

    public static string BlocosParaTexto(List<Bloco> blocos)
    {
        var sb = new StringBuilder();
        foreach (var b in blocos)
        {
            if (b.Tipo == "p") sb.Append(b.Texto).Append("\n\n");
            else
            {
                sb.Append("[TABELA]\n");
                if (b.Cabecalho != null) sb.Append("# ").Append(string.Join(" | ", b.Cabecalho)).Append('\n');
                foreach (var r in b.Linhas ?? new()) sb.Append(string.Join(" | ", r)).Append('\n');
                sb.Append("[/TABELA]\n\n");
            }
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    public static List<Bloco> TextoParaBlocos(string texto)
    {
        var res = new List<Bloco>();
        var linhas = texto.Replace("\r\n", "\n").Split('\n');
        var par = new List<string>();
        void Fecha()
        {
            var t = Texto.Compacta(string.Join(" ", par));
            if (t.Length > 0) res.Add(Bloco.Paragrafo(t));
            par.Clear();
        }
        for (int i = 0; i < linhas.Length; i++)
        {
            var l = linhas[i].Trim();
            if (l == "[TABELA]")
            {
                Fecha();
                List<string>? cab = null;
                var rows = new List<List<string>>();
                for (i++; i < linhas.Length && linhas[i].Trim() != "[/TABELA]"; i++)
                {
                    var r = linhas[i].Trim();
                    if (r.Length == 0) continue;
                    var cels = r.TrimStart('#').Split('|').Select(c => c.Trim()).ToList();
                    if (r.StartsWith("#")) cab = cels; else rows.Add(cels);
                }
                if (rows.Count > 0 || cab != null) res.Add(Bloco.Tabela(cab, rows));
            }
            else if (l.Length == 0) Fecha();
            else par.Add(l);
        }
        Fecha();
        return res;
    }
}
