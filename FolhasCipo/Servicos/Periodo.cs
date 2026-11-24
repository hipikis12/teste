namespace FolhasCipo.Servicos;

/// <summary>Período de uma folha de alteração (anual para cabos/soldados, quadrimestral para oficiais/sargentos).</summary>
public class Periodo
{
    public DateTime Inicio { get; init; }
    public DateTime Fim { get; init; }
    public int? Quadrimestre { get; init; }
    public string Titulo { get; init; } = "";   // "2026" ou "1° QUADRIMESTRE 2026"
    public string Texto { get; init; } = "";    // "janeiro a dezembro de 2026" ou "01 Fev a 31 Mai 26"
    public string FolhaNumero { get; init; } = "01"; // nas folhas da P1 é sempre 01
    public string Tc { get; init; } = "01 A 00 M 00 D";
    public string Sufixo { get; init; } = "";   // para o nome do arquivo
    /// <summary>Texto da 1ª parte (documento de inclusão/exclusão) quando o militar chegou ou saiu dentro do período.</summary>
    public string Inclusao { get; init; } = "SEM ALTERAÇÃO.";
    public string Exclusao { get; init; } = "SEM ALTERAÇÃO.";

    static string A2(int ano) => (ano % 100).ToString("00");
    static string DataCurta(DateTime d) => $"{d.Day:00} {FolhasCipo.Texto.MesesAbrev[d.Month - 1]} {A2(d.Year)}";

    public static Periodo Anual(int ano) => new()
    {
        Inicio = new DateTime(ano, 1, 1), Fim = new DateTime(ano, 12, 31),
        Titulo = ano.ToString(), Texto = $"janeiro a dezembro de {ano}",
        Tc = "01 A 00 M 00 D", Sufixo = ano.ToString(),
    };

    /// <summary>1º: 01/fev–31/mai; 2º: 01/jun–30/set; 3º: 01/out–31/jan do ano seguinte.</summary>
    public static Periodo Quadrimestral(int ano, int q) => q switch
    {
        1 => new() { Inicio = new(ano, 2, 1), Fim = new(ano, 5, 31), Quadrimestre = 1, Titulo = $"1° QUADRIMESTRE {ano}",
                     Texto = $"01 Fev a 31 Mai {A2(ano)}", Tc = "00 A 04 M 00 D", Sufixo = $"1 Quadrimestre {ano}" },
        2 => new() { Inicio = new(ano, 6, 1), Fim = new(ano, 9, 30), Quadrimestre = 2, Titulo = $"2° QUADRIMESTRE {ano}",
                     Texto = $"01 Jun a 30 Set {A2(ano)}", Tc = "00 A 04 M 00 D", Sufixo = $"2 Quadrimestre {ano}" },
        3 => new() { Inicio = new(ano, 10, 1), Fim = new(ano + 1, 1, 31), Quadrimestre = 3, Titulo = $"3° QUADRIMESTRE {ano}",
                     Texto = $"01 Out {A2(ano)} a 31 Jan {A2(ano + 1)}", Tc = "00 A 04 M 00 D", Sufixo = $"3 Quadrimestre {ano}" },
        _ => throw new ArgumentOutOfRangeException(nameof(q)),
    };

    /// <summary>Quadrimestre que contém a data (janeiro pertence ao 3º do ano anterior).</summary>
    public static (int ano, int q) QuadrimestreDe(DateTime d) =>
        d.Month == 1 ? (d.Year - 1, 3) : d.Month <= 5 ? (d.Year, 1) : d.Month <= 9 ? (d.Year, 2) : (d.Year, 3);

    /// <summary>Tempo entre duas datas (inclusive) no formato "00 A 00 M 00 D".</summary>
    public static string Tempo(DateTime ini, DateTime fim)
    {
        var f = fim.Date.AddDays(1);
        int anos = f.Year - ini.Year, meses = f.Month - ini.Month, dias = f.Day - ini.Day;
        if (dias < 0) { meses--; dias += DateTime.DaysInMonth(f.AddMonths(-1).Year, f.AddMonths(-1).Month); }
        if (meses < 0) { anos--; meses += 12; }
        return $"{anos:00} A {meses:00} M {dias:00} D";
    }

    /// <summary>
    /// Recorta o período pela chegada/saída do militar na unidade (como a P1 faz nas folhas de quem foi transferido).
    /// Retorna null se o militar não esteve na unidade em nenhum dia do período.
    /// </summary>
    public Periodo? Recortar(Policial p)
    {
        var ini = Inicio;
        var fim = Fim;
        string inc = "SEM ALTERAÇÃO.", exc = "SEM ALTERAÇÃO.";
        if (p.InclusaoData is { } di && di.Date >= Inicio && di.Date <= Fim)
        {
            ini = di.Date;
            inc = string.IsNullOrWhiteSpace(p.InclusaoDoc) ? $"A contar de {di:dd/MM/yyyy}." : p.InclusaoDoc.Trim();
        }
        else if (p.InclusaoData is { } di2 && di2.Date > Fim) return null;
        if (p.ExclusaoData is { } de && de.Date >= ini && de.Date <= Fim)
        {
            fim = de.Date;
            exc = string.IsNullOrWhiteSpace(p.ExclusaoDoc) ? $"A contar de {de:dd/MM/yyyy}." : p.ExclusaoDoc.Trim();
        }
        else if (p.ExclusaoData is { } de2 && de2.Date < Inicio) return null;
        if (ini == Inicio && fim == Fim && inc == Inclusao && exc == Exclusao) return this;
        return new Periodo
        {
            Inicio = ini, Fim = fim, Quadrimestre = Quadrimestre, Titulo = Titulo,
            Texto = ini == Inicio && fim == Fim ? Texto : $"{DataCurta(ini)} a {DataCurta(fim)}", FolhaNumero = FolhaNumero, Tc = ini == Inicio && fim == Fim ? Tc : Tempo(ini, fim),
            Sufixo = Sufixo, Inclusao = inc, Exclusao = exc,
        };
    }
}
