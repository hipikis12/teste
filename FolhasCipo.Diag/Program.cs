using FolhasCipo;
using FolhasCipo.Extracao;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;
using System.Text;

// Teste de fumaça: gera um BI sintético (nomes FICTÍCIOS, mesmo layout de um elogio coletivo),
// lê com o mesmo código do programa e confere se o elogio chega à folha do policial.
int falhasAmostra = 0;
var linhas = new (int pag, double top, string t)[]
{
    (1, 60, "BOLETIM INTERNO da CIPO Nº 11 de 09 de Março de 2026"),
    (1, 80, "2ª PARTE - ASSUNTOS GERAIS E ADMINISTRATIVOS"),
    (1, 120, "ELOGIOS:"),
    (1, 369, "Elogio os Policiais Militares 1º SGT QPPM ******002 ARNALDO PEREIRA LIMA, CB QPPM"),
    (1, 382, "******587 VALTER ROCHA DA SILVA, CB QPPM 100094418 ELIAS JOSÉ BARROS, CB QPPM"),
    (1, 395, "******069 OSVALDO ARAÚJO DE MELO, CB QPPM ******359 GILMAR SOUZA CRUZ, CB"),
    (1, 409, "QPPM ******009 JOAO FERREIRA PINTO, CB QPPM ******369 HENRIQUE FONSECA REGO,"),
    (1, 422, "CB QPPM ******275 DAVI MIGUEL CARVALHO DE SOUZA, CB QPPM ******177"),
    (1, 435, "WILSON VICENTE, CB QPPM ******275 ISAAC COSTA SOARES, CB QPPM ******522"),
    (1, 448, "ELTON LIMA DE CARVALHO, CB QPPM ******610 TIAGO LUCAS MENDES, CB"),
    (1, 462, "QPPM ******420 JOSÉ ALBERTO JUSTO DE FREITAS, CB QPPM ******578 PAULO FARIAS, CB"),
    (1, 475, "QPPM ******605 SILAS ALVES MARTINS pelo empenho, dedicação e espírito de colaboração"),
    (1, 488, "demonstrados na realização de diversos serviços no âmbito desta Unidade."),
    (1, 507, "Os referidos militares atuaram de forma voluntariosa e eficiente na limpeza e organização das dependências do"),
    (1, 520, "quartel, na reorganização de bens móveis e materiais de consumo no almoxarifado."),
    (1, 539, "Cumpre ressaltar que tais ações foram realizadas em momento de significativa necessidade por parte do"),
    (1, 552, "Comando, ocasião em que os militares demonstraram elevado espírito de cooperação."),
    (1, 571, "Diante do exposto, registro o presente elogio como forma de justo reconhecimento ao elevado padrão de"),
    (1, 584, "integrantes da tropa. (COLETIVO). BRUNO PEDRO DE FARIA - 1º TEN QOAPM."),
    (1, 622, "Concedo elogio aos policiais militares CB QPPM ******421 JOSIEL ALMEIDA SANTANA, CB QPPM"),
    (1, 635, "******189 JORGE MENDES CAMPELO, pelo destacado"),
    (1, 648, "desempenho nas atividades de fiscalização de trânsito no município de Buritis. Os referidos militares vêm se"),
    (1, 661, "criminosas de trânsito."),
    (1, 680, "Recomendo que os referidos militares continuem desenvolvendo o imprescindível trabalho. (COLETIVO)."),
    (1, 700, "BRUNO PEDRO DE FARIA - 1º TEN QOAPM"),
    (1, 713, "Subcomandante da CIPO/CRP-IV"),
    (1, 726, "Portaria nº 1381 de 20 de fevereiro de 2026"),
};

var b = new PdfDocumentBuilder();
var fonte = b.AddTrueTypeFont(File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf")));
var pg = b.AddPage(595, 842);
foreach (var (_, top, t) in linhas)
    pg.AddText(t, 9, new UglyToad.PdfPig.Core.PdfPoint(60, 842 - top), fonte);
var caminho = Path.Combine(Path.GetTempPath(), "bi_sintetico.pdf");
File.WriteAllBytes(caminho, b.Build());

foreach (var (txt, esperado) in new (string, int?)[] {
    ("BOLETIM INTERNO da CIPO Nº 34", 34), ("BOLETIM INTERNO da CIPO Nº 11 CIPO/P-1 de 09 de Março de 2026.", 11),
    ("B O L E T I M  I N T E R N O  da CIPO N º 1 5", 15), ("BOLETIM INTERNO", null),
    ("Publicar a presente portaria em Boletim Interno nº 5 da CIPO", null) })
{
    var r = Segmentador.NumeroDoCabecalho(txt);
    Console.WriteLine($"cabecalho '{txt}' => {r}");
    if (r != esperado) Console.WriteLine("FALHA: cabeçalho esperado " + esperado);
}

// ---- amostra com a geometria real de um BI avulso (quadro grande em volta do texto, nomes removidos)
{
    var amostra = Path.Combine(AppContext.BaseDirectory, "amostras", "bi_pagina1_geometria_real.pdf");
    var l2 = new LeitorPdf();
    var it2 = l2.Ler(amostra);
    Console.WriteLine("=== AMOSTRA GEOMETRIA REAL: itens ===");
    foreach (var i in it2.Take(30)) Console.WriteLine(i);
    var b2 = new Segmentador().SepararBis(it2, l2.IdsSei);
    Console.WriteLine("=== AMOSTRA: BIs encontrados = " + string.Join(",", b2.Select(x => x.Numero)));
    if (!b2.Any(x => x.Numero == 45)) { Console.WriteLine("FALHA: BI 45 da amostra nao reconhecido"); falhasAmostra = 1; }
}


// ---- amostra BI 38: dois requerimentos e dois pareceres em sequência (nomes fictícios, texto/posições do BI real)
{
    var linhasJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "amostras", "bi38_requerimentos.json")));
    var bb = new PdfDocumentBuilder();
    var ff = bb.AddTrueTypeFont(File.ReadAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf")));
    var paginas = new Dictionary<int, UglyToad.PdfPig.Writer.PdfPageBuilder>();
    foreach (var e in linhasJson.RootElement.EnumerateArray())
    {
        int pn = e.GetProperty("p").GetInt32();
        if (!paginas.ContainsKey(pn)) { while (paginas.Count < pn) paginas[paginas.Count + 1] = bb.AddPage(595, 842); }
        paginas[pn].AddText(e.GetProperty("t").GetString()!, 8.5, new UglyToad.PdfPig.Core.PdfPoint(40, 842 - e.GetProperty("y").GetDouble()), ff);
    }
    var cam38 = Path.Combine(Path.GetTempPath(), "bi38.pdf");
    File.WriteAllBytes(cam38, bb.Build());
    var ef = new List<Policial>
    {
        new() { Id = 1, PostoGrad = "CB PM", Quadro = "QPPM", Matricula = "100000000", Nome = "JOAQUIM ROCHA PIRES", NomeGuerra = "PIRES" },
        new() { Id = 2, PostoGrad = "2º SGT PM", Quadro = "QPPM", Matricula = "100077002", Nome = "REINALDO SOUSA LIMA", NomeGuerra = "LIMA" },
        new() { Id = 3, PostoGrad = "CB PM", Quadro = "QPPM", Matricula = "100096212", Nome = "ALMIR HENRIQUE COSTA", NomeGuerra = "ALMIR" },
    };
    var lt = new LeitorPdf();
    var its = lt.Ler(cam38);
    var sg = new Segmentador();
    var bs = sg.SepararBis(its, lt.IdsSei);
    Console.WriteLine("=== BI38 amostra: BIs=" + string.Join(",", bs.Select(x => x.Numero)));
    foreach (var bi in bs)
    {
        var atos = sg.Segmentar(bi, its);
        Console.WriteLine($"=== BI38 atos: {atos.Count}");
        foreach (var a in atos) Console.WriteLine($"  ato [{a.Secao}] itens={a.Itens.Count} assin={a.Assinatura.Count} 1o='{a.Itens.First().TextoCompleto[..Math.Min(50, a.Itens.First().TextoCompleto.Length)]}'");
        var rs = new Extrator(new Identificador(ef)).Processar(bi, atos);
        foreach (var p in rs.Publicacoes)
        {
            var tx = string.Join(" ", p.Blocos.Where(x => x.Tipo == "p").Select(x => x.Texto));
            Console.WriteLine($"  PM {p.PolicialId} | {p.Rotulo} | len={tx.Length} | {tx[..Math.Min(110, tx.Length)]} ... {tx[Math.Max(0, tx.Length - 90)..]}");
        }
    }
}

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
return falhas + falhasAmostra == 0 ? 0 : 1;
