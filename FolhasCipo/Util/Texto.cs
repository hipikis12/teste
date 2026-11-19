using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FolhasCipo;

public static class Texto
{
    public static bool Contem(string s, params string[] partes) =>
        partes.Any(p => (s ?? "").Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Remove acentos (º vira "o", como no NFKD).</summary>
    public static string SemAcento(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var n = s.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(n.Length);
        foreach (var c in n)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c < 0x2000 ? c : ' ');
        return sb.ToString();
    }

    static readonly Regex NaoAlnum = new(@"[^A-Z0-9\*]+", RegexOptions.Compiled);
    static readonly Regex Espacos = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Normaliza para comparação: sem acento, maiúsculo, só letras/dígitos/asterisco.</summary>
    public static string Norm(string? s)
    {
        var t = SemAcento(s).ToUpperInvariant();
        t = NaoAlnum.Replace(t, " ");
        return Espacos.Replace(t, " ").Trim();
    }

    public static string Compacta(string s) => Espacos.Replace(s ?? "", " ").Trim();

    /// <summary>Remove caracteres de controle que tornam o XML do Word inválido.</summary>
    public static string Limpa(string? s) =>
        Regex.Replace(s ?? "", @"[\x00-\x08\x0B\x0C\x0E-\x1F￾￿]", "");

    public static double Similaridade(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return 1.0 - (double)d[a.Length, b.Length] / Math.Max(a.Length, b.Length);
    }

    /// <summary>Proporção de letras maiúsculas entre as letras do texto.</summary>
    public static double ProporcaoMaiusculas(string s)
    {
        int letras = 0, mai = 0;
        foreach (var c in s)
            if (char.IsLetter(c)) { letras++; if (char.IsUpper(c)) mai++; }
        return letras == 0 ? 0 : (double)mai / letras;
    }

    public static readonly string[] Meses =
        { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };
    public static readonly string[] MesesAbrev =
        { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };

    public static int MesPorNome(string nome)
    {
        var n = SemAcento(nome).ToLowerInvariant();
        for (int i = 0; i < 12; i++)
            if (SemAcento(Meses[i]) == n) return i + 1;
        return 0;
    }

    public static string Capitaliza(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
