using System.Text.Json;

namespace FolhasCipo;

public class Policial
{
    public int Id { get; set; }
    public int Antiguidade { get; set; }
    public string PostoGrad { get; set; } = "";
    public string Quadro { get; set; } = "";
    public string Matricula { get; set; } = "";
    public string Nome { get; set; } = "";
    public string NomeGuerra { get; set; } = "";
    public string Sexo { get; set; } = "";
    public string Unidade { get; set; } = "";
    /// <summary>Comportamento padrão (usado quando não há publicação de comportamento no período).</summary>
    public string Comportamento { get; set; } = "";
    /// <summary>"AUTO", "ANUAL" ou "QUADRIMESTRAL".</summary>
    public string TipoFolha { get; set; } = "AUTO";
    public bool Ativo { get; set; } = true;
    public string Observacao { get; set; } = "";

    /// <summary>Chegada na unidade (quando ocorre no período, a folha começa nessa data e a 1ª parte cita o documento).</summary>
    public DateTime? InclusaoData { get; set; }
    public string InclusaoDoc { get; set; } = "";
    /// <summary>Saída da unidade (a folha termina nessa data e a 1ª parte cita o documento).</summary>
    public DateTime? ExclusaoData { get; set; }
    public string ExclusaoDoc { get; set; } = "";

    public bool EhOficial => Texto.Contem(PostoGrad, "TEN", "CAP", "MAJ", "CEL");

    /// <summary>Oficiais e sargentos (inclui ST) têm folha quadrimestral; cabos e soldados, anual.</summary>
    public bool FolhaQuadrimestral => TipoFolha switch
    {
        "ANUAL" => false,
        "QUADRIMESTRAL" => true,
        _ => EhOficial || Texto.Contem(PostoGrad, "SGT", "ST ", "SUB") || PostoGrad.Trim().StartsWith("ST"),
    };

    public string Descricao => $"{PostoGrad} {NomeGuerra} - {Matricula}";
    public override string ToString() => Descricao;
}

public class Boletim
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public int Ano { get; set; }
    public DateTime Data { get; set; }
    public string SeiId { get; set; } = "";
    public string Arquivo { get; set; } = "";
    public DateTime ImportadoEm { get; set; }
    public int QtdPublicacoes { get; set; }

    public string Descricao => $"BI Nº {Numero:00}/{Ano} - {Data:dd/MM/yyyy}";
    public override string ToString() => Descricao;
}

/// <summary>Bloco de conteúdo de uma publicação: parágrafo corrido ou tabela.</summary>
public class Bloco
{
    public string Tipo { get; set; } = "p"; // "p" ou "tabela"
    public string Texto { get; set; } = "";
    public List<string>? Cabecalho { get; set; }
    public List<List<string>>? Linhas { get; set; }

    public static Bloco Paragrafo(string t) => new() { Tipo = "p", Texto = t };
    public static Bloco Tabela(List<string>? cab, List<List<string>> linhas) => new() { Tipo = "tabela", Cabecalho = cab, Linhas = linhas };
}

public class Publicacao
{
    public int Id { get; set; }
    public int PolicialId { get; set; }
    public int BoletimId { get; set; }
    public string Rotulo { get; set; } = "";
    public List<Bloco> Blocos { get; set; } = new();
    public int Ordem { get; set; }
    public bool Manual { get; set; }
    public bool Editada { get; set; }

    // preenchidos na leitura (join)
    public int BiNumero { get; set; }
    public int BiAno { get; set; }
    public DateTime BiData { get; set; }
    public string BiSeiId { get; set; } = "";

    public string BlocosJson
    {
        get => JsonSerializer.Serialize(Blocos);
        set => Blocos = string.IsNullOrWhiteSpace(value) ? new() : JsonSerializer.Deserialize<List<Bloco>>(value) ?? new();
    }

    public string Previa
    {
        get
        {
            var b = Blocos.FirstOrDefault(x => x.Tipo == "p");
            var t = b?.Texto ?? (Blocos.Count > 0 ? "[tabela] " + string.Join(" | ", Blocos[0].Linhas?.FirstOrDefault() ?? new()) : "");
            return t.Length > 180 ? t[..180] + "…" : t;
        }
    }
}

/// <summary>Configurações gerais (assinatura, pasta de BIs etc.).</summary>
public class Config
{
    public string ChefeP1Nome { get; set; } = "NOME DO CHEFE DA P1 - POSTO QUADRO";
    public string ChefeP1Cargo { get; set; } = "Chefe da P1/CIPO";
    public string LocalData { get; set; } = "Buritis-RO, datado e assinado conforme sistema.";
    public string Unidade { get; set; } = "CIPO";
    public string PastaBIs { get; set; } = "";
    public string PastaSaida { get; set; } = "";
}
