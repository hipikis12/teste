using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Color = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace FolhasCipo.Servicos;

/// <summary>Grava a folha de alteração em .docx no formato do modelo da P1.</summary>
public class GeradorFolha
{
    const string Fonte = "Times New Roman";
    const int W = 9638; // largura útil (A4, margens de 2 cm), em DXA
    readonly Config _cfg;
    public GeradorFolha(Config cfg) { _cfg = cfg; }

    public class Dados
    {
        public Policial Policial = null!;
        public Periodo Periodo = null!;
        public List<Publicacao> Publicacoes = new();
        public string? Comportamento;
        public string ConfBi = "";
    }

    // ---------- helpers OpenXML ----------

    static Run R(string texto, bool negrito = false, bool sublinhado = false, string? cor = null, int tamanho = 22)
    {
        var rp = new RunProperties();
        if (negrito) rp.Append(new Bold());
        if (cor != null) rp.Append(new Color { Val = cor });
        rp.Append(new FontSize { Val = tamanho.ToString() });
        if (sublinhado) rp.Append(new Underline { Val = UnderlineValues.Single });
        var run = new Run(rp);
        var partes = Texto.Limpa(texto).Split('\t');
        for (int i = 0; i < partes.Length; i++)
        {
            if (i > 0) run.Append(new TabChar());
            if (partes[i].Length > 0) run.Append(new Text(partes[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return run;
    }

    static Paragraph P(IEnumerable<Run> runs, JustificationValues? alin = null, int antes = 0, int depois = 40,
                       bool manterComProximo = false, Tabs? tabs = null)
    {
        var pp = new ParagraphProperties();
        if (manterComProximo) pp.Append(new KeepNext());
        if (tabs != null) pp.Append(tabs);
        pp.Append(new SpacingBetweenLines { Before = antes.ToString(), After = depois.ToString(), Line = "264", LineRule = LineSpacingRuleValues.Auto });
        pp.Append(new Justification { Val = alin ?? JustificationValues.Left });
        var p = new Paragraph(pp);
        foreach (var r in runs) p.Append(r);
        return p;
    }

    static Paragraph P(Run r, JustificationValues? alin = null, int antes = 0, int depois = 40, bool manter = false) =>
        P(new[] { r }, alin, antes, depois, manter);

    static Tabs TabDireitaPontilhado() =>
        new(new TabStop { Val = TabStopValues.Right, Leader = TabStopLeaderCharValues.Dot, Position = W - 300 });

    static Paragraph Pontilhado(string rotulo, string valor, bool negrito) =>
        P(new[] { R(rotulo, negrito), R("\t " + valor, negrito) }, tabs: TabDireitaPontilhado());

    static TableBorders Bordas() => new(
        new TopBorder { Val = BorderValues.Single, Size = 6 }, new LeftBorder { Val = BorderValues.Single, Size = 6 },
        new BottomBorder { Val = BorderValues.Single, Size = 6 }, new RightBorder { Val = BorderValues.Single, Size = 6 },
        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6 }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 6 });

    static TableCell Celula(int largura, IEnumerable<OpenXmlElement> conteudo, int span = 1, string? fundo = null)
    {
        var tcp = new TableCellProperties(new TableCellWidth { Width = largura.ToString(), Type = TableWidthUnitValues.Dxa });
        if (span > 1) tcp.Append(new GridSpan { Val = span });
        if (fundo != null) tcp.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fundo, Color = "auto" });
        tcp.Append(new TableCellMargin(new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
            new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa }));
        var c = new TableCell(tcp);
        foreach (var e in conteudo) c.Append(e);
        return c;
    }

    static Table NovaTabela(int[] larguras)
    {
        var t = new Table(new TableProperties(
            new TableWidth { Width = W.ToString(), Type = TableWidthUnitValues.Dxa },
            Bordas(),
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(new TableCellLeftMargin { Width = 100, Type = TableWidthValues.Dxa },
                                       new TableCellRightMargin { Width = 100, Type = TableWidthValues.Dxa })));
        var grid = new TableGrid();
        foreach (var l in larguras) grid.Append(new GridColumn { Width = l.ToString() });
        t.Append(grid);
        return t;
    }

    // ---------- negrito no nome do militar ----------

    static Regex RegexNome(string nome)
    {
        var sb = new StringBuilder();
        var toks = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < toks.Length; i++)
        {
            if (i > 0) sb.Append(@"\s+");
            foreach (var c in toks[i])
            {
                if (char.IsLetter(c))
                {
                    var s = Texto.SemAcento(c.ToString());
                    sb.Append('[').Append(c).Append(char.ToLower(c)).Append(s).Append(s.ToLower()).Append(']');
                }
                else sb.Append(Regex.Escape(c.ToString()));
            }
        }
        return new Regex(sb.ToString());
    }

    static IEnumerable<Run> ComNegrito(string texto, Regex nome)
    {
        int pos = 0;
        foreach (Match m in nome.Matches(texto))
        {
            if (m.Index > pos) yield return R(texto[pos..m.Index]);
            yield return R(m.Value, true);
            pos = m.Index + m.Length;
        }
        if (pos < texto.Length) yield return R(texto[pos..]);
    }

    // ---------- partes da folha ----------

    Table Cabecalho(Dados d)
    {
        var p = d.Policial;
        int meia = W / 2;
        var esquerda = new List<OpenXmlElement>
        {
            P(R("POLÍCIA MILITAR DO ESTADO DE RONDÔNIA", true)),
            P(R(_cfg.Unidade, true, true), depois: 200),
            P(new[] { R("NOME: ", true), R(p.Nome) }),
            P(new[] { R("POSTO/GRAD.: ", true), R(p.PostoGrad) }),
            P(new[] { R("QUALIFICAÇÃO PM: ", true), R(p.Quadro) }),
            P(new[] { R("REG. ESTATÍSTICO: ", true), R(p.Matricula) }),
        };
        var dir = JustificationValues.Right;
        var direita = new List<OpenXmlElement>
        {
            P(R(""), dir),
            P(R("FOLHA DE ALTERAÇÕES", true), dir, depois: 200),
            P(new[] { R("FOLHA Nº ", true), R(d.Periodo.FolhaNumero) }, dir),
            P(R(d.Periodo.Titulo, d.Periodo.Quadrimestre != null), dir),
            P(new[] { R("PERÍODO: ", true), R(d.Periodo.Texto) }, dir),
        };
        var comp = p.EhOficial ? "" : $"\"COMPORTAMENTO {(string.IsNullOrWhiteSpace(d.Comportamento) ? "__________" : d.Comportamento)}\"";
        var partes = new List<OpenXmlElement>
        {
            P(R("1ª PARTE:", true, true)),
            P(new[] { R("INCLUSÃO: ", true), R("SEM ALTERAÇÃO."), R("\tCONF BI " + d.ConfBi, cor: "C00000") },
              tabs: new Tabs(new TabStop { Val = TabStopValues.Right, Position = W - 300 })),
            P(new[] { R("EXCLUSÃO: ", true), R("SEM ALTERAÇÃO.") }, depois: 200),
            P(new[] { R("2ª PARTE:", true, true), R(comp.Length > 0 ? "   " + comp : "", true) }),
            Pontilhado("TEMPO COMPUTADO DE EFETIVO SERVIÇO (TC) ", d.Periodo.Tc, true),
            Pontilhado("ARREGIMENTADO ", d.Periodo.Tc, false),
            Pontilhado("NÃO ARREGIMENTADO ", "00 A 00 M 00 D", false),
            P(R("= CURSO")),
            Pontilhado("TEMPO NÃO COMPUTADO (TNC) ", "00 A 00 M 00 D", true),
            P(new[] { R("DE\tA\t=") }, depois: 200,
              tabs: new Tabs(new TabStop { Val = TabStopValues.Left, Position = 3400 }, new TabStop { Val = TabStopValues.Left, Position = 6400 })),
            P(R("3ª PARTE: ASSUNTOS GERAIS E ADMINISTRATIVOS", true, true), depois: 80),
        };
        var t = NovaTabela(new[] { meia, W - meia });
        t.Append(new TableRow(Celula(meia, esquerda), Celula(W - meia, direita)));
        t.Append(new TableRow(Celula(W, partes, span: 2)));
        return t;
    }

    static Table TabelaDados(Bloco b)
    {
        var todas = new List<List<string>>();
        if (b.Cabecalho != null) todas.Add(b.Cabecalho);
        todas.AddRange(b.Linhas ?? new());
        int n = Math.Max(1, todas.Max(r => r.Count));
        // larguras proporcionais ao conteúdo
        var need = Enumerable.Range(0, n).Select(i => todas.Max(r =>
        {
            var s = i < r.Count ? r[i] ?? "" : "";
            var maiorPalavra = s.Split(' ').Select(w => w.Length + 1.5).DefaultIfEmpty(0).Max();
            return Math.Min(28, Math.Max(maiorPalavra, Math.Sqrt(s.Length) * 2.2));
        })).Select(x => Math.Max(7, x)).ToArray();
        double tot = need.Sum();
        var larg = need.Select(x => (int)(W * x / tot)).ToArray();
        larg[^1] = W - larg[..^1].Sum();
        var t = NovaTabela(larg);
        bool primeira = true;
        foreach (var r in todas)
        {
            bool cab = primeira && b.Cabecalho != null;
            var tr = new TableRow();
            if (cab) tr.Append(new TableRowProperties(new TableHeader()));
            for (int i = 0; i < n; i++)
            {
                var txt = i < r.Count ? r[i] ?? "" : "";
                tr.Append(Celula(larg[i], new[] { P(R(txt, cab, tamanho: 18), JustificationValues.Center, depois: 0) }, fundo: cab ? "EDEDED" : null));
            }
            t.Append(tr);
            primeira = false;
        }
        return t;
    }

    IEnumerable<OpenXmlElement> Corpo(Dados d)
    {
        var nome = RegexNome(d.Policial.Nome);
        if (d.Publicacoes.Count == 0)
        {
            yield return P(R("SEM ALTERAÇÃO."), antes: 120);
        }
        else
        {
            int n = 0;
            string? rotAnterior = null;
            foreach (var pub in d.Publicacoes)
            {
                if (pub.Rotulo != rotAnterior)
                {
                    n++;
                    yield return P(R($"{n}. {pub.Rotulo} :", true), antes: 160, depois: 60, manter: true);
                    rotAnterior = pub.Rotulo;
                }
                var cabecalho = $"BI Nº {pub.BiNumero:00} CIPO/P-1 de {pub.BiData.Day:00} de {Texto.Capitaliza(Texto.Meses[pub.BiData.Month - 1])} de {pub.BiData.Year}. ";
                bool primeiro = true;
                foreach (var b in pub.Blocos)
                {
                    if (b.Tipo == "p")
                    {
                        var runs = new List<Run>();
                        if (primeiro)
                        {
                            runs.Add(R(cabecalho, true));
                            if (!string.IsNullOrEmpty(pub.BiSeiId)) runs.Add(R($"({pub.BiSeiId}) "));
                            runs.Add(R("PUBLICOU: ", true));
                        }
                        runs.AddRange(ComNegrito(b.Texto, nome));
                        yield return P(runs, JustificationValues.Both, depois: 80);
                    }
                    else
                    {
                        if (primeiro)
                        {
                            var runs = new List<Run> { R(cabecalho, true) };
                            if (!string.IsNullOrEmpty(pub.BiSeiId)) runs.Add(R($"({pub.BiSeiId}) "));
                            runs.Add(R("PUBLICOU:", true));
                            yield return P(runs, depois: 60, manterComProximo: true);
                        }
                        yield return TabelaDados(b);
                        yield return P(R(""), depois: 40);
                    }
                    primeiro = false;
                }
            }
        }
        yield return P(R(_cfg.LocalData), JustificationValues.Right, antes: 480, depois: 480);
        yield return P(R(_cfg.ChefeP1Nome, true), JustificationValues.Center, depois: 0);
        yield return P(R(_cfg.ChefeP1Cargo), JustificationValues.Center);
    }

    public void Gerar(Dados d, string caminho)
    {
        using var doc = WordprocessingDocument.Create(caminho, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var estilos = main.AddNewPart<StyleDefinitionsPart>();
        estilos.Styles = new Styles(new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = Fonte, HighAnsi = Fonte, ComplexScript = Fonte, EastAsia = Fonte },
                new FontSize { Val = "22" }, new Languages { Val = "pt-BR" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { After = "0", Line = "264", LineRule = LineSpacingRuleValues.Auto }))));
        var body = new Body();
        body.Append(P(R("FOLHA DE ALTERAÇÃO", true, tamanho: 24), JustificationValues.Center, depois: 200));
        body.Append(Cabecalho(d));
        foreach (var e in Corpo(d)) body.Append(e);
        body.Append(new SectionProperties(
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin { Top = 1134, Bottom = 1134, Left = 1134U, Right = 1134U, Header = 708U, Footer = 708U, Gutter = 0U }));
        main.Document = new Document(body);
        main.Document.Save();
    }
}
