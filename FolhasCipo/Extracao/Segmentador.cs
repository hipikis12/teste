using System.Text.RegularExpressions;

namespace FolhasCipo.Extracao;

/// <summary>Um ato publicado (portaria, elogio, tabela de férias...) dentro de um BI.</summary>
public class Ato
{
    public int BiNumero { get; set; }
    public DateTime BiData { get; set; }
    public string BiSeiId { get; set; } = "";
    public string Secao { get; set; } = "";
    public List<Item> Itens { get; } = new();
    public List<string> Assinatura { get; } = new();
}

/// <summary>Divide o fluxo de itens em BIs → seções → atos.</summary>
public class Segmentador
{
    const RegexOptions O = RegexOptions.Compiled;

    public const string RANK =
        @"(?:\d\s*[ºª°o]\s*(?:SGT|TEN|SARGENTO|TENENTE)|SUB\s?TEN|ST|CB|SD|CAP|MAJ|TEN\s?CEL|TC|CEL|CORONEL|" +
        @"TERCEIRO-SARGENTO|SEGUNDO-SARGENTO|PRIMEIRO-SARGENTO|PRIMEIRO-TENENTE|SEGUNDO-TENENTE|" +
        @"CAPIT[ÃA]O|SUBTENENTE|CABO|SOLDADO|ASPIRANTE|MAJOR|TENENTE)";

    public static readonly Regex RankRe = new(@"\b" + RANK + @"(?=[\s\.]|$)", O);
    public static readonly Regex EntryStart = new(@"^\s*(?:(?:\d{1,3}|[a-z]|[IVX]+)\s*[\.\)\-–]\s*|[-–•]\s*)?(?:AL\s+)?" + RANK + @"\b", O);
    static readonly Regex Sig1 = new(@"^(?:No Impedimento\s+)?([A-ZÀ-Ý][A-ZÀ-Ý\.'´` ]{4,}?)\s*[-–]\s*(?:" + RANK + @"|1º TEN|2º TEN|TEN)\b[^a-z]{0,40}$", O);
    static readonly Regex RankOnly = new(@"^(?:" + RANK + @")(?:\s+(?:PM|QPPM|QOPM|QOAPM|QOPMS|QPM|PMRO))?(?:\s*[-–]\s*(?:RE|MAT\.?)\s*[\d\*\.\-]+)?\.?$", O);
    static readonly Regex Func = new(@"^(Comandante|Subcomandante|SUBCOMANDANTE|COMANDANTE|Chefe|Respondendo|Portaria n[ºo°]? ?\d+|Processo|" +
        @"PROCESSO|Requerente|REQUERENTE|Coordenador|COORDENADOR|Presidente|Encarregad|Diretor|Corregedor|" +
        @"Membro|Escrivão|Secretári|Ajudante|Auxiliar|da Coordenadoria|Departamento|Comandante-Geral|" +
        @"Auditor|Cmt|CMT|Resp\.|Interino|em exercício|Assessor|Matr[ií]cula|RE ?\d|Mat\.? ?\d|" +
        @"Subcmt|Sub Cmt|Chefe-Adjunto|da PMRO|Comando|Seção|Companhia Independente)", O);
    static readonly Regex Junk = new(@"^(COMPANHIA INDEPENDENTE DE POLICIAMENTO OSTENSIVO/CRP-IV$|Avenida Porto Velho, nº 800|E-mail: cipo@|" +
        @"Telefone \(69\)|CNPJ \d|GOVERNO DO ESTADO DE RONDÔNIA$|Polícia Militar - PM$|" +
        @"Companhia Independente de Policiamento Ostensivo de Buritis - Seção de Pessoal|" +
        @"BOLETIM INTERNO$|PUBLIQUE-SE:?$|\*+$|\(?SEM ALTERAÇÃO\)?\.?$)", O | RegexOptions.IgnoreCase);
    static readonly Regex SeiJunk = new(@"SEEI|PPor|Referência: Caso responda|^\d{2}/\d{2}/\d{4}, \d{2}:\d{2} SEI/RO|^https?://|" +
        @"código verificador|verificador \d+|código CRC|conforme horário oficial|caput e seus §§|Decreto nº 21\.794|^\d+/\d+$", O);
    static readonly Regex BiHeader = new(@"^BOLETIM INTERNO\s+d[aoe]\s+.{1,40}?N[ºo°]\s*(\d+)", O | RegexOptions.IgnoreCase);

    static readonly string[] Keys =
    {
        "FÉRIAS", "FERIAS", "LICENÇA", "TRÂNSITO", "TRANSITO", "INSTALAÇÃO", "DISPENSA", "PORTARIA", "OFÍCIO",
        "OFICIO", "DESPACHO", "DECISÃO", "PARECER", "REQUERIMENTO", "MEMORANDO", "MOVIMENTO SANITÁRIO", "INSPEÇÃO",
        "AFASTAMENTO", "APRESENTAÇÃO", "ELOGIO", "PUNIÇÃO", "JULGAMENTO", "HOMOLOGAÇÃO", "JUSTIÇA", "DECRETO",
        "AUTORIZAÇÃO", "NOTA", "TERMO", "DECLARAÇÃO", "PAGAMENTO", "INFORMAÇÕES", "ERRATA", "PARTE", "MISSÕES",
        "CURSO", "DOCUMENTOS", "ALTERAÇÃO DE", "DIVERSOS", "GERAIS", "ADMINISTRATIVO", "RETORNO", "COMPORTAMENTO",
        "CERTIDÃO", "ATESTADO", "SINDICÂNCIA", "PROMOÇÃO", "MEDALHA", "CLASSIFICAÇÃO", "MOVIMENTAÇÃO", "ESCALA",
        "RESSARCIMENTO", "PRORROGAÇÃO", "INTERRUPÇÃO", "ADIAMENTO", "SUSPENSÃO", "ATA", "RELATÓRIO", "PUBLICAÇÃO",
        "CONVOCAÇÃO", "DESIGNAÇÃO", "EXCLUSÃO", "INCLUSÃO", "LUTO", "NÚPCIAS", "NUPCIAL", "PATERNIDADE", "MATERNIDADE",
        "LTS", "PORTE", "RECOMPENSA", "AUXÍLIO", "ABONO", "SOLUÇÃO", "CERTIFICADO", "COMUNICAÇÃO", "MANDADO",
    };

    Dictionary<string, int> _freq = new();

    /// <summary>Texto do cabeçalho de seção (normalizado) ou null.</summary>
    public static string? Cabecalho(string t)
    {
        var s = t.Trim().Replace("​", "");
        if (s.Length > 130 || s.Length < 3) return null;
        if (Regex.IsMatch(s, @"^\d[ªa]\s*PARTE")) return s.ToUpperInvariant();
        if (Regex.IsMatch(s, @"^I+\s*[–-]\s*(GERAIS|ADMINISTRATIVO|JUSTIÇA)") || s == "II ADMINISTRATIVO" || s == "ASSUNTOS GERAIS E ADMINISTRATIVOS")
            return "GERAIS";
        Match m = Regex.Match(s, @"^-\s*([^\d]+?)\s*:?\s*$");
        if (!m.Success) m = Regex.Match(s, @"^[a-z][\.\)]\s+(.+?)\s*:?\s*$");
        if (!m.Success) m = Regex.Match(s, @"^[IVX]+\s*[-–]\s*([^\d]+?)\s*:\s*$");
        if (!m.Success) m = Regex.Match(s, @"^\d\.\s*((?:ALTERAÇÃO DE|DIVERSOS)[^\d]*?)\s*:?\s*$");
        if (!m.Success) m = Regex.Match(s, @"^(4\.1\s*-\s*JUSTIÇA(?:/ELOGIO)?)\s*:?\s*$");
        if (!m.Success) m = Regex.Match(s, @"^(RETORNO|INSTALAÇÃO|ELOGIO|LICENÇA ESPECIAL|TRÂNSITO|ELOGIOS)\s*:\s*$");
        if (!m.Success) return null;
        var body = m.Groups[1].Value.Trim(' ', ':', '-', '–').Replace("​", "");
        double lim = Regex.IsMatch(s, @"^[a-z][\.\)]") ? 0.75 : 0.85;
        var up = body.ToUpperInvariant();
        if (Texto.ProporcaoMaiusculas(body) < lim || !Keys.Any(k => up.Contains(k)) || Regex.Matches(body, @"\d").Count > 6)
            return null;
        return Regex.Replace(up, @"\s+", " ");
    }

    /// <summary>A linha i inicia um bloco de assinatura? Devolve o índice da última linha do bloco.</summary>
    int? FimAssinatura(List<Item> its, int i)
    {
        var s = its[i].Texto.Trim();
        var s2 = Regex.Replace(s, @"(?<=\b[A-ZÀ-Ý]) (?=[A-ZÀ-Ý]\b)", ""); // "A N A" -> "ANA"
        bool ok = false;
        if (s.Length < 95 && (Sig1.IsMatch(s) || Sig1.IsMatch(s2)) && !Regex.IsMatch(s, @"\d{4}")) ok = true;
        else if (s.Length < 60 && Regex.IsMatch(s, @"^[A-ZÀ-Ý][A-ZÀ-Ý\.' ]{6,}$") && s.Split(' ').Length >= 2 && i + 1 < its.Count
                 && !its[i + 1].EhTabela && RankOnly.IsMatch(its[i + 1].Texto.Trim()))
            ok = true;
        if (!ok) return null;
        int j = i;
        while (j + 1 < its.Count && !its[j + 1].EhTabela)
        {
            var n = its[j + 1].Texto.Trim();
            if (n.StartsWith("Portaria n") && _freq.GetValueOrDefault(n) < 3) break; // título de nova portaria, não designação
            if (n.Length < 120 && (Func.IsMatch(n) || RankOnly.IsMatch(n) || Regex.IsMatch(n, @"^(PMRO|CIPO|CRP)")) && Cabecalho(n) == null) j++;
            else break;
        }
        return j;
    }

    static List<Item> Limpar(IEnumerable<Item> its)
    {
        var res = new List<Item>();
        string? pularAte = null;
        int restam = 0;
        foreach (var it in its)
        {
            if (it.EhTabela) { if (pularAte == null) res.Add(it); continue; }
            var t = it.Texto.Trim();
            if (pularAte != null)
            {
                restam--;
                if (Regex.IsMatch(t, pularAte) || restam <= 0) pularAte = null;
                continue;
            }
            if (t.StartsWith("Documento assinado eletronicamente"))
            {
                if (!t.Contains("2017")) { pularAte = "2017"; restam = 4; }
                continue;
            }
            if (t.StartsWith("A autenticidade deste documento"))
            {
                if (!t.Contains("CRC")) { pularAte = "CRC"; restam = 3; }
                continue;
            }
            if (Junk.IsMatch(t) || SeiJunk.IsMatch(t)) continue;
            res.Add(it);
        }
        return res;
    }

    static DateTime DataDoCabecalho(string texto, DateTime padrao)
    {
        var m = Regex.Match(texto, @"(\d{1,2})\s+(?:de|DE|De)\s+([A-Za-zÇçÃã]+)");
        var a = Regex.Match(texto, @"\b(20\d{2})\b");
        if (m.Success && a.Success)
        {
            int mes = Texto.MesPorNome(m.Groups[2].Value);
            if (mes > 0)
                try { return new DateTime(int.Parse(a.Groups[1].Value), mes, int.Parse(m.Groups[1].Value)); } catch { }
        }
        return padrao;
    }

    public class Bi
    {
        public int Numero;
        public DateTime Data;
        public string SeiId = "";
        public List<Item> Itens = new();
    }

    // cabeçalho tolerante: sem espaços (letras espaçadas pelo PDF), em maiúsculas, com ou sem "da CIPO"
    static readonly Regex BiHeaderCompacto = new(@"^BOLETIMINTERNO(?:D[AOE])?[A-Z/\-0-9]{0,30}?N[ºO°]?(\d{1,3})(?!\d)", O);

    /// <summary>Número do BI se a linha for o cabeçalho "BOLETIM INTERNO ... Nº xx", senão null.</summary>
    public static int? NumeroDoCabecalho(string texto)
    {
        var t = texto.Trim();
        if (t.Length == 0 || t.Length > 140) return null;
        var m = BiHeader.Match(t);
        if (m.Success) return int.Parse(m.Groups[1].Value);
        // só vale se a linha estiver (quase toda) em maiúsculas: evita "Boletim Interno nº 5" citado dentro de um texto
        if (Texto.ProporcaoMaiusculas(t) < 0.8) return null;
        var sq = Regex.Replace(Texto.SemAcento(t).ToUpperInvariant(), @"[\s\.]+", "");
        var m2 = BiHeaderCompacto.Match(sq);
        return m2.Success ? int.Parse(m2.Groups[1].Value) : null;
    }

    static string TextoDoItem(Item it) => it.EhTabela ? string.Join(" ", it.Celulas) : it.Texto;

    /// <summary>Separa os BIs contidos no PDF (um ou vários).</summary>
    public List<Bi> SepararBis(List<Item> itens, Dictionary<int, string> idsSei)
    {
        var bis = new List<Bi>();
        Bi? cur = null;
        for (int k = 0; k < itens.Count; k++)
        {
            var it = itens[k];
            // o título pode estar dentro de um quadro/tabela (BI avulso): olha cada linha de cada célula
            var candidatos = it.EhTabela
                ? it.Celulas.SelectMany(c => c.Split('\n')).ToList()
                : new List<string> { it.Texto };
            int? n = null;
            foreach (var cand in candidatos) { n = NumeroDoCabecalho(cand); if (n != null) break; }
            if (n != null)
            {
                var ctx = TextoDoItem(it) + " " + string.Join(" ", itens.Skip(k + 1).Take(4).Select(TextoDoItem));
                cur = new Bi { Numero = n.Value, Data = DataDoCabecalho(ctx, DateTime.Today), SeiId = idsSei.GetValueOrDefault(n.Value, "") };
                bis.Add(cur);
                continue;
            }
            cur?.Itens.Add(it);
        }
        // PDF de um único BI sem o cabeçalho reconhecível: usa o número do rodapé "Boletim Interno Nº xx (id)"
        if (bis.Count == 0 && idsSei.Count == 1)
        {
            var (num, id) = idsSei.First();
            var ctx = string.Join(" ", itens.Take(40).Select(TextoDoItem));
            var bi = new Bi { Numero = num, Data = DataDoCabecalho(ctx, DateTime.Today), SeiId = id };
            bi.Itens.AddRange(itens);
            bis.Add(bi);
        }
        return bis;
    }

    /// <summary>Divide um BI em atos (pula a 1ª parte – escala de serviço).</summary>
    public List<Ato> Segmentar(Bi bi, List<Item> todosItens)
    {
        _freq = todosItens.Where(i => !i.EhTabela).GroupBy(i => i.Texto.Trim()).ToDictionary(g => g.Key, g => g.Count());
        var its = bi.Itens;
        int k0 = its.FindIndex(i => !i.EhTabela && Regex.IsMatch(i.Texto.Trim(), @"^2[ªa]\s*PARTE"));
        its = Limpar(its.Skip(Math.Max(0, k0)));
        var atos = new List<Ato>();
        string secao = "GERAIS";
        Ato? ato = null;
        Ato Novo()
        {
            var a = new Ato { BiNumero = bi.Numero, BiData = bi.Data, BiSeiId = bi.SeiId, Secao = secao };
            atos.Add(a);
            return a;
        }
        int k = 0;
        while (k < its.Count)
        {
            var it = its[k];
            if (!it.EhTabela)
            {
                var h = Cabecalho(it.Texto);
                if (h != null)
                {
                    secao = h;
                    ato = null;
                    k++;
                    // cabeçalho quebrado em duas linhas
                    while (k < its.Count && !its[k].EhTabela)
                    {
                        var n2 = its[k].Texto.Trim();
                        bool cont = n2.Length < 70 && Texto.ProporcaoMaiusculas(n2) > 0.9 && !Regex.IsMatch(n2, @"\d{3}")
                                    && Cabecalho(n2) == null && FimAssinatura(its, k) == null && !Regex.IsMatch(n2, "^(O |A |OS |AS )")
                                    && n2.Split(' ').Length <= 8 && !EntryStart.IsMatch(n2);
                        cont |= n2.Length < 40 && !Regex.IsMatch(n2, @"\d") && k + 1 < its.Count && !its[k + 1].EhTabela
                                && its[k + 1].Texto.Trim().StartsWith("Portaria n");
                        if (!cont) break;
                        secao += " " + n2.ToUpperInvariant();
                        k++;
                    }
                    continue;
                }
                var j = FimAssinatura(its, k);
                if (j != null)
                {
                    ato ??= Novo();
                    for (int q = k; q <= j; q++)
                    {
                        its[q].Assinatura = true;
                        ato.Itens.Add(its[q]);
                        ato.Assinatura.Add(its[q].Texto);
                    }
                    ato = null;
                    k = j.Value + 1;
                    continue;
                }
            }
            ato ??= Novo();
            ato.Itens.Add(it);
            k++;
        }
        return atos.Where(a => a.Itens.Any(x => !x.Assinatura)).ToList();
    }
}
