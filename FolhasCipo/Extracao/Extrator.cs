using System.Text.Json;
using System.Text.RegularExpressions;

namespace FolhasCipo.Extracao;

/// <summary>Resultado da extração de um BI.</summary>
public class ResultadoBi
{
    public int Numero { get; set; }
    public DateTime Data { get; set; }
    public string SeiId { get; set; } = "";
    public List<Publicacao> Publicacoes { get; } = new();
}

/// <summary>
/// Transforma os atos de um BI em publicações por policial, recortando o trecho de cada um
/// (mesma lógica validada nos modelos de folha de alteração).
/// </summary>
public class Extrator
{
    const RegexOptions O = RegexOptions.Compiled;
    static readonly Regex Art = new(@"^\s*(Art\.?|Artigo|ART\.?|Art)\s*\d", O | RegexOptions.IgnoreCase);
    static readonly Regex DivArtigo = new(@"(?<=[\.\);:\]])\s+(?=Art(?:igo)?\.?\s*\d+\s*[ºo°]?(?!\w))", O);
    static readonly Regex HeaderWords = new(@"\b(NOME|Nome Completo|Grad|GRAD|Graduação|Posto/Graduação|Matrícula|MATRÍCULA)\b", O);
    static readonly Regex InlineSig = new(@"(?:No Impedimento\s+)?\b[A-ZÀ-Ý][A-ZÀ-Ý'´ ]{5,}?\s*[-–]\s*(?:\d[ºª°]\s*TEN|TEN\s?CEL|TEN|CAP|MAJ|CEL|CORONEL|SUB\s?TEN|ST)\b\s*(?:QOPM|QOAPM|QOPMS|PM|QOA PM)?", O);
    static readonly Regex Starter = new(@"^(\(\d{8}\)\s*(?:Fica concedido|Concedo|Elogio|É por dever|Louvo|Na ocasião|Informo)|Fica concedido|Concedo elogio|Concedo o presente elogio|Elogio (o|a|os|as|aos|ao)\b|Elogiar|É por dever de justiça|" +
        @"Louvo|Portaria n[ºo°]\s*\d+\s+de|PORTARIA N[ºo°]\s*\d+\s+DE|Of[íi]cio n[ºo°]|Despacho n[ºo°]|Decis[ãa]o n[ºo°]|" +
        @"Parecer n[ºo°]|Memorando n[ºo°]|Apresentou-se|CERTIFICO|ATA DE INSPE[ÇC][ÃA]O|SOLU[ÇC][ÃA]O DE ATESTADO|" +
        @"(?:Do|De)\s*:|OBJETO\s*:|OBJETIVO\s*:|ERRATA|Nota n[ºo°]|Termo n[ºo°]|TERMO DE (?:DEVOLU|ENTREGA|RESPONSABILIDADE|CAUTELA)|Comportamento e Pontua|MENÇÃO ELOGIOSA|REQUERIMENTO\b|" +
        @"(?:\(\d{8}\)\s*)?O COMANDANTE[^.]{0,200}?consigna[r]?\s+elogio)", O);
    static readonly Regex DocStart = new(@"^(\(\d{8}\)|MENÇÃO ELOGIOSA|REQUERIMENTO|De\s*:|O COMANDANTE[^.]{0,200}?consigna|Fica concedido|Concedo|Elogi|É por dever|Louvo|Portaria|PORTARIA|Of[íi]cio|Despacho|Decis|Parecer|Memorando|" +
        @"CERTIFICO|ATA DE|SOLU|Do\s*:|OBJET|ERRATA|Nota n|Termo n|TERMO DE)", O);
    static readonly Regex FimConector = new(@"(,|\b(de|do|da|dos|das|o|a|os|as|ao|aos|pelo|pela|e|ou|que|com|militar|militares|policiais|policial|sendo|ambos))\s*$", O | RegexOptions.IgnoreCase);
    static readonly Regex Fechamento = new(@"^(Buritis|Quartel|Porto Velho|Publique|Cumpra|Registre|\(\.\.\.\)|Art|Par[áa]grafo|[IVX]+\s*[–-])", O);

    readonly Identificador _id;
    public Extrator(Identificador id) { _id = id; }

    class Unidade
    {
        public string Tipo = "par"; // par | entry | table
        public List<string> Linhas = new();
        public bool Assin;
        public List<string>? Cab;
        public List<List<string>> Rows = new();
        public List<HashSet<int>> Quem = new();
        public HashSet<int> Todos = new();
        public string Txt = "";
        public bool ArtigoDividido;
        public bool TemPessoas => Tipo == "entry" || (Tipo == "table" && Rows.Count > 0)
                                  || (Tipo == "par" && !Assin && ArtigoDividido && Todos.Count > 0);
    }

    record Peca(string Tipo, string Texto = "", List<string>? Cab = null, List<List<string>>? Rows = null); // t | o | tab

    public static string JuntarLinhas(IEnumerable<string> linhas)
    {
        var o = "";
        foreach (var l0 in linhas)
        {
            var l = l0.Trim();
            if (l.Length == 0) continue;
            if (o.Length == 0) o = l;
            else if (o.EndsWith("-") && !o.EndsWith(" -")) o += l;
            else o += " " + l;
        }
        return o;
    }

    static string TiraAssinaturaEmbutida(string t) => InlineSig.Replace(t, " ");

    List<Unidade> MontarUnidades(List<Item> itens)
    {
        var gaps = new List<double>();
        for (int i = 1; i < itens.Count; i++)
        {
            var a = itens[i - 1]; var b = itens[i];
            double g = b.Y - a.Y;
            if (!a.EhTabela && !b.EhTabela && a.Pagina == b.Pagina && g > 5 && g < 30) gaps.Add(g);
        }
        gaps.Sort();
        double lg = gaps.Count > 0 ? gaps[gaps.Count / 2] : 13.8;

        var us = new List<Unidade>();
        Unidade? cur = null;
        Item? prev = null;
        foreach (var it in itens)
        {
            if (it.EhTabela)
            {
                var cells = it.Celulas.Select(c => Texto.Compacta(c.Replace('\n', ' '))).ToList();
                var junto = string.Join(" ", cells);
                bool head = HeaderWords.IsMatch(junto) && _id.Mencoes(junto, false).Count == 0;
                if (cur == null || cur.Tipo != "table" || (head && cur.Rows.Count > 0))
                {
                    cur = new Unidade { Tipo = "table" };
                    us.Add(cur);
                }
                if (head) cur.Cab = cells; else cur.Rows.Add(cells);
                prev = it;
                continue;
            }
            var t = it.Texto.Trim();
            bool sig = it.Assinatura;
            bool novoPar = cur == null || cur.Tipo == "table" || prev == null || prev.EhTabela
                           || (prev.Pagina == it.Pagina && it.Y - prev.Y > lg * 1.3)
                           || Art.IsMatch(t) || sig != cur.Assin;
            var prevTxt = cur != null && cur.Tipo != "table" && cur.Linhas.Count > 0 ? cur.Linhas[^1] : "";
            bool entry = !sig && Segmentador.EntryStart.IsMatch(t) && Segmentador.RankRe.Matches(t).Count <= 1 && t.Length < 140
                         && !FimConector.IsMatch(prevTxt);
            if (entry)
            {
                cur = new Unidade { Tipo = "entry", Linhas = { t } };
                us.Add(cur);
            }
            else if (cur != null && cur.Tipo == "entry" && t.Length < 80 && !sig && !Art.IsMatch(t) && !Segmentador.EntryStart.IsMatch(t)
                     && !Starter.IsMatch(t) && !Fechamento.IsMatch(t)
                     && (!Regex.IsMatch(cur.Linhas[^1], @"[;\.]\s*$") || Regex.IsMatch(cur.Linhas[^1], @"Comportamento;\s*$|Pontua[çc][ãa]o \d+;\s*$")))
                cur.Linhas.Add(t);
            else if (cur != null && cur.Tipo == "entry" && t.Length < 45 && Regex.IsMatch(t, @"^[A-ZÀ-Ý \.;']+$")
                     && !Regex.IsMatch(cur.Linhas[^1], @"[;\.]\s*$"))
                cur.Linhas.Add(t);
            else if (novoPar || cur == null || cur.Tipo != "par")
            {
                cur = new Unidade { Tipo = "par", Linhas = { t }, Assin = sig };
                us.Add(cur);
            }
            else cur.Linhas.Add(t);
            prev = it;
        }

        // parágrafos com várias publicações (homologação de atestados, documentos colados com id SEI)
        var div = new List<Unidade>();
        foreach (var u in us)
        {
            if (u.Tipo == "par" && !u.Assin)
            {
                var txt = JuntarLinhas(u.Linhas);
                var partes = Regex.Split(txt, @"(?=Publicação de Homologação de Atestado)|(?<=[\.\)]) ?(?=\(\d{8}\)\s*(?:Fica concedido|Concedo|Elogio|É por dever|Louvo|Na ocasião|Informo))")
                                  .Where(p => p.Trim().Length > 0).ToList();
                if (partes.Count > 1)
                {
                    foreach (var p in partes) div.Add(new Unidade { Tipo = "par", Linhas = { p.Trim() } });
                    continue;
                }
            }
            div.Add(u);
        }
        // parágrafo corrido com vários artigos ("Artigo 1º - ... Artigo 2º - ..."): uma unidade por artigo
        var porArtigo = new List<Unidade>();
        foreach (var u in div)
        {
            if (u.Tipo == "par" && !u.Assin)
            {
                var partes = DivArtigo.Split(JuntarLinhas(u.Linhas)).Where(p => p.Trim().Length > 0).ToList();
                if (partes.Count >= 3 && partes.Count(p => Art.IsMatch(p)) >= 2)
                {
                    foreach (var p in partes) porArtigo.Add(new Unidade { Tipo = "par", Linhas = { p.Trim() } });
                    continue;
                }
            }
            porArtigo.Add(u);
        }
        div = porArtigo;
        foreach (var u in div)
        {
            if (u.Tipo == "table")
            {
                u.Quem = u.Rows.Select(r => _id.Mencoes(string.Join(" ", r))).ToList();
                u.Todos = u.Quem.Count > 0 ? u.Quem.SelectMany(x => x).ToHashSet() : new();
            }
            else
            {
                u.Txt = JuntarLinhas(u.Linhas);
                u.Todos = u.Assin ? new() : _id.Mencoes(TiraAssinaturaEmbutida(u.Txt));
                u.ArtigoDividido = u.Tipo == "par" && !u.Assin && Art.IsMatch(u.Txt);
            }
        }
        return div;
    }

    List<List<Unidade>> SepararDocumentos(List<Unidade> us)
    {
        var grupos = new List<List<Unidade>>();
        var cur = new List<Unidade>();
        foreach (var u in us)
        {
            bool inicia = u.Tipo == "par" && !u.Assin && Starter.IsMatch(u.Txt);
            if (inicia && cur.Any(x => x.Tipo != "par" || x.Assin || x.Todos.Count > 0 || x.Txt.Length > 250))
            {
                var cauda = new List<Unidade>();
                while (cur.Count > 0 && cur[^1].Tipo == "par" && cur[^1].Todos.Count == 0 && !cur[^1].Assin
                       && cur[^1].Txt.Length < 160 && !Regex.IsMatch(cur[^1].Txt, @"[\.;]\s*$"))
                {
                    cauda.Insert(0, cur[^1]);
                    cur.RemoveAt(cur.Count - 1);
                }
                if (cur.Count > 0) grupos.Add(cur);
                cur = cauda;
            }
            cur.Add(u);
            if (u.Tipo == "par" && !u.Assin && Regex.IsMatch(u.Txt, @"\((COLETIVO|INDIVIDUAL)\)")
                && InlineSig.IsMatch(u.Txt.Length > 120 ? u.Txt[^120..] : u.Txt))
            {
                grupos.Add(cur);
                cur = new List<Unidade>();
            }
        }
        if (cur.Count > 0) grupos.Add(cur);
        return grupos;
    }

    static List<List<int>> Artigos(List<Unidade> us)
    {
        var blocos = new List<List<int>>();
        var cur = new List<int>();
        for (int k = 0; k < us.Count; k++)
        {
            if (us[k].Tipo == "par" && Art.IsMatch(us[k].Txt) && cur.Count > 0) { blocos.Add(cur); cur = new(); }
            cur.Add(k);
        }
        if (cur.Count > 0) blocos.Add(cur);
        return blocos;
    }

    List<Peca> Trecho(List<Unidade> us, int P, bool documento)
    {
        var manter = new bool[us.Count];
        var linhasTab = new Dictionary<int, List<List<string>>>();
        if (documento)
        {
            var blocos = Artigos(us);
            var artIdx = Enumerable.Range(0, blocos.Count)
                .Where(i => us[blocos[i][0]].Tipo == "par" && Art.IsMatch(us[blocos[i][0]].Txt)).ToList();
            bool algumP = blocos.Any(b => b.Any(k => us[k].Todos.Contains(P)));
            var protegidos = artIdx.TakeLast(2).ToHashSet(); // os dois últimos artigos (publicação / vigência) ficam
            for (int bi = 0; bi < blocos.Count; bi++)
            {
                var blk = blocos[bi];
                bool temPessoas = blk.Any(k => us[k].TemPessoas);
                bool pDentro = blk.Any(k => us[k].Todos.Contains(P));
                bool ehArt = us[blk[0]].Tipo == "par" && Art.IsMatch(us[blk[0]].Txt);
                if (ehArt && temPessoas && !pDentro && blocos.Count > 2) continue; // artigo só com outros militares
                if (ehArt && !temPessoas && !pDentro && algumP && artIdx.Count >= 5
                    && !protegidos.Contains(bi) && !blk.Any(k => us[k].Assin)) continue; // artigo normativo genérico de ato longo
                foreach (var k in blk)
                {
                    var u = us[k];
                    if (u.Tipo == "entry" && !u.Todos.Contains(P)) continue;
                    if (u.Tipo == "table")
                    {
                        var rk = u.Rows.Where((r, i) => u.Quem[i].Contains(P)).ToList();
                        if (u.Rows.Count > 0 && rk.Count == 0 && u.Todos.Count > 0) continue;
                        linhasTab[k] = u.Todos.Count > 0 ? rk : u.Rows;
                    }
                    manter[k] = true;
                }
            }
        }
        else
        {
            for (int k = 0; k < us.Count; k++)
            {
                if (!us[k].Todos.Contains(P)) continue;
                manter[k] = true;
                if (us[k].Tipo == "table")
                    linhasTab[k] = us[k].Rows.Where((r, i) => us[k].Quem[i].Contains(P)).ToList();
                int j = k - 1;
                while (j >= 0 && (us[j].Tipo == "table" || us[j].Tipo == "entry") && !us[j].Todos.Contains(P)) j--;
                if (j >= 0 && us[j].Tipo == "par" && us[j].Todos.Count == 0 && us[j].Txt.Length < 220) manter[j] = true;
            }
        }
        var pecas = new List<Peca>();
        int ultimo = -1;
        for (int k = 0; k < us.Count; k++)
        {
            if (!manter[k]) continue;
            if (pecas.Count > 0 && k != ultimo + 1) pecas.Add(new Peca("o"));
            var u = us[k];
            pecas.Add(u.Tipo == "table" ? new Peca("tab", Cab: u.Cab, Rows: linhasTab.GetValueOrDefault(k, new List<List<string>>())) : new Peca("t", u.Txt));
            ultimo = k;
        }
        if (documento && ultimo < us.Count - 1 && manter.Any(x => x)) pecas.Add(new Peca("o"));
        return pecas;
    }

    // ---------------- rótulos ----------------

    static readonly (string pat, string rot)[] RotSecao =
    {
        (@"F[ÉE]RIAS\s*[–-]\s*SEM EFEITO", "FÉRIAS – SEM EFEITO"),
        (@"F[ÉE]RIAS|^RETORNO|INFORMATIVO DE F", "FÉRIAS"),
        (@"DISPENSA (COMO|POR)? ?RECOMPENSA|DISPENSA RECOMPENSA", "DISPENSA COMO RECOMPENSA"),
        (@"ELOGIO", "ELOGIO"),
        (@"LICEN[ÇC]A ESPECIAL", "LICENÇA ESPECIAL"),
        (@"PATERNIDADE", "LICENÇA PATERNIDADE"),
        (@"NUPCIAL|N[ÚU]PCIAS", "LICENÇA NUPCIAL"),
        (@"TR[ÂA]NSITO|INSTALA[ÇC][ÃA]O", "TRÂNSITO / INSTALAÇÃO"),
        (@"^PORTARIA", "PORTARIA"),
        (@"^OF[ÍI]CIO", "OFÍCIO"),
        (@"^REQUERIMENTO", "REQUERIMENTO"),
        (@"^PARECER", "PARECER"),
        (@"MOVIMENTO SANIT|INSPE[ÇC][ÃA]O DE SA[ÚU]DE|ATA M[ÉE]DICA", "MOVIMENTO SANITÁRIO"),
        (@"NOTA DE PUNI", "NOTA DE PUNIÇÃO"),
        (@"ERRATA", "ERRATA"),
        (@"^DESPACHO", "DESPACHO"),
        (@"^DECIS", "DECISÃO"),
        (@"^MEMORANDO", "MEMORANDO"),
        (@"^AFASTAMENTO", "AFASTAMENTO"),
        (@"^CERTID", "CERTIDÃO"),
        (@"^CURSO", "CURSOS"),
        (@"ATESTADO DE ORIGEM", "ATESTADO DE ORIGEM"),
    };
    static readonly (string pat, string rot)[] RotDocumento =
    {
        (@"^(Concedo|Elogi|É por dever|Louvo|Fica concedido|\(\d{8}\)\s*(Fica|Concedo|Elogio))", "ELOGIO"),
        (@"SOLU[ÇC][ÃA]O DE ATESTADO DE ORIGEM", "SOLUÇÃO DE ATESTADO DE ORIGEM"),
        (@"^(Portaria|PORTARIA) N", "PORTARIA"),
        (@"^(Portaria|PORTARIA) n", "PORTARIA"),
        (@"^Of[íi]cio n", "OFÍCIO"),
        (@"^Despacho", "DESPACHO"),
        (@"^Decis[ãa]o", "DECISÃO"),
        (@"^Parecer", "PARECER"),
        (@"^Memorando", "MEMORANDO"),
        (@"^ATA DE INSPE|^INSPE[ÇC][ÃA]O DE SA", "MOVIMENTO SANITÁRIO"),
        (@"^HOMOLOGA[ÇC][ÃA]O D[EA] (SINDIC|FEITO)", "SINDICÂNCIA"),
        (@"^HOMOLOGA", "HOMOLOGAÇÃO"),
        (@"^CERTIFICO", "APRESENTAÇÃO"),
        (@"^(Do\s*:|De\s*:|OBJETO|OBJETIVO|REQUERIMENTO)", "REQUERIMENTO"),
        (@"^(MENÇÃO ELOGIOSA|O COMANDANTE[^.]{0,200}?consigna)", "ELOGIO"),
        (@"^(?:\(\d{8}\)\s*)?TERMO DE", "INFORMAÇÃO"),
        (@"^ERRATA", "ERRATA"),
    };
    static readonly (string pat, string rot)[] RotApresentacao =
    {
        (@"[Dd]ispensa (como|por) [Rr]ecompensa", "DISPENSA COMO RECOMPENSA"),
        (@"[Ll]icen[çc]a [Ee]special", "LICENÇA ESPECIAL"),
        (@"[Ll]icen[çc]a [Pp]aternidade", "LICENÇA PATERNIDADE"),
        (@"[Nn][úu]pcias|[Nn]upcial", "LICENÇA NUPCIAL"),
        (@"[Tt]r[âa]nsito|[Ii]nstala[çc][ãa]o", "TRÂNSITO / INSTALAÇÃO"),
        (@"[Ff][ée]rias", "FÉRIAS"),
        (@"LTS|[Tt]ratamento de [Ss]a[úu]de|[Ll]uto", "AFASTAMENTO"),
    };
    static readonly (string pat, string rot)[] RotReserva =
    {
        (@"(?i)sindic[âa]ncia|feito preliminar", "SINDICÂNCIA"),
        (@"(?i)\belogio\b|louvo", "ELOGIO"),
        (@"(?i)^portaria n|dispõe sobre (a )?designa", "PORTARIA"),
        (@"^Informa[çc][ãa]o n", "INFORMAÇÃO"),
        (@"^Parte n", "PARTE"),
        (@"(?i)^autoriza[çc][ãa]o", "AUTORIZAÇÃO"),
        (@"(?i)condecora[çc]", "CONDECORAÇÃO"),
        (@"(?i)requerimento|atendendo ao pedido", "DECISÃO"),
        (@"^Do\s+.{0,80}Assunto", "OFÍCIO"),
    };
    static readonly Regex Generico = new(@"^(GERAIS|ADMINISTRATIVO|ALTERAÇÃO DE|DIVERSOS|APRESENTAÇÃO|\d[ªa] PARTE|4\.1 - JUSTIÇA|JUSTIÇA|" +
        @"DOCUMENTOS|ASSUNTOS|HOMOLOGAÇÃO|DECRETO|INFORMAÇÕES|PARTE|TERMOS|DECLARAÇÃO|AUTORIZAÇÃO|PAGAMENTOS|NOTA|AFASTAMENTO|MOVIMENTO SANIT)", O);

    static string Rotulo(string secao, List<Peca> pecas)
    {
        var textos = pecas.Where(p => p.Tipo == "t").Select(p => p.Texto).ToList();
        var texto = string.Join(" ", textos);
        if (Regex.IsMatch(texto, @"Comportamento\s*;\s*\w")) return "COMPORTAMENTO";
        var primeiro = textos.FirstOrDefault(t => t.StartsWith("Apresentou-se"))
                       ?? textos.FirstOrDefault(t => t.Length > 25 && !t.StartsWith("Processo")) ?? textos.FirstOrDefault() ?? "";
        if (Regex.IsMatch(texto.Length > 400 ? texto[..400] : texto, @"SOLU[ÇC][ÃA]O DE ATESTADO DE ORIGEM")) return "SOLUÇÃO DE ATESTADO DE ORIGEM";
        if (primeiro.StartsWith("Apresentou-se") || (Generico.IsMatch(secao) && texto.Contains("Apresentou-se")))
        {
            foreach (var (pat, rot) in RotApresentacao)
                if (Regex.IsMatch(primeiro.Length > 0 ? primeiro : texto, pat)) return rot;
            return "APRESENTAÇÃO";
        }
        bool forte = Regex.IsMatch(secao, @"F[ÉE]RIAS|ELOGIO|DISPENSA|LICEN|TR[ÂA]NSITO|INSTALA") && !secao.StartsWith("PORTARIA");
        if (!forte)
            foreach (var t in textos.Take(3))
                foreach (var (pat, rot) in RotDocumento)
                    if (Regex.IsMatch(t.Length > 200 ? t[..200] : t, pat)) return rot;
        foreach (var (pat, rot) in RotSecao)
            if (Regex.IsMatch(secao, pat)) return rot;
        if (!Generico.IsMatch(secao)) return Regex.Replace(secao, @"^(\d[ªa] PARTE\s*[–-]\s*)", "");
        var r = secao switch
        {
            "HOMOLOGAÇÃO" => "HOMOLOGAÇÃO", "DECRETO" => "DECRETO", "AUTORIZAÇÃO" => "AUTORIZAÇÃO",
            "DECLARAÇÃO" => "DECLARAÇÃO", "TERMOS" => "TERMO", "NOTA" => "NOTA", _ => "PUBLICAÇÃO",
        };
        if (r == "PUBLICAÇÃO")
        {
            var cab = texto.Length > 500 ? texto[..500] : texto;
            foreach (var (pat, rot) in RotReserva)
                if (Regex.IsMatch(cab, pat)) return rot;
        }
        return r;
    }

    // ---------------- montagem dos blocos ----------------

    static readonly Regex EntradaPosto = new(@"(?=\b(?:CB|SD|ASP|ST|SUB ?TEN|CAP|MAJ|TEN ?CEL|CEL|[123]º SGT|[12]º TEN)\s+(?:QPPM|QOPM|QOAPM|QPPMS|QOPMS|QOA ?PM|QPM|PM)\b)", O);
    static readonly Regex ListaElogio = new(@"^(Elogio\s+(?:os|aos|as|às)\s+(?:Policiais\s+Militares|PPMM|militares)\s*)(.*?)(\s+(?:pelo|pela|pelos|pelas|por|em raz[ãa]o|devido|em virtude|que)\s.*)$", O | RegexOptions.Singleline);

    /// <summary>Elogio coletivo: mantém só a entrada do próprio PM na lista de nomes e abrevia as demais com (...).</summary>
    static string ReduzListaElogio(string txt, string nomeNorm)
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
    static string LimpaSobreposicao(string txt)
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


    static List<Bloco> ParaBlocos(List<Peca> pecas, string nomeNorm = "")
    {
        var blocos = new List<Bloco>();
        var buf = new List<string>();
        void Flush()
        {
            if (buf.Count == 0) return;
            var t = ReduzListaElogio(LimpaSobreposicao(Texto.Limpa(Texto.Compacta(string.Join(" ", buf)))), nomeNorm);
            if (t.Length > 0 && t != "(...)") blocos.Add(Bloco.Paragrafo(t));
            buf.Clear();
        }
        foreach (var p in pecas)
        {
            if (p.Tipo == "t") buf.Add(p.Texto);
            else if (p.Tipo == "o") { if (buf.Count == 0 || buf[^1] != "(...)") buf.Add("(...)"); }
            else if (p.Tipo == "tab" && p.Rows != null && p.Rows.Count > 0)
            {
                Flush();
                var cab = p.Cab?.Where(c => c.Trim().Length > 0).Select(Texto.Limpa).ToList();
                var rows = p.Rows.Select(r => r.Where(c => c.Trim().Length > 0).Select(Texto.Limpa).ToList()).ToList();
                if (cab != null && rows.Any(r => r.Count != cab.Count))
                {
                    // células mescladas: mantém as colunas originais sem as totalmente vazias
                    int n = Math.Max(p.Cab!.Count, p.Rows.Max(r => r.Count));
                    var cols = Enumerable.Range(0, n).Where(i => p.Rows.Append(p.Cab!).Any(r => i < r.Count && r[i].Trim().Length > 0)).ToList();
                    cab = cols.Select(i => i < p.Cab!.Count ? p.Cab[i] : "").ToList();
                    rows = p.Rows.Select(r => cols.Select(i => i < r.Count ? r[i] : "").ToList()).ToList();
                }
                blocos.Add(Bloco.Tabela(cab, rows));
            }
        }
        Flush();
        return blocos;
    }

    // ---------------- ponto de entrada ----------------

    public ResultadoBi Processar(Segmentador.Bi bi, List<Ato> atos)
    {
        var res = new ResultadoBi { Numero = bi.Numero, Data = bi.Data, SeiId = bi.SeiId };
        // ato só com tabela sem cabeçalho e texto curto pertence ao ato anterior
        var juntos = new List<(Ato ato, List<Unidade> us)>();
        foreach (var a in atos)
        {
            var us = MontarUnidades(a.Itens);
            var txt = us.Where(u => u.Tipo != "table").ToList();
            if (juntos.Count > 0 && us.Any(u => u.Tipo == "table" && u.Cab == null) && txt.All(u => u.Txt.Length < 60))
            {
                juntos[^1].us.AddRange(us);
                continue;
            }
            juntos.Add((a, us));
        }
        int ordem = 0;
        var vistos = new HashSet<string>();
        foreach (var (ato, todas) in juntos)
        {
            // fragmento (lista de nomes/assinatura que sobrou de um elogio) volta para o documento anterior
            var docs = new List<List<Unidade>>();
            foreach (var g in SepararDocumentos(todas))
            {
                bool fragmento = docs.Count > 0 && (g[0].Tipo == "entry" || g[0].Assin) && g.All(u => u.Tipo != "table")
                                 && !g.Any(u => u.Tipo == "par" && !u.Assin && (u.Txt.Length > 200 || Starter.IsMatch(u.Txt)));
                if (fragmento) docs[^1].AddRange(g); else docs.Add(g);
            }
            foreach (var us in docs)
            {
                ordem++;
                var envolvidos = us.SelectMany(u => u.Todos).ToHashSet();
                // citados só por posto + nome de guerra
                foreach (var u in us.Where(u => u.Tipo != "table" && !u.Assin))
                    foreach (var p in _id.NomesDeGuerra(TiraAssinaturaEmbutida(u.Txt), envolvidos).ToList())
                        if (u.Todos.Add(p)) envolvidos.Add(p);
                var primeiro = us.FirstOrDefault(u => u.Tipo == "par");
                bool documento = us.Any(u => u.Assin) ||
                                 (primeiro != null && DocStart.IsMatch(primeiro.Txt) && !primeiro.Txt.StartsWith("Comportamento"));
                foreach (var P in envolvidos)
                {
                    var pecas = Trecho(us, P, documento);
                    if (!pecas.Any(p => p.Tipo is "t" or "tab")) continue;
                    var blocos = ParaBlocos(pecas, _id.NomeNorm(P));
                    if (blocos.Count == 0) continue;
                    var pub = new Publicacao { PolicialId = P, Rotulo = Rotulo(ato.Secao, pecas), Blocos = blocos, Ordem = ordem };
                    var chave = P + "|" + JsonSerializer.Serialize(blocos);
                    if (!vistos.Add(chave)) continue; // repetição exata no mesmo BI
                    // mesma portaria publicada duas vezes no BI (texto do BI + cópia do Diário Oficial)
                    var inicio = string.Join(" ", blocos.Where(b => b.Tipo == "p").Select(b => b.Texto));
                    var mp = Regex.Match(inicio.Length > 400 ? inicio[..400] : inicio, @"Portaria\s*n[ºo°]?\s*(\d{3,5})\s+de\s+\d", RegexOptions.IgnoreCase);
                    if (mp.Success && !vistos.Add(P + "|PORTARIA|" + mp.Groups[1].Value)) continue;
                    res.Publicacoes.Add(pub);
                }
            }
        }
        return res;
    }
}
