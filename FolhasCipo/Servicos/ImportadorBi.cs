using FolhasCipo.Extracao;

namespace FolhasCipo.Servicos;

public class ResumoImportacao
{
    public List<string> Linhas { get; } = new();
    public int TotalPublicacoes { get; set; }
    public override string ToString() => string.Join(Environment.NewLine, Linhas);
}

/// <summary>Importa um PDF (um BI semanal ou um processo SEI com vários BIs) e grava as publicações por policial.</summary>
public class ImportadorBi
{
    readonly Database _db;
    public ImportadorBi(Database db) { _db = db; }

    /// <param name="confirmarSubstituicao">Chamado quando o BI já existe; retorna true para reprocessar.</param>
    public ResumoImportacao Importar(string caminhoPdf, Func<Boletim, int, bool> confirmarSubstituicao)
    {
        var resumo = new ResumoImportacao();
        var efetivo = _db.ListarPoliciais(somenteAtivos: true);
        if (efetivo.Count == 0)
            throw new InvalidOperationException("Cadastre os policiais (aba Policiais) antes de importar boletins.");

        var leitor = new LeitorPdf();
        var itens = leitor.Ler(caminhoPdf);
        var seg = new Segmentador();
        var bis = seg.SepararBis(itens, leitor.IdsSei);
        if (bis.Count == 0)
        {
            // guarda o que foi lido para diagnóstico (primeiras linhas e linhas que citam "boletim")
            string dump = "";
            try
            {
                var pasta = Path.Combine(AppContext.BaseDirectory, "dados", "diagnostico");
                Directory.CreateDirectory(pasta);
                var linhas = itens.Take(60).Select(i => i.ToString()).ToList();
                linhas.Add("--- linhas com 'boletim' ---");
                linhas.AddRange(itens.Where(i => i.TextoCompleto.Contains("boletim", StringComparison.OrdinalIgnoreCase)).Take(40).Select(i => i.ToString()));
                dump = Path.Combine(pasta, "leitura_sem_cabecalho.txt");
                File.WriteAllLines(dump, linhas);
            }
            catch { }
            throw new InvalidOperationException("Não encontrei o cabeçalho \"BOLETIM INTERNO ... Nº\" no PDF (" + itens.Count + " linhas lidas). " +
                "Verifique se o arquivo é um BI." + (dump.Length > 0 ? " Detalhes salvos em: " + dump : ""));
        }
        {
            var nums = bis.Select(b => b.Numero).Distinct().OrderBy(n => n).ToList();
            resumo.Linhas.Add("BIs encontrados no PDF: " + string.Join(", ", nums));
            var faltam = Enumerable.Range(nums.First(), nums.Last() - nums.First() + 1).Except(nums).ToList();
            if (faltam.Count > 0) resumo.Linhas.Add("   ATENÇÃO: faltam no PDF os BIs: " + string.Join(", ", faltam));
        }

        var extrator = new Extrator(new Identificador(efetivo));
        foreach (var bi in bis)
        {
            var existente = _db.ObterBoletim(bi.Numero, bi.Data.Year);
            if (existente != null)
            {
                int editadas = _db.ContarEditadas(existente.Id);
                if (!confirmarSubstituicao(existente, editadas))
                {
                    resumo.Linhas.Add($"BI Nº {bi.Numero:00}/{bi.Data.Year}: já importado – mantido sem alteração.");
                    continue;
                }
            }
            var atos = seg.Segmentar(bi, itens);
            var res = extrator.Processar(bi, atos);
            var b = new Boletim { Numero = bi.Numero, Ano = bi.Data.Year, Data = bi.Data, SeiId = bi.SeiId, Arquivo = caminhoPdf };
            int id = _db.SalvarBoletim(b);
            _db.LimparPublicacoesAutomaticas(id);
            foreach (var p in res.Publicacoes) p.BoletimId = id;
            _db.InserirPublicacoes(res.Publicacoes);
            int pms = res.Publicacoes.Select(p => p.PolicialId).Distinct().Count();
            resumo.TotalPublicacoes += res.Publicacoes.Count;
            resumo.Linhas.Add($"BI Nº {bi.Numero:00}/{bi.Data.Year} ({bi.Data:dd/MM/yyyy}): {res.Publicacoes.Count} publicações para {pms} policiais.");
            foreach (var av in res.Avisos.Distinct()) resumo.Linhas.Add("   ATENÇÃO: " + av);
            try
            {
                var pasta = Path.Combine(AppContext.BaseDirectory, "dados", "diagnostico");
                Directory.CreateDirectory(pasta);
                File.WriteAllLines(Path.Combine(pasta, $"BI_{bi.Numero:00}_{bi.Data.Year}.txt"), res.Diagnostico);
            }
            catch { /* o diagnóstico é opcional */ }
        }
        return resumo;
    }
}
