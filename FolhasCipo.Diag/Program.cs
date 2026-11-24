using FolhasCipo;
using FolhasCipo.Extracao;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;
using System.Text;

// Teste de fumaça: gera um BI sintético (nomes FICTÍCIOS, mesmo layout de um elogio coletivo),
// lê com o mesmo código do programa e confere se o elogio chega à folha do policial.
var linhas = new (string t, bool novaPagina)[]
{
    ("BOLETIM INTERNO da CIPO Nº 11 de 09 de Março de 2026", false),
    ("2ª PARTE - ASSUNTOS GERAIS E ADMINISTRATIVOS", false),
    ("ELOGIOS:", false),
    ("Elogio os Policiais Militares 1º SGT QPPM ******002 ARNALDO PEREIRA LIMA, CB QPPM", false),
    ("******587 VALTER ROCHA DA SILVA, CB QPPM 100094418 ELIAS JOSÉ BARROS, CB QPPM", false),
    ("******069 OSVALDO ARAÚJO DE MELO, CB QPPM ******359 GILMAR SOUZA CRUZ, CB", false),
    ("QPPM ******009 JOAO FERREIRA PINTO, CB QPPM ******369 HENRIQUE FONSECA REGO,", false),
    ("CB QPPM ******275 DAVI MIGUEL CARVALHO DE SOUZA, CB QPPM ******177", false),
    ("WILSON VICENTE, CB QPPM ******275 ISAAC COSTA SOARES, CB QPPM ******522", false),
    ("ELTON LIMA DE CARVALHO, CB QPPM ******610 TIAGO LUCAS MENDES, CB", false),
    ("QPPM ******420 JOSÉ ALBERTO JUSTO DE FREITAS, CB QPPM ******605 SILAS ALVES MARTINS pelo empenho, dedicação e espírito de colaboração", false),
    ("demonstrados na realização de diversos serviços no âmbito desta Unidade.", false),
    ("Os referidos militares atuaram de forma voluntariosa e eficiente na limpeza e organização das dependências do", false),
    ("quartel, na reorganização de bens móveis e materiais de consumo no almoxarifado.", false),
    ("Diante do exposto, registro o presente elogio como forma de justo reconhecimento ao elevado padrão de", false),
    ("integrantes da tropa. (COLETIVO). BRUNO PEDRO DE FARIA - 1º TEN QOAPM.", false),
    ("Concedo elogio aos policiais militares CB QPPM ******421 JOSIEL ALMEIDA SANTANA, CB QPPM", false),
    ("******189 JORGE MENDES CAMPELO, pelo destacado", false),
    ("desempenho nas atividades de fiscalização de trânsito no município de Buritis.", false),
    ("criminosas de trânsito. (COLETIVO).", false),
    ("BRUNO PEDRO DE FARIA - 1º TEN QOAPM", false),
    ("Subcomandante da CIPO/CRP-IV", false),
};

var b = new PdfDocumentBuilder();
var fonte = b.AddTrueTypeFont(File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf")));
var pg = b.AddPage(595, 842);
double y = 800;
foreach (var (t, _) in linhas)
{
    pg.AddText(t, 8, new UglyToad.PdfPig.Core.PdfPoint(40, y), fonte);
    y -= 13;
}
var caminho = Path.Combine(Path.GetTempPath(), "bi_sintetico.pdf");
File.WriteAllBytes(caminho, b.Build());

var efetivo = new List<Policial>
{
    new() { Id = 1, PostoGrad = "CB PM", Quadro = "QPPM", Matricula = "100094587", Nome = "VALTER ROCHA DA SILVA", NomeGuerra = "ROCHA" },
    new() { Id = 2, PostoGrad = "CB PM", Quadro = "QPPM", Matricula = "100096189", Nome = "JORGE MENDES CAMPELO", NomeGuerra = "CAMPELO" },
    new() { Id = 3, PostoGrad = "CB PM", Quadro = "QPPM", Matricula = "100095275", Nome = "ISAAC COSTA SOARES", NomeGuerra = "SOARES" },
};

var leitor = new LeitorPdf();
var itens = leitor.Ler(caminho);
Console.WriteLine("=== ITENS LIDOS ===");
foreach (var i in itens) Console.WriteLine(i);
var seg = new Segmentador();
var bis = seg.SepararBis(itens, leitor.IdsSei);
Console.WriteLine($"=== BIs: {bis.Count} ===");
var extrator = new Extrator(new Identificador(efetivo));
int falhas = 0;
foreach (var bi in bis)
{
    var atos = seg.Segmentar(bi, itens);
    Console.WriteLine($"=== BI {bi.Numero}: {atos.Count} atos ===");
    foreach (var a in atos) Console.WriteLine($"  ato [{a.Secao}] itens={a.Itens.Count} assin={a.Assinatura.Count}");
    var res = extrator.Processar(bi, atos);
    foreach (var p in res.Publicacoes)
        Console.WriteLine($"  PM {p.PolicialId} | {p.Rotulo} | {string.Join(" ", p.Blocos.Where(x => x.Tipo == "p").Select(x => x.Texto))[..Math.Min(220, string.Join(" ", p.Blocos.Select(x => x.Texto)).Length)]}");
    foreach (var id in new[] { 1, 2, 3 })
        if (!res.Publicacoes.Any(p => p.PolicialId == id && p.Rotulo == "ELOGIO"))
        { Console.WriteLine($"FALHA: PM {id} sem ELOGIO"); falhas++; }
}
return falhas == 0 ? 0 : 1;
