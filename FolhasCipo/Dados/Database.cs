using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FolhasCipo;

/// <summary>Acesso ao banco SQLite (arquivo dados/folhas.db ao lado do executável).</summary>
public class Database
{
    public string Caminho { get; }
    readonly string _cs;

    public Database(string? caminho = null)
    {
        Caminho = caminho ?? Path.Combine(AppContext.BaseDirectory, "dados", "folhas.db");
        Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
        _cs = new SqliteConnectionStringBuilder { DataSource = Caminho }.ToString();
        CriarTabelas();
    }

    SqliteConnection Abrir()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
        return c;
    }

    void Exec(SqliteConnection c, string sql, params (string, object?)[] ps)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    void CriarTabelas()
    {
        using var c = Abrir();
        Exec(c, @"
CREATE TABLE IF NOT EXISTS policiais(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  antiguidade INTEGER NOT NULL DEFAULT 0,
  posto TEXT NOT NULL DEFAULT '',
  quadro TEXT NOT NULL DEFAULT '',
  matricula TEXT NOT NULL UNIQUE,
  nome TEXT NOT NULL,
  nome_guerra TEXT NOT NULL DEFAULT '',
  sexo TEXT NOT NULL DEFAULT '',
  unidade TEXT NOT NULL DEFAULT '',
  comportamento TEXT NOT NULL DEFAULT '',
  tipo_folha TEXT NOT NULL DEFAULT 'AUTO',
  ativo INTEGER NOT NULL DEFAULT 1,
  observacao TEXT NOT NULL DEFAULT '');
CREATE TABLE IF NOT EXISTS boletins(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  numero INTEGER NOT NULL,
  ano INTEGER NOT NULL,
  data TEXT NOT NULL,
  sei_id TEXT NOT NULL DEFAULT '',
  arquivo TEXT NOT NULL DEFAULT '',
  importado_em TEXT NOT NULL,
  UNIQUE(numero, ano));
CREATE TABLE IF NOT EXISTS publicacoes(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  policial_id INTEGER NOT NULL REFERENCES policiais(id) ON DELETE CASCADE,
  boletim_id INTEGER NOT NULL REFERENCES boletins(id) ON DELETE CASCADE,
  rotulo TEXT NOT NULL,
  blocos TEXT NOT NULL,
  ordem INTEGER NOT NULL DEFAULT 0,
  manual INTEGER NOT NULL DEFAULT 0,
  editada INTEGER NOT NULL DEFAULT 0);
CREATE INDEX IF NOT EXISTS ix_pub_pol ON publicacoes(policial_id);
CREATE TABLE IF NOT EXISTS config(chave TEXT PRIMARY KEY, valor TEXT NOT NULL);");
        // colunas acrescentadas depois da primeira versão
        var cols = new HashSet<string>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(policiais)";
            using var r = cmd.ExecuteReader();
            while (r.Read()) cols.Add(r.GetString(1));
        }
        foreach (var (col, tipo) in new[] { ("inclusao_data", "TEXT"), ("inclusao_doc", "TEXT NOT NULL DEFAULT ''"),
                                            ("exclusao_data", "TEXT"), ("exclusao_doc", "TEXT NOT NULL DEFAULT ''") })
            if (!cols.Contains(col)) Exec(c, $"ALTER TABLE policiais ADD COLUMN {col} {tipo}");
    }

    static string? DataSql(DateTime? d) => d?.ToString("yyyy-MM-dd");
    static DateTime? LerData(SqliteDataReader r, int i) => r.IsDBNull(i) || r.GetString(i).Length == 0 ? null : ParseData(r.GetString(i));

    // ---------------- Policiais ----------------

    public List<Policial> ListarPoliciais(bool somenteAtivos = false)
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, antiguidade, posto, quadro, matricula, nome, nome_guerra, sexo, unidade, comportamento, tipo_folha, ativo, observacao, inclusao_data, inclusao_doc, exclusao_data, exclusao_doc FROM policiais"
                          + (somenteAtivos ? " WHERE ativo = 1" : "") + " ORDER BY antiguidade, nome";
        using var r = cmd.ExecuteReader();
        var l = new List<Policial>();
        while (r.Read())
            l.Add(new Policial
            {
                Id = r.GetInt32(0), Antiguidade = r.GetInt32(1), PostoGrad = r.GetString(2), Quadro = r.GetString(3),
                Matricula = r.GetString(4), Nome = r.GetString(5), NomeGuerra = r.GetString(6), Sexo = r.GetString(7),
                Unidade = r.GetString(8), Comportamento = r.GetString(9), TipoFolha = r.GetString(10),
                Ativo = r.GetInt32(11) == 1, Observacao = r.GetString(12),
                InclusaoData = LerData(r, 13), InclusaoDoc = r.GetString(14), ExclusaoData = LerData(r, 15), ExclusaoDoc = r.GetString(16),
            });
        return l;
    }

    public void SalvarPolicial(Policial p)
    {
        using var c = Abrir();
        var ps = new (string, object?)[]
        {
            ("$id", p.Id), ("$ant", p.Antiguidade), ("$posto", p.PostoGrad.Trim()), ("$quadro", p.Quadro.Trim()),
            ("$mat", p.Matricula.Trim()), ("$nome", p.Nome.Trim().ToUpperInvariant()), ("$guerra", p.NomeGuerra.Trim().ToUpperInvariant()),
            ("$sexo", p.Sexo.Trim()), ("$uni", p.Unidade.Trim()), ("$comp", p.Comportamento.Trim()), ("$tipo", p.TipoFolha),
            ("$ativo", p.Ativo ? 1 : 0), ("$obs", p.Observacao),
            ("$incd", DataSql(p.InclusaoData)), ("$incdoc", p.InclusaoDoc.Trim()), ("$excd", DataSql(p.ExclusaoData)), ("$excdoc", p.ExclusaoDoc.Trim()),
        };
        if (p.Id == 0)
        {
            Exec(c, @"INSERT INTO policiais(antiguidade, posto, quadro, matricula, nome, nome_guerra, sexo, unidade, comportamento, tipo_folha, ativo, observacao,
                      inclusao_data, inclusao_doc, exclusao_data, exclusao_doc)
                      VALUES($ant,$posto,$quadro,$mat,$nome,$guerra,$sexo,$uni,$comp,$tipo,$ativo,$obs,$incd,$incdoc,$excd,$excdoc)", ps);
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT last_insert_rowid()";
            p.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }
        else
        {
            Exec(c, @"UPDATE policiais SET antiguidade=$ant, posto=$posto, quadro=$quadro, matricula=$mat, nome=$nome, nome_guerra=$guerra,
                      sexo=$sexo, unidade=$uni, comportamento=$comp, tipo_folha=$tipo, ativo=$ativo, observacao=$obs,
                      inclusao_data=$incd, inclusao_doc=$incdoc, exclusao_data=$excd, exclusao_doc=$excdoc WHERE id=$id", ps);
        }
    }

    public void ExcluirPolicial(int id)
    {
        using var c = Abrir();
        Exec(c, "DELETE FROM policiais WHERE id=$id", ("$id", id));
    }

    // ---------------- Boletins ----------------

    public List<Boletim> ListarBoletins()
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT b.id, b.numero, b.ano, b.data, b.sei_id, b.arquivo, b.importado_em,
                                   (SELECT COUNT(*) FROM publicacoes p WHERE p.boletim_id = b.id)
                            FROM boletins b ORDER BY b.ano DESC, b.numero DESC";
        using var r = cmd.ExecuteReader();
        var l = new List<Boletim>();
        while (r.Read())
            l.Add(new Boletim
            {
                Id = r.GetInt32(0), Numero = r.GetInt32(1), Ano = r.GetInt32(2), Data = ParseData(r.GetString(3)),
                SeiId = r.GetString(4), Arquivo = r.GetString(5), ImportadoEm = ParseData(r.GetString(6)), QtdPublicacoes = r.GetInt32(7),
            });
        return l;
    }

    public Boletim? ObterBoletim(int numero, int ano) => ListarBoletins().FirstOrDefault(b => b.Numero == numero && b.Ano == ano);

    /// <summary>Insere ou atualiza o BI e retorna o id.</summary>
    public int SalvarBoletim(Boletim b)
    {
        using var c = Abrir();
        Exec(c, @"INSERT INTO boletins(numero, ano, data, sei_id, arquivo, importado_em) VALUES($n,$a,$d,$s,$f,$i)
                  ON CONFLICT(numero, ano) DO UPDATE SET data=$d, sei_id=$s, arquivo=$f, importado_em=$i",
            ("$n", b.Numero), ("$a", b.Ano), ("$d", b.Data.ToString("yyyy-MM-dd")), ("$s", b.SeiId), ("$f", b.Arquivo),
            ("$i", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id FROM boletins WHERE numero=$n AND ano=$a";
        cmd.Parameters.AddWithValue("$n", b.Numero);
        cmd.Parameters.AddWithValue("$a", b.Ano);
        b.Id = Convert.ToInt32(cmd.ExecuteScalar());
        return b.Id;
    }

    public void ExcluirBoletim(int id)
    {
        using var c = Abrir();
        Exec(c, "DELETE FROM boletins WHERE id=$id", ("$id", id));
    }

    public int ContarEditadas(int boletimId)
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM publicacoes WHERE boletim_id=$b AND (editada=1 OR manual=1)";
        cmd.Parameters.AddWithValue("$b", boletimId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Remove as publicações automáticas (não editadas e não manuais) de um BI antes de reprocessar.</summary>
    public void LimparPublicacoesAutomaticas(int boletimId)
    {
        using var c = Abrir();
        Exec(c, "DELETE FROM publicacoes WHERE boletim_id=$b AND manual=0 AND editada=0", ("$b", boletimId));
    }

    // ---------------- Publicações ----------------

    public List<Publicacao> ListarPublicacoes(int? policialId = null, DateTime? de = null, DateTime? ate = null)
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        var where = new List<string>();
        if (policialId != null) { where.Add("p.policial_id = $pol"); cmd.Parameters.AddWithValue("$pol", policialId); }
        if (de != null) { where.Add("b.data >= $de"); cmd.Parameters.AddWithValue("$de", de.Value.ToString("yyyy-MM-dd")); }
        if (ate != null) { where.Add("b.data <= $ate"); cmd.Parameters.AddWithValue("$ate", ate.Value.ToString("yyyy-MM-dd")); }
        cmd.CommandText = @"SELECT p.id, p.policial_id, p.boletim_id, p.rotulo, p.blocos, p.ordem, p.manual, p.editada,
                                   b.numero, b.ano, b.data, b.sei_id
                            FROM publicacoes p JOIN boletins b ON b.id = p.boletim_id"
                          + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
                          + " ORDER BY b.data, b.numero, p.ordem, p.id";
        using var r = cmd.ExecuteReader();
        var l = new List<Publicacao>();
        while (r.Read())
            l.Add(new Publicacao
            {
                Id = r.GetInt32(0), PolicialId = r.GetInt32(1), BoletimId = r.GetInt32(2), Rotulo = r.GetString(3),
                BlocosJson = r.GetString(4), Ordem = r.GetInt32(5), Manual = r.GetInt32(6) == 1, Editada = r.GetInt32(7) == 1,
                BiNumero = r.GetInt32(8), BiAno = r.GetInt32(9), BiData = ParseData(r.GetString(10)), BiSeiId = r.GetString(11),
            });
        return l;
    }

    public void SalvarPublicacao(Publicacao p)
    {
        using var c = Abrir();
        var ps = new (string, object?)[]
        {
            ("$id", p.Id), ("$pol", p.PolicialId), ("$bi", p.BoletimId), ("$rot", p.Rotulo), ("$blo", p.BlocosJson),
            ("$ord", p.Ordem), ("$man", p.Manual ? 1 : 0), ("$edi", p.Editada ? 1 : 0),
        };
        if (p.Id == 0)
            Exec(c, @"INSERT INTO publicacoes(policial_id, boletim_id, rotulo, blocos, ordem, manual, editada)
                      VALUES($pol,$bi,$rot,$blo,$ord,$man,$edi)", ps);
        else
            Exec(c, @"UPDATE publicacoes SET policial_id=$pol, boletim_id=$bi, rotulo=$rot, blocos=$blo, ordem=$ord,
                      manual=$man, editada=$edi WHERE id=$id", ps);
    }

    public void InserirPublicacoes(IEnumerable<Publicacao> pubs)
    {
        using var c = Abrir();
        using var tx = c.BeginTransaction();
        foreach (var p in pubs)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO publicacoes(policial_id, boletim_id, rotulo, blocos, ordem, manual, editada)
                                VALUES($pol,$bi,$rot,$blo,$ord,0,0)";
            cmd.Parameters.AddWithValue("$pol", p.PolicialId);
            cmd.Parameters.AddWithValue("$bi", p.BoletimId);
            cmd.Parameters.AddWithValue("$rot", p.Rotulo);
            cmd.Parameters.AddWithValue("$blo", p.BlocosJson);
            cmd.Parameters.AddWithValue("$ord", p.Ordem);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void ExcluirPublicacao(int id)
    {
        using var c = Abrir();
        Exec(c, "DELETE FROM publicacoes WHERE id=$id", ("$id", id));
    }

    // ---------------- Configuração ----------------

    public Config LerConfig()
    {
        var cfg = new Config();
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT chave, valor FROM config";
        using var r = cmd.ExecuteReader();
        var props = typeof(Config).GetProperties().ToDictionary(p => p.Name);
        while (r.Read())
            if (props.TryGetValue(r.GetString(0), out var prop)) prop.SetValue(cfg, r.GetString(1));
        return cfg;
    }

    public void SalvarConfig(Config cfg)
    {
        using var c = Abrir();
        foreach (var prop in typeof(Config).GetProperties())
            Exec(c, "INSERT INTO config(chave, valor) VALUES($k,$v) ON CONFLICT(chave) DO UPDATE SET valor=$v",
                ("$k", prop.Name), ("$v", prop.GetValue(cfg)?.ToString() ?? ""));
    }

    static DateTime ParseData(string s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.MinValue;
}
