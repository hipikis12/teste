using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FolhasCipo.Extracao;

/// <summary>Item do fluxo de leitura: uma linha de texto ou uma linha de tabela (com células).</summary>
public class Item
{
    public int Pagina { get; set; }
    public double Y { get; set; }          // distância do topo da página
    public double X { get; set; }
    public bool EhTabela { get; set; }
    public string Texto { get; set; } = "";
    public List<string> Celulas { get; set; } = new();
    public bool Assinatura { get; set; }

    public string TextoCompleto => EhTabela ? string.Join(" | ", Celulas) : Texto;
    public override string ToString() => $"p{Pagina} y{Y:0} {(EhTabela ? "[T] " : "")}{TextoCompleto}";
}

/// <summary>
/// Lê o PDF dos boletins com o PdfPig e devolve um fluxo linear de itens em ordem de leitura.
/// Tabelas são reconstruídas a partir das linhas (bordas) desenhadas na página.
/// Linhas de tabelas usadas só como moldura (uma célula preenchida) viram texto comum.
/// </summary>
public class LeitorPdf
{
    static readonly Regex Rodape = new(@"(Boletim\s+Interno\s+N[ºo°]?\s*\d+\s*\(\d{8}\)|SEI\s+\d{4}\.\d{6}/\d{4}-\d{2}\s*/\s*pg\.)", RegexOptions.Compiled);
    static readonly Regex RodapeId = new(@"Boletim\s+Interno\s+N[ºo°]?\s*(\d+)\s*\((\d{8})\)", RegexOptions.Compiled);

    /// <summary>Número do BI → id do documento SEI (lido do rodapé das páginas).</summary>
    public Dictionary<int, string> IdsSei { get; } = new();

    record Seg(double A, double B0, double B1); // A = coordenada fixa; [B0,B1] = extensão
    record Celula(double X0, double Y0, double X1, double Y1);
    record Letra(string V, double X0, double X1, double Base, double Tam);

    public List<Item> Ler(string caminho)
    {
        var itens = new List<Item>();
        using var doc = PdfDocument.Open(caminho);
        foreach (var page in doc.GetPages())
            itens.AddRange(LerPagina(page));
        return itens.OrderBy(i => i.Pagina).ThenBy(i => i.Y).ToList();
    }

    IEnumerable<Item> LerPagina(Page page)
    {
        double H = page.Height;
        // 1) letras sem duplicatas (negrito simulado imprime a letra duas vezes)
        var vistos = new HashSet<(string, long, long)>();
        var letras = new List<Letra>();
        foreach (var l in page.Letters)
        {
            if (string.IsNullOrEmpty(l.Value)) continue;
            var key = (l.Value, (long)Math.Round(l.GlyphRectangle.Left * 2), (long)Math.Round(l.StartBaseLine.Y * 2));
            if (!vistos.Add(key)) continue;
            letras.Add(new Letra(l.Value, l.GlyphRectangle.Left, l.GlyphRectangle.Right, H - l.StartBaseLine.Y,
                l.PointSize > 0 ? l.PointSize : Math.Max(4, l.GlyphRectangle.Height)));
        }

        // 2) bordas horizontais e verticais
        var hs = new List<Seg>();
        var vs = new List<Seg>();
        try
        {
            foreach (var path in page.ExperimentalAccess.Paths)
                foreach (var sub in path)
                {
                    var rr = sub.GetBoundingRectangle();
                    if (rr == null) continue;
                    var r = rr.Value;
                    double x0 = r.Left, x1 = r.Right, y0 = H - r.Top, y1 = H - r.Bottom;
                    double w = x1 - x0, h = y1 - y0;
                    if (h <= 2.5 && w > 3) hs.Add(new Seg((y0 + y1) / 2, x0, x1));
                    else if (w <= 2.5 && h > 3) vs.Add(new Seg((x0 + x1) / 2, y0, y1));
                    else if (w > 3 && h > 3 && path.IsStroked)
                    {
                        hs.Add(new Seg(y0, x0, x1)); hs.Add(new Seg(y1, x0, x1));
                        vs.Add(new Seg(x0, y0, y1)); vs.Add(new Seg(x1, y0, y1));
                    }
                }
        }
        catch { /* página sem caminhos legíveis: segue só com texto */ }
        hs = Mesclar(hs);
        vs = Mesclar(vs);
        var celulas = Celulas(hs, vs);

        // 3) linhas de tabela: células agrupadas pelo topo
        var usadas = new HashSet<Letra>();
        var saida = new List<Item>();
        foreach (var grupo in celulas.GroupBy(c => Math.Round(c.Y0 / 2) * 2).OrderBy(g => g.Key))
        {
            var cels = grupo.OrderBy(c => c.X0).ToList();
            var textos = new List<string>();
            var porCelula = new List<List<Letra>>();
            foreach (var c in cels)
            {
                var dentro = letras.Where(l => Dentro(l, c)).ToList();
                porCelula.Add(dentro);
                textos.Add(MontarTexto(dentro));
            }
            int preenchidas = textos.Count(t => t.Trim().Length > 0);
            if (preenchidas < 2) continue; // moldura: o texto sai como linhas comuns
            foreach (var d in porCelula) foreach (var l in d) usadas.Add(l);
            var linha = string.Join(" ", textos);
            if (Rodape.IsMatch(linha)) continue;
            saida.Add(new Item { Pagina = page.Number, Y = cels.Min(c => c.Y0), X = cels.Min(c => c.X0), EhTabela = true, Celulas = textos });
        }

        // 4) texto comum (fora das tabelas)
        foreach (var (y, x, t) in Linhas(letras.Where(l => !usadas.Contains(l)).ToList()))
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            var m = RodapeId.Match(t);
            if (m.Success) IdsSei[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
            if (Rodape.IsMatch(t)) continue;
            saida.Add(new Item { Pagina = page.Number, Y = y, X = x, Texto = t.Trim() });
        }
        return saida;
    }

    static bool Dentro(Letra l, Celula c)
    {
        double cx = (l.X0 + l.X1) / 2, cy = l.Base - l.Tam * 0.3;
        return cx >= c.X0 - 0.5 && cx <= c.X1 + 0.5 && cy >= c.Y0 - 0.5 && cy <= c.Y1 + 0.5;
    }

    static string MontarTexto(List<Letra> ls) => string.Join("\n", Linhas(ls).Select(l => l.texto.Trim()).Where(t => t.Length > 0));

    /// <summary>Agrupa letras em linhas (pela linha de base) e insere espaços pelos intervalos.</summary>
    static List<(double y, double x, string texto)> Linhas(List<Letra> ls)
    {
        var res = new List<(double, double, string)>();
        if (ls.Count == 0) return res;
        var ord = ls.OrderBy(l => l.Base).ThenBy(l => l.X0).ToList();
        var atual = new List<Letra> { ord[0] };
        double baseAtual = ord[0].Base;
        void Fecha()
        {
            var linha = atual.OrderBy(l => l.X0).ToList();
            var sb = new StringBuilder();
            Letra? ant = null;
            foreach (var l in linha)
            {
                if (ant != null && l.V != " " && !sb.ToString().EndsWith(" "))
                {
                    double gap = l.X0 - ant.X1;
                    if (gap > Math.Max(1.0, l.Tam * 0.18)) sb.Append(' ');
                }
                sb.Append(l.V);
                ant = l;
            }
            var topo = linha.Min(l => l.Base - l.Tam * 0.8);
            res.Add((topo, linha[0].X0, Regex.Replace(sb.ToString(), @"\s+", " ")));
        }
        for (int i = 1; i < ord.Count; i++)
        {
            var l = ord[i];
            if (Math.Abs(l.Base - baseAtual) <= Math.Max(1.5, l.Tam * 0.3)) atual.Add(l);
            else { Fecha(); atual = new List<Letra> { l }; baseAtual = l.Base; }
        }
        Fecha();
        return res;
    }

    /// <summary>Junta segmentos colineares que se tocam.</summary>
    static List<Seg> Mesclar(List<Seg> segs)
    {
        var res = new List<Seg>();
        foreach (var g in segs.OrderBy(s => s.A).ThenBy(s => s.B0).GroupBy(s => Math.Round(s.A)))
        {
            Seg? cur = null;
            foreach (var s in g.OrderBy(s => s.B0))
            {
                if (cur == null) { cur = s; continue; }
                if (s.B0 <= cur.B1 + 2) cur = cur with { B1 = Math.Max(cur.B1, s.B1) };
                else { res.Add(cur); cur = s; }
            }
            if (cur != null) res.Add(cur);
        }
        return res;
    }

    static bool CobreH(List<Seg> hs, double y, double x0, double x1) =>
        hs.Any(h => Math.Abs(h.A - y) <= 2 && h.B0 <= x0 + 2 && h.B1 >= x1 - 2);

    static bool CobreV(List<Seg> vs, double x, double y0, double y1) =>
        vs.Any(v => Math.Abs(v.A - x) <= 2 && v.B0 <= y0 + 2 && v.B1 >= y1 - 2);

    /// <summary>Encontra as células: retângulos fechados pelas bordas (mesma ideia do pdfplumber).</summary>
    static List<Celula> Celulas(List<Seg> hs, List<Seg> vs)
    {
        var pts = new List<(double x, double y)>();
        foreach (var h in hs)
            foreach (var v in vs)
                if (v.A >= h.B0 - 2 && v.A <= h.B1 + 2 && h.A >= v.B0 - 2 && h.A <= v.B1 + 2)
                    pts.Add((v.A, h.A));
        pts = pts.Distinct().ToList();
        var cels = new List<Celula>();
        foreach (var p in pts)
        {
            var abaixo = pts.Where(q => Math.Abs(q.x - p.x) <= 2 && q.y > p.y + 2).OrderBy(q => q.y).ToList();
            var direita = pts.Where(q => Math.Abs(q.y - p.y) <= 2 && q.x > p.x + 2).OrderBy(q => q.x).ToList();
            bool achou = false;
            foreach (var b in abaixo)
            {
                if (!CobreV(vs, p.x, p.y, b.y)) break;
                foreach (var r in direita)
                {
                    if (!CobreH(hs, p.y, p.x, r.x)) break;
                    if (pts.Any(q => Math.Abs(q.x - r.x) <= 2 && Math.Abs(q.y - b.y) <= 2)
                        && CobreH(hs, b.y, p.x, r.x) && CobreV(vs, r.x, p.y, b.y))
                    {
                        cels.Add(new Celula(p.x, p.y, r.x, b.y));
                        achou = true;
                        break;
                    }
                }
                if (achou) break;
            }
        }
        // remove células que contêm outras (moldura em volta de tabela interna)
        return cels.Where(c => !cels.Any(o => o != c && o.X0 >= c.X0 - 1 && o.X1 <= c.X1 + 1 && o.Y0 >= c.Y0 - 1 && o.Y1 <= c.Y1 + 1
                                           && (o.X1 - o.X0) * (o.Y1 - o.Y0) < (c.X1 - c.X0) * (c.Y1 - c.Y0) * 0.9)).ToList();
    }
}
