using System.Text;
using System.Text.RegularExpressions;
using FolhasCipo.Extracao;

namespace FolhasCipo.Servicos;

/// <summary>
/// Compara folhas de alteração feitas pela P1 (PDF exportado do SEI) com o que o sistema lançaria
/// para o mesmo policial e período. Serve de gabarito: mostra o BI que falta e o que sobra.
/// </summary>
public class Conferencia
{
    readonly Database _db;
    public Conferencia(Database db) { _db = db; }

    static readonly Dictionary<string, int> Meses = new()
    {
        ["JAN"] = 1, ["FEV"] = 2, ["MAR"] = 3, ["ABR"] = 4, ["MAI"] = 5, ["JUN"] = 6,
        ["JUL"] = 7, ["AGO"] = 8, ["SET"] = 9, ["OUT"] = 10, ["NOV"] = 11, ["DEZ"] = 12,
    };

    /// <summary>Lê o "PERÍODO:" da folha (ex.: "01 Fev a 31 Mai 26", "01/01/2026 a 31/12/2026", "01 JANEIRO A 31 DEZEMBRO DE 2026").</summary>
    static (DateTime ini, DateTime fim)? LerPeriodo(string texto)
    {
        var m = Regex.Match(texto, @"PER[ÍI]ODO:\s*([^\n]+)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var s = FolhasCipo.Texto.SemAcento(m.Groups[1].Value).ToUpperInvariant();
        s = Regex.Split(s, @"\s{2,}")[0];
        var q = Regex.Match(FolhasCipo.Texto.SemAcento(texto).ToUpperInvariant(), @"(\d)\s*[O°º]?\s*QUADRIMESTRE\s*(\d{4})");
        var pts = new List<(int d, int m, int? y)>();
        foreach (Match x in Regex.Matches(s, @"(\d{1,2})\s*(?:/|DE\s+)?\s*(\d{1,2}|[A-Z]{3})[A-Z]*\s*(?:/?\s*(?:DE\s+)?(\d{2,4}))?"))
        {
            var mo = x.Groups[2].Value;
            int mes = int.TryParse(mo, out var mm) ? mm : Meses.GetValueOrDefault(mo.Length >= 3 ? mo[..3] : mo);
            int dia = int.Parse(x.Groups[1].Value);
            if (mes < 1 || mes > 12 || dia > 31) continue;
            int? ano = x.Groups[3].Success && x.Groups[3].Value.Length > 0 ? int.Parse(x.Groups[3].Value) : null;
            if (ano < 100) ano += 2000;
            pts.Add((dia, mes, ano));
        }
        if (pts.Count < 2) return null;
        var a = pts[0]; var b = pts[^1];
        int anoRef = q.Success ? int.Parse(q.Groups[2].Value) : DateTime.Today.Year;
        int yb = b.y ?? a.y ?? (q.Success && q.Groups[1].Value == "3" && b.m == 1 ? anoRef + 1 : anoRef);
        int ya = a.y ?? (a.m > b.m ? yb - 1 : yb);
        try { return (new DateTime(ya, a.m, Math.Min(a.d, DateTime.DaysInMonth(ya, a.m))), new DateTime(yb, b.m, Math.Min(b.d, DateTime.DaysInMonth(yb, b.m)))); }
        catch { return null; }
    }

    public string Conferir(IEnumerable<string> pdfs)
    {
        var sb = new StringBuilder();
        var policiais = _db.ListarPoliciais();
        var boletins = _db.ListarBoletins();
        int ok = 0, falta = 0, sobra = 0;
        foreach (var arq in pdfs)
        {
            var nome = Path.GetFileName(arq);
            string texto;
            try
            {
                var itens = new LeitorPdf().Ler(arq);
                texto = string.Join("\n", itens.Select(i => i.TextoCompleto));
            }
            catch (Exception ex) { sb.AppendLine($"{nome}: não consegui ler ({ex.Message})"); continue; }

            var mat = Regex.Match(texto, @"REG\.?\s*ESTAT[ÍI]STICO:\s*(\d+)");
            var pol = mat.Success ? policiais.FirstOrDefault(p => p.Matricula == mat.Groups[1].Value) : null;
            if (pol == null) { sb.AppendLine($"{nome}: matrícula não encontrada no cadastro."); continue; }
            var per = LerPeriodo(texto);
            if (per == null) { sb.AppendLine($"{nome} ({pol.NomeGuerra}): não entendi o PERÍODO."); continue; }
            var (ini, fim) = per.Value;

            int i3 = texto.IndexOf("3ª PARTE", StringComparison.OrdinalIgnoreCase);
            var corpo = i3 >= 0 ? texto[i3..] : texto;
            var citados = Regex.Matches(corpo, @"\bBI\s*n?[ºo°]\s*\.?\s*(\d{1,2})\b", RegexOptions.IgnoreCase)
                               .Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();
            // BIs da CIPO dentro do período (os demais são de outra unidade ou de outro ano)
            var noPeriodo = boletins.Where(b => b.Data >= ini && b.Data <= fim).ToList();
            var gabarito = citados.Where(n => noPeriodo.Any(b => b.Numero == n)).ToHashSet();
            var fora = citados.Except(gabarito).OrderBy(x => x).ToList();
            var corte = Regex.Match(texto, @"(?:CONF\w*|FEITO|PRONTO)\s*(?:AT[EÉ])?\s*(?:O\s*)?(?:BI|BOLETIM)\s*N?[ºo°]?\s*(\d{1,2})", RegexOptions.IgnoreCase);
            int limite = Math.Max(corte.Success ? int.Parse(corte.Groups[1].Value) : 999, gabarito.Count > 0 ? gabarito.Max() : 0);

            var sistema = _db.ListarPublicacoes(pol.Id, ini, fim).Where(p => p.BiNumero <= limite).Select(p => p.BiNumero).ToHashSet();
            var faltam = gabarito.Except(sistema).OrderBy(x => x).ToList();
            var sobram = sistema.Except(gabarito).OrderBy(x => x).ToList();
            ok += gabarito.Intersect(sistema).Count();
            falta += faltam.Count;
            sobra += sobram.Count;
            sb.Append($"{pol.PostoGrad} {pol.NomeGuerra} ({ini:dd/MM/yy} a {fim:dd/MM/yy}): {gabarito.Intersect(sistema).Count()} iguais");
            if (faltam.Count > 0) sb.Append($" | FALTA no sistema: BI {string.Join(", ", faltam)}");
            if (sobram.Count > 0) sb.Append($" | A MAIS no sistema: BI {string.Join(", ", sobram)}");
            if (fora.Count > 0) sb.Append($" | ignorados (outra unidade/fora do período): {string.Join(", ", fora)}");
            sb.AppendLine();
        }
        int total = ok + falta;
        sb.AppendLine();
        sb.AppendLine($"RESUMO: {ok} lançamentos iguais, {falta} faltando, {sobra} a mais."
                      + (total > 0 ? $" Acerto: {100.0 * ok / total:0.0}% do que a P1 lançou." : ""));
        sb.AppendLine("Obs.: diferenças podem ser engano na folha manual (nº do BI trocado) – confira o BI antes de corrigir o sistema.");
        return sb.ToString();
    }
}
