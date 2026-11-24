using System.Text.RegularExpressions;

namespace FolhasCipo.Extracao;

/// <summary>Define o rótulo (título da seção) de cada publicação na folha, igual ao usado pela P1.</summary>
static class Rotulador
{
    const RegexOptions O = RegexOptions.Compiled;


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

    public static string Rotulo(string secao, List<string> textos)
    {
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
}
