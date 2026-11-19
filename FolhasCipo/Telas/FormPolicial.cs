namespace FolhasCipo.Telas;

/// <summary>Edição dos dados de um policial.</summary>
public class FormPolicial : Form
{
    readonly Policial _p;
    readonly NumericUpDown _antig = new() { Minimum = 0, Maximum = 99999 };
    readonly ComboBox _posto = new() { DropDownStyle = ComboBoxStyle.DropDown };
    readonly ComboBox _quadro = new() { DropDownStyle = ComboBoxStyle.DropDown };
    readonly TextBox _mat = new();
    readonly TextBox _nome = new() { CharacterCasing = CharacterCasing.Upper };
    readonly TextBox _guerra = new() { CharacterCasing = CharacterCasing.Upper };
    readonly ComboBox _sexo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox _unidade = new();
    readonly ComboBox _comp = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox _tipo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox _ativo = new() { Text = "Ativo no efetivo da unidade" };
    readonly TextBox _obs = new() { Multiline = true, Height = 70, ScrollBars = ScrollBars.Vertical };

    public FormPolicial(Policial p)
    {
        _p = p;
        Text = p.Id == 0 ? "Novo policial" : "Editar policial";
        Width = 620;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;

        _posto.Items.AddRange(new object[] { "CEL PM", "TEN CEL PM", "MAJ PM", "CAP PM", "1º TEN PM", "2º TEN PM", "ASP OF PM",
                                             "ST PM", "1º SGT PM", "2º SGT PM", "3º SGT PM", "CB PM", "SD PM" });
        _quadro.Items.AddRange(new object[] { "QOPM", "QOAPM", "QOPMS", "QPPM" });
        _sexo.Items.AddRange(new object[] { "", "M", "F" });
        _comp.Items.AddRange(new object[] { "", "EXCEPCIONAL", "ÓTIMO", "BOM", "INSUFICIENTE", "MAU" });
        _tipo.Items.AddRange(new object[] { "AUTO", "ANUAL", "QUADRIMESTRAL" });

        _antig.Value = Math.Max(0, p.Antiguidade);
        _posto.Text = p.PostoGrad;
        _quadro.Text = p.Quadro;
        _mat.Text = p.Matricula;
        _nome.Text = p.Nome;
        _guerra.Text = p.NomeGuerra;
        _sexo.SelectedItem = _sexo.Items.Contains(p.Sexo) ? p.Sexo : "";
        _unidade.Text = p.Unidade;
        _comp.SelectedItem = _comp.Items.Contains(p.Comportamento) ? p.Comportamento : "";
        _tipo.SelectedItem = _tipo.Items.Contains(p.TipoFolha) ? p.TipoFolha : "AUTO";
        _ativo.Checked = p.Ativo;
        _obs.Text = p.Observacao;

        var dica = new Label
        {
            AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(420, 0),
            Text = "Folha AUTO: oficiais e sargentos = quadrimestral; cabos e soldados = anual.\n" +
                   "Comportamento: usado quando não há publicação de comportamento no período.\n" +
                   "O nome completo e o nome de guerra devem ser iguais aos que aparecem nos BIs.",
        };
        var form = Ui.Formulario(
            ("Antiguidade (ordem)", _antig), ("Posto/Graduação", _posto), ("Quadro", _quadro), ("Matrícula (RE)", _mat),
            ("Nome completo", _nome), ("Nome de guerra", _guerra), ("Sexo", _sexo), ("Unidade/Fração", _unidade),
            ("Comportamento", _comp), ("Tipo de folha", _tipo), ("", _ativo), ("Observação", _obs), ("", dica));

        var ok = new Button { Text = "Salvar", DialogResult = DialogResult.None, Width = 100 };
        var cancelar = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 100 };
        ok.Click += (_, _) => Confirmar();
        var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        botoes.Controls.Add(cancelar);
        botoes.Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = cancelar;
        Controls.Add(form);
        Controls.Add(botoes);
    }

    void Confirmar()
    {
        if (string.IsNullOrWhiteSpace(_mat.Text) || string.IsNullOrWhiteSpace(_nome.Text))
        {
            Ui.Erro("Matrícula e nome completo são obrigatórios.");
            return;
        }
        _p.Antiguidade = (int)_antig.Value;
        _p.PostoGrad = _posto.Text.Trim();
        _p.Quadro = _quadro.Text.Trim();
        _p.Matricula = _mat.Text.Trim();
        _p.Nome = _nome.Text.Trim();
        _p.NomeGuerra = _guerra.Text.Trim();
        _p.Sexo = _sexo.SelectedItem?.ToString() ?? "";
        _p.Unidade = _unidade.Text.Trim();
        _p.Comportamento = _comp.SelectedItem?.ToString() ?? "";
        _p.TipoFolha = _tipo.SelectedItem?.ToString() ?? "AUTO";
        _p.Ativo = _ativo.Checked;
        _p.Observacao = _obs.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
