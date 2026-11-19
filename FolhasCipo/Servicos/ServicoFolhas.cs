using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace FolhasCipo.Servicos;

/// <summary>Monta e grava as folhas de alteração a partir do banco.</summary>
public class ServicoFolhas
{
    readonly Database _db;
    public ServicoFolhas(Database db) { _db = db; }

    static string Seguro(string s) => Regex.Replace(Texto.SemAcento(s), @"[^A-Za-z0-9 ._-]", "").Trim();

    /// <summary>Comportamento: última publicação de comportamento até o fim do período; senão, o do cadastro.</summary>
    static string? Comportamento(Policial p, List<Publicacao> todas, DateTime fim)
    {
        var ultima = todas.Where(x => x.Rotulo == "COMPORTAMENTO" && x.BiData <= fim).LastOrDefault();
        if (ultima != null)
        {
            var txt = string.Join(" ", ultima.Blocos.Select(b => b.Texto));
            var m = Regex.Match(txt, @"Comportamento\s*;?\s*(EXCEPCIONAL|ÓTIMO|OTIMO|BOM|INSUFICIENTE|MAU)", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant().Replace("OTIMO", "ÓTIMO");
        }
        return string.IsNullOrWhiteSpace(p.Comportamento) ? null : p.Comportamento;
    }

    /// <summary>
    /// Gera as folhas. modo: "AUTO" (cabos anual + oficiais/sargentos no quadrimestre escolhido),
    /// "ANUAL" (todos anual) ou "QUADRIMESTRAL" (todos no quadrimestre escolhido).
    /// </summary>
    public List<string> Gerar(IEnumerable<Policial> policiais, int ano, int quadrimestre, string modo, string pastaSaida, IProgress<string>? progresso = null)
    {
        var cfg = _db.LerConfig();
        var gerador = new GeradorFolha(cfg);
        var boletins = _db.ListarBoletins();
        var arquivos = new List<string>();
        foreach (var p in policiais)
        {
            bool quad = modo switch { "ANUAL" => false, "QUADRIMESTRAL" => true, _ => p.FolhaQuadrimestral };
            var per = quad ? Periodo.Quadrimestral(ano, quadrimestre) : Periodo.Anual(ano);
            var todas = _db.ListarPublicacoes(p.Id);
            var noPeriodo = todas.Where(x => x.BiData >= per.Inicio && x.BiData <= per.Fim).ToList();
            var ultimoBi = boletins.Where(b => b.Data >= per.Inicio && b.Data <= per.Fim).OrderBy(b => b.Data).LastOrDefault();
            var dados = new GeradorFolha.Dados
            {
                Policial = p, Periodo = per, Publicacoes = noPeriodo,
                Comportamento = p.EhOficial ? null : Comportamento(p, todas, per.Fim),
                ConfBi = ultimoBi == null ? "—" : $"{ultimoBi.Numero:00}/{ultimoBi.Ano % 100:00}",
            };
            var sub = quad ? Path.Combine(pastaSaida, "Oficiais e Sargentos - quadrimestral", $"{quadrimestre} Quadrimestre {ano}")
                           : Path.Combine(pastaSaida, $"Anual {ano}");
            Directory.CreateDirectory(sub);
            var nome = $"{p.Antiguidade:00} - Folha de Alteracao {per.Sufixo} - {Seguro(p.PostoGrad.Replace(" PM", ""))} {Seguro(p.Quadro)} {Seguro(p.NomeGuerra)} - {p.Matricula}.docx";
            var caminho = Path.Combine(sub, nome);
            gerador.Gerar(dados, caminho);
            arquivos.Add(caminho);
            progresso?.Report(nome);
        }
        return arquivos;
    }

    /// <summary>Importa o "Relatório do efetivo" (.xlsx) exportado do sistema. Atualiza pela matrícula.</summary>
    public (int novos, int atualizados) ImportarEfetivo(string caminhoXlsx)
    {
        using var wb = new XLWorkbook(caminhoXlsx);
        var ws = wb.Worksheet(1);
        // localiza a linha de cabeçalho (contém "Matrícula")
        var linhas = ws.RowsUsed().ToList();
        int hdr = linhas.FindIndex(r => r.CellsUsed().Any(c => c.GetString().Trim().Equals("Matrícula", StringComparison.OrdinalIgnoreCase)));
        if (hdr < 0) throw new InvalidOperationException("Não encontrei a coluna \"Matrícula\" na planilha.");
        var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in linhas[hdr].CellsUsed()) col[c.GetString().Trim()] = c.Address.ColumnNumber;
        string V(IXLRow r, string nome) => col.TryGetValue(nome, out var i) ? r.Cell(i).GetString().Trim() : "";

        var existentes = _db.ListarPoliciais().ToDictionary(p => p.Matricula);
        int novos = 0, atual = 0, ordem = 0;
        foreach (var r in linhas.Skip(hdr + 1))
        {
            var mat = V(r, "Matrícula");
            if (string.IsNullOrWhiteSpace(mat)) continue;
            ordem++;
            var p = existentes.GetValueOrDefault(mat) ?? new Policial { Matricula = mat };
            if (p.Id == 0) novos++; else atual++;
            p.Antiguidade = ordem;
            p.PostoGrad = V(r, "Posto/Graduação");
            p.Quadro = V(r, "Quadro");
            p.Nome = V(r, "Nome");
            p.NomeGuerra = V(r, "Nome de guerra");
            p.Sexo = V(r, "Sexo");
            p.Unidade = V(r, "Unidade");
            p.Ativo = true;
            _db.SalvarPolicial(p);
        }
        return (novos, atual);
    }
}
