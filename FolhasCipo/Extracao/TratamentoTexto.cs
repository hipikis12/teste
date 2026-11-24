using System.Text.RegularExpressions;

namespace FolhasCipo.Extracao;

/// <summary>Tratamentos de texto do ato antes de ir para a folha (elogio coletivo, lixo de PDF).</summary>
static class TratamentoTexto
{
    const RegexOptions O = RegexOptions.Compiled;

    static readonly Regex EntradaPosto = new(@"(?=\b(?:CB|SD|ASP|ST|SUB ?TEN|CAP|MAJ|TEN ?CEL|CEL|[123]º SGT|[12]º TEN)\s+(?:QPPM|QOPM|QOAPM|QPPMS|QOPMS|QOA ?PM|QPM|PM)\b)", O);
    static readonly Regex ListaElogio = new(@"^(Elogio\s+(?:os|aos|as|às)\s+(?:Policiais\s+Militares|PPMM|militares)\s*)(.*?)(\s+(?:pelo|pela|pelos|pelas|por|em raz[ãa]o|devido|em virtude|que)\s.*)$", O | RegexOptions.Singleline);

    /// <summary>Elogio coletivo: mantém só a entrada do próprio PM na lista de nomes e abrevia as demais com (...).</summary>
    public static string ReduzListaElogio(string txt, string nomeNorm)
    {
        if (nomeNorm.Length == 0) return txt;
        var m = ListaElogio.Match(txt);
        if (!m.Success) return txt;
        var entradas = EntradaPosto.Split(m.Groups[2].Value).Select(e => e.Trim()).Where(e => e.Length > 0).ToList();
        if (entradas.Count < 3) return txt;
        var saida = new List<string>();
        bool omitiu = false, achou = false;
        foreach (var e in entradas)
        {
            if (Regex.Replace(Texto.Norm(e), @"\s+", " ").Contains(nomeNorm)) { saida.Add(e.TrimEnd(',', ' ')); omitiu = false; achou = true; }
            else if (!omitiu) { saida.Add("(...)"); omitiu = true; }
        }
        if (!achou) return txt;
        var lista = string.Join(", ", saida).Replace("(...), ", "(...) ").Replace(", (...)", " (...)");
        return m.Groups[1].Value + lista + m.Groups[3].Value;
    }

    /// <summary>Remove lixo de linhas sobrepostas do PDF (trecho repetido, sobras de letras).</summary>
    public static string LimpaSobreposicao(string txt)
    {
        foreach (Match m in Regex.Matches(txt, @"Art\. \d+º [^.]{35}"))
        {
            int i2 = txt.IndexOf(m.Value, m.Index + 1, StringComparison.Ordinal);
            if (i2 <= 0 || i2 - m.Index >= 600) continue;
            var seg = txt.Substring(m.Index, i2 - m.Index).Trim();
            if (Regex.IsMatch(seg, @"[\.;:]$") || Regex.IsMatch(seg, @"LEIA-SE|SEM EFEITO|TORNAR|ONDE SE LÊ", RegexOptions.IgnoreCase)) continue;
            txt = txt.Remove(m.Index, i2 - m.Index);
            break;
        }
        txt = Regex.Replace(txt, @"\bPol (Militar|Civil)\b", "Polícia $1");
        txt = Regex.Replace(txt, @"(.{25,160}?\))[,;]?\s+\1", "$1");
        txt = Regex.Replace(txt, @"(\(\.\.\.\))\s+(?:[a-zíãõé]{1,5}\s+){1,4}[a-zíãõé]{1,5}$", "$1");
        return txt;
    }
}
