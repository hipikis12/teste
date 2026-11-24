using System.Text.RegularExpressions;

namespace FolhasCipo.Extracao;

/// <summary>Descobre quais policiais do efetivo são citados num texto.</summary>
public class Identificador
{
    readonly List<Policial> _efetivo;
    readonly Dictionary<int, string> _nomeNorm;
    readonly Dictionary<string, int> _porMatricula;
    readonly Dictionary<string, List<int>> _porFinal;
    readonly Dictionary<string, List<Policial>> _homonimos;

    public Identificador(List<Policial> efetivo)
    {
        _efetivo = efetivo;
        _nomeNorm = efetivo.ToDictionary(p => p.Id, p => Texto.Norm(p.Nome));
        _porMatricula = new();
        foreach (var p in efetivo)
        {
            var d = new string(p.Matricula.Where(char.IsDigit).ToArray());
            if (d.Length >= 6) _porMatricula[d] = p.Id;
        }
        _porFinal = efetivo.Where(p => p.Matricula.Length >= 3)
            .GroupBy(p => p.Matricula[^3..]).ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());
        _homonimos = efetivo.GroupBy(p => _nomeNorm[p.Id]).Where(g => g.Count() > 1).ToDictionary(g => g.Key, g => g.ToList());
    }

    public Policial ObterPolicial(int id) => _efetivo.First(p => p.Id == id);
    public string NomeNorm(int id) => _nomeNorm[id];
    public IReadOnlyList<Policial> Efetivo => _efetivo;

    /// <summary>Policiais cujo nome parece estar no texto, mas escrito de forma que o reconhecimento normal não pegou
    /// (palavras faltando, trocadas ou separadas). Serve só para avisar a P1 – não lança nada sozinho.</summary>
    public List<Policial> CitacoesDuvidosas(string texto, IEnumerable<int> reconhecidos)
    {
        var ja = reconhecidos.ToHashSet();
        var toks = Texto.Norm(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var res = new List<Policial>();
        foreach (var p in _efetivo)
        {
            if (ja.Contains(p.Id)) continue;
            var nome = _nomeNorm[p.Id].Split(' ').Where(t => t.Length > 2).Distinct().ToList();
            if (nome.Count < 2) continue;
            for (int i = 0; i < toks.Length; i++)
            {
                if (toks[i] != nome[0]) continue;
                var janela = toks.Skip(i).Take(nome.Count + 3).ToHashSet();
                if (nome.Count(t => janela.Contains(t)) >= Math.Max(2, nome.Count - 1)) { res.Add(p); break; }
            }
        }
        return res;
    }

    /// <summary>Padrão de posto (texto normalizado) para cada graduação.</summary>
    public static string PadraoPosto(string posto)
    {
        var p = Texto.Norm(posto);
        if (p.StartsWith("CB")) return "CB|CABO";
        if (p.StartsWith("SD")) return "SD|SOLDADO";
        if (p.StartsWith("ST") || p.StartsWith("SUB")) return "ST|SUB TEN|SUBTEN|SUBTENENTE";
        if (p.Contains("SGT")) return @"\d ?O SGT|\d SGT|SGT|SARGENTO";
        if (p.Contains("TEN CEL") || p.StartsWith("TC")) return "TEN CEL|TC";
        if (p.Contains("TEN")) return @"\d ?O TEN|\d TEN|TEN|TENENTE";
        if (p.StartsWith("CAP")) return "CAP|CAPITAO";
        if (p.StartsWith("MAJ")) return "MAJ|MAJOR";
        if (p.StartsWith("CEL")) return "CEL|CORONEL";
        return Regex.Escape(p.Split(' ')[0]);
    }

    public HashSet<int> Mencoes(string texto, bool fuzzy = true)
    {
        var nt = " " + Texto.Norm(texto) + " ";
        var achados = new HashSet<int>();
        foreach (var p in _efetivo)
        {
            var nome = " " + _nomeNorm[p.Id] + " ";
            if (!nt.Contains(nome)) continue;
            if (_homonimos.TryGetValue(_nomeNorm[p.Id], out var grupo))
            {
                foreach (Match m in Regex.Matches(nt, Regex.Escape(nome)))
                {
                    var quem = Desempatar(nt, m.Index, grupo);
                    if (quem != null) achados.Add(quem.Value);
                }
            }
            else achados.Add(p.Id);
        }
        // matrícula completa (com ou sem hífen/ponto)
        var digitos = Regex.Replace(texto, @"(?<=\d)[\-\. ](?=\d)", "");
        foreach (Match m in Regex.Matches(digitos, @"\d{7,10}"))
            if (_porMatricula.TryGetValue(m.Value, out var id)) achados.Add(id);
        // RE mascarado (******474) + primeiro nome
        foreach (Match m in Regex.Matches(nt, @"\*{3,}\s*(\d)\s*(\d)\s*(\d)\s+([A-Z ]{3,40})"))
        {
            var fim = m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value;
            if (!_porFinal.TryGetValue(fim, out var ids)) continue;
            var primeiro = m.Groups[4].Value.Trim().Split(' ')[0];
            foreach (var i in ids)
                if (_nomeNorm[i].Split(' ')[0] == primeiro && !_homonimos.ContainsKey(_nomeNorm[i])) achados.Add(i);
        }
        if (fuzzy)
        {
            var toks = nt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in _efetivo)
            {
                if (achados.Contains(p.Id) || _homonimos.ContainsKey(_nomeNorm[p.Id])) continue;
                var pt = _nomeNorm[p.Id].Split(' ');
                int L = pt.Length;
                for (int i = 0; i + L <= toks.Length; i++)
                {
                    if (toks[i + L - 1] != pt[^1] && toks[i] != pt[0]) continue;
                    var janela = string.Join(" ", toks, i, L);
                    if (janela == _nomeNorm[p.Id]) continue;
                    if (Texto.Similaridade(janela, _nomeNorm[p.Id]) >= 0.9) { achados.Add(p.Id); break; }
                }
            }
        }
        return achados;
    }

    /// <summary>Homônimos: decide pelo posto ou pela matrícula próximos da citação.</summary>
    int? Desempatar(string nt, int pos, List<Policial> grupo)
    {
        var antes = nt.Substring(Math.Max(0, pos - 45), Math.Min(45, pos));
        var depois = nt.Substring(pos, Math.Min(70, nt.Length - pos));
        foreach (var p in grupo)
        {
            var mat = new string(p.Matricula.Where(char.IsDigit).ToArray());
            if (mat.Length >= 6 && (antes + depois).Contains(mat[^6..])) return p.Id;
            if (mat.Length >= 3 && Regex.IsMatch(antes + depois[..Math.Min(40, depois.Length)], @"\*+ ?" + mat[^3..] + @"\b")) return p.Id;
        }
        var fimAntes = antes[^Math.Min(30, antes.Length)..];
        foreach (var p in grupo)
            if (Regex.IsMatch(fimAntes, @"\b(" + PadraoPosto(p.PostoGrad) + @")\b")) return p.Id;
        return null;
    }

    /// <summary>Citação só pelo posto + nome de guerra (ex.: "CB PM FULANO").</summary>
    public IEnumerable<int> NomesDeGuerra(string texto, IEnumerable<int> jaCitados)
    {
        var t = " " + Texto.Norm(texto) + " ";
        var conhecidos = jaCitados.Select(i => _nomeNorm[i]).ToList();
        foreach (var p in _efetivo)
        {
            var g = Texto.Norm(p.NomeGuerra);
            if (g.Length < 4) continue;
            var pad = $@" (?:{PadraoPosto(p.PostoGrad)})(?: PM| QPPM| QOAPM| QOPM| QOA PM| QPM)? {Regex.Escape(g)}[ ,\.;]";
            if (!Regex.IsMatch(t, pad)) continue;
            var ultimo = g.Split(' ')[^1];
            if (conhecidos.Any(k => k != _nomeNorm[p.Id] && k.Split(' ').Contains(ultimo))) continue;
            yield return p.Id;
        }
    }
}
