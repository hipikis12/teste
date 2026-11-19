namespace FolhasCipo.Telas;

public class FormPrincipal : Form
{
    readonly Database _db;
    readonly AbaPoliciais _policiais;
    readonly AbaBoletins _boletins;
    readonly AbaPublicacoes _publicacoes;
    readonly AbaGerar _gerar;

    public FormPrincipal(Database db)
    {
        _db = db;
        Text = "Folhas de Alteração - CIPO";
        Width = 1200;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        _policiais = new AbaPoliciais(db) { Dock = DockStyle.Fill };
        _boletins = new AbaBoletins(db) { Dock = DockStyle.Fill };
        _publicacoes = new AbaPublicacoes(db) { Dock = DockStyle.Fill };
        _gerar = new AbaGerar(db) { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Pagina("Policiais", _policiais));
        tabs.TabPages.Add(Pagina("Boletins (BI)", _boletins));
        tabs.TabPages.Add(Pagina("Publicações por policial", _publicacoes));
        tabs.TabPages.Add(Pagina("Gerar folhas", _gerar));
        tabs.SelectedIndexChanged += (_, _) => Atualizar(tabs.SelectedIndex);
        Controls.Add(tabs);

        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel("Banco de dados: " + db.Caminho));
        Controls.Add(status);

        Shown += (_, _) =>
        {
            Atualizar(0);
            _boletins.VerificarPastaDeBis(perguntar: true);
        };
    }

    static TabPage Pagina(string titulo, Control c)
    {
        var p = new TabPage(titulo);
        p.Controls.Add(c);
        return p;
    }

    void Atualizar(int aba)
    {
        switch (aba)
        {
            case 0: _policiais.Recarregar(); break;
            case 1: _boletins.Recarregar(); break;
            case 2: _publicacoes.Recarregar(); break;
            case 3: _gerar.Recarregar(); break;
        }
    }
}
