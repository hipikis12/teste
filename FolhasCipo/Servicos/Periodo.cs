namespace FolhasCipo.Servicos;

/// <summary>Período de uma folha de alteração (anual para cabos/soldados, quadrimestral para oficiais/sargentos).</summary>
public class Periodo
{
    public DateTime Inicio { get; init; }
    public DateTime Fim { get; init; }
    public int? Quadrimestre { get; init; }
    public string Titulo { get; init; } = "";   // "2026" ou "1° QUADRIMESTRE 2026"
    public string Texto { get; init; } = "";    // "janeiro a dezembro de 2026" ou "01 Fev a 31 Mai 26"
    public string FolhaNumero { get; init; } = "01";
    public string Tc { get; init; } = "01 A 00 M 00 D";
    public string Sufixo { get; init; } = "";   // para o nome do arquivo

    static string A2(int ano) => (ano % 100).ToString("00");

    public static Periodo Anual(int ano) => new()
    {
        Inicio = new DateTime(ano, 1, 1), Fim = new DateTime(ano, 12, 31),
        Titulo = ano.ToString(), Texto = $"janeiro a dezembro de {ano}", FolhaNumero = "01",
        Tc = "01 A 00 M 00 D", Sufixo = ano.ToString(),
    };

    /// <summary>1º: 01/fev–31/mai; 2º: 01/jun–30/set; 3º: 01/out–31/jan do ano seguinte.</summary>
    public static Periodo Quadrimestral(int ano, int q) => q switch
    {
        1 => new() { Inicio = new(ano, 2, 1), Fim = new(ano, 5, 31), Quadrimestre = 1, Titulo = $"1° QUADRIMESTRE {ano}",
                     Texto = $"01 Fev a 31 Mai {A2(ano)}", FolhaNumero = "01", Tc = "00 A 04 M 00 D", Sufixo = $"1 Quadrimestre {ano}" },
        2 => new() { Inicio = new(ano, 6, 1), Fim = new(ano, 9, 30), Quadrimestre = 2, Titulo = $"2° QUADRIMESTRE {ano}",
                     Texto = $"01 Jun a 30 Set {A2(ano)}", FolhaNumero = "02", Tc = "00 A 04 M 00 D", Sufixo = $"2 Quadrimestre {ano}" },
        3 => new() { Inicio = new(ano, 10, 1), Fim = new(ano + 1, 1, 31), Quadrimestre = 3, Titulo = $"3° QUADRIMESTRE {ano}",
                     Texto = $"01 Out {A2(ano)} a 31 Jan {A2(ano + 1)}", FolhaNumero = "03", Tc = "00 A 04 M 00 D", Sufixo = $"3 Quadrimestre {ano}" },
        _ => throw new ArgumentOutOfRangeException(nameof(q)),
    };

    /// <summary>Quadrimestre que contém a data (janeiro pertence ao 3º do ano anterior).</summary>
    public static (int ano, int q) QuadrimestreDe(DateTime d) =>
        d.Month == 1 ? (d.Year - 1, 3) : d.Month <= 5 ? (d.Year, 1) : d.Month <= 9 ? (d.Year, 2) : (d.Year, 3);
}
