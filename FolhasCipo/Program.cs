using FolhasCipo.Telas;

namespace FolhasCipo;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new FormPrincipal(new Database()));
    }
}
