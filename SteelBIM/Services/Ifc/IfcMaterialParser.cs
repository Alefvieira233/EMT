using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteelBIM.Services.Ifc
{
    public static class IfcMaterialParser
    {
        // v2.7.8 (auditoria 2026-05-25 #003 perf): cache de ExtrairTipoEDimensoes.
        // Hot path: CalcularScore eh chamada N×M vezes em ReConstruirGrupos do
        // Conversor IFC (Alef relatou 400 grupos × 59 perfis Revit = 24k calls).
        // Cada call faz 2 invocacoes de ExtrairTipoEDimensoes (regex + parse).
        //
        // Cache colapsa para ~unique strings (tipicamente 59 perfis Revit +
        // 400 secoes IFC ~= 459 entries no max). ConcurrentDictionary garante
        // thread-safety mesmo Revit sendo single-threaded (defensivo p/ futura
        // execucao em sub-thread). Entries imutaveis — IReadOnlyList<double>
        // previne mutacao acidental do cache pelo caller.
        //
        // Memory footprint: ~500 entries × ~50 bytes = 25KB worst case. OK.
        private static readonly ConcurrentDictionary<string, (string tipo, IReadOnlyList<double> dims)> _extracaoCache
            = new ConcurrentDictionary<string, (string tipo, IReadOnlyList<double> dims)>(StringComparer.Ordinal);

        /// <summary>
        /// v2.7.8: limpa cache de extracao. Uso esperado: testes que precisam
        /// de estado limpo entre cenarios. Em producao nunca chamado — cache
        /// vive durante toda a sessao do Revit.
        /// </summary>
        internal static void ResetCacheForTests() => _extracaoCache.Clear();

        /// <summary>
        /// v2.7.8: count atual de entries no cache. Internal para testes
        /// poderem assertar comportamento de memoization.
        /// </summary>
        internal static int CacheCount => _extracaoCache.Count;

        // Parametros IFC lidos por esta ferramenta:
        // IfcMaterial  -> secao transversal + norma + material
        // IfcName      -> nome do membro (ex: "Member 80")
        // IfcGUID      -> identificador unico do elemento IFC

        // ─── BUG 3 v2.7.0: rejeitar pseudo-secoes de concreto ─────────────────
        // O parser original aceitava "R_M1 R_M1 200/400" como se fosse um perfil
        // estrutural (vinha de armadura de concreto NBR 6118/EN 1992). Resultado:
        // pilares de concreto apareciam no DataGrid como falsos perfis a converter.

        // Padrao 1: R_M, S_M, RQ_M, SQ_M com numero (classificacao de armadura).
        // Cobre tanto "R_M1" sozinho quanto "R_M1 R_M1" (repeticao tipica).
        private static readonly Regex RxConcretoMArmadura = new Regex(
            @"^\s*[RS]Q?_M\d+", RegexOptions.Compiled);

        // Padrao 2: NphiM (N barras de diametro M) — ex: "12phi10", "8phi16".
        private static readonly Regex RxConcretoArmaduraBarras = new Regex(
            @"^\s*\d+phi\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Padrao 3: XX/YY sozinho (so dimensao, sem tipo estrutural) — ex: "200/400".
        private static readonly Regex RxConcretoSoDimensao = new Regex(
            @"^\s*\d+/\d+\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Extrai o nome da secao transversal do campo IfcMaterial.
        /// "A572, Grade 50 (HR Shapes) | AISC 360-16 (W 360x44.6)" -> "W 360x44.6"
        /// "A36 | ASME C 200/80/2/2/25/C" -> "C 200/80/2/2/25/C"
        ///
        /// v2.7.0: rejeita pseudo-secoes de concreto (R_M1, SQ_M1, 12phi10,
        /// 200/400 sozinho) que antes eram tratadas como perfil estrutural.
        /// </summary>
        public static string ExtrairNomeSecao(string ifcMaterial)
        {
            if (string.IsNullOrWhiteSpace(ifcMaterial))
                return string.Empty;

            string[] segmentos = ifcMaterial.Split('|');
            string ultimo = segmentos[segmentos.Length - 1].Trim();

            string candidato;

            int abre = ultimo.LastIndexOf('(');
            int fecha = ultimo.LastIndexOf(')');
            if (abre >= 0 && fecha > abre)
            {
                candidato = ultimo.Substring(abre + 1, fecha - abre - 1).Trim();
            }
            else
            {
                int espaco = ultimo.IndexOf(' ');
                if (espaco > 0)
                {
                    string prefixo = ultimo.Substring(0, espaco);
                    if (prefixo.Contains("-") || prefixo.All(char.IsDigit))
                        candidato = ultimo.Substring(espaco + 1).Trim();
                    else
                        candidato = ultimo;
                }
                else
                {
                    candidato = ultimo;
                }
            }

            // BUG 3 v2.7.0: rejeitar pseudo-secoes de concreto
            if (RxConcretoMArmadura.IsMatch(candidato)
                || RxConcretoArmaduraBarras.IsMatch(candidato)
                || RxConcretoSoDimensao.IsMatch(candidato))
            {
                return string.Empty;
            }

            return candidato;
        }

        /// <summary>
        /// Extrai o nome do material de engenharia do campo IfcMaterial.
        /// "A572, Grade 50 (HR Shapes) | AISC 360-16 (W 360x44.6)" -> "A572, Grade 50"
        /// </summary>
        public static string ExtrairNomeMaterial(string ifcMaterial)
        {
            if (string.IsNullOrWhiteSpace(ifcMaterial))
                return string.Empty;

            string parte = ifcMaterial.Split('|')[0].Trim();

            int paren = parte.IndexOf('(');
            if (paren > 0)
                parte = parte.Substring(0, paren).Trim();

            return parte.TrimEnd(',').Trim();
        }

        /// <summary>
        /// Normaliza o nome para comparacao: sem espacos, maiusculas.
        /// "W 360x44.6" -> "W360X44.6"
        /// </summary>
        public static string NormalizarNome(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return string.Empty;

            return nome.Replace(" ", "").ToUpperInvariant();
        }

        /// <summary>
        /// Correspondencia simples por substring (mantida para compatibilidade).
        /// </summary>
        public static bool CorresponderNome(string nomeSecao, string nomePerfil)
        {
            if (string.IsNullOrWhiteSpace(nomeSecao) || string.IsNullOrWhiteSpace(nomePerfil))
                return false;

            string secNorm = NormalizarNome(nomeSecao);
            string perNorm = NormalizarNome(nomePerfil);

            return perNorm.Contains(secNorm) || secNorm.Contains(perNorm);
        }

        // Mapeamento de tipos IFC -> tipos Revit compativeis (ordem = prioridade)
        private static readonly Dictionary<string, string[]> _tipoMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["C"] = new[] { "UE", "U", "C" },
                ["U"] = new[] { "U", "UE", "C" },
                ["UE"] = new[] { "UE", "U", "C" },
                ["RHS"] = new[] { "RHS", "SHS", "HSS" },
                ["SHS"] = new[] { "SHS", "RHS", "HSS" },
                ["CHS"] = new[] { "CHS", "HSS" },
                ["HSS"] = new[] { "HSS", "RHS", "SHS", "CHS" },
                ["W"] = new[] { "W", "WF" },
                ["I"] = new[] { "I", "IPE", "W" },
                ["IPE"] = new[] { "IPE", "I", "W" },
                ["HEA"] = new[] { "HEA", "HEB" },
                ["HEB"] = new[] { "HEB", "HEA" },
                ["L"] = new[] { "L" },
                ["T"] = new[] { "T" },
            };

        private static readonly int[] _tipoPts = { 100, 70, 50 };

        /// <summary>
        /// Calcula pontuacao de compatibilidade entre uma secao IFC e o nome de um tipo Revit.
        /// Leva em conta tipo estrutural (C->Ue, RHS->SHS, etc.) e dimensoes.
        /// Retorna 0 se os tipos sao incompativeis.
        /// </summary>
        public static int CalcularScore(string nomeSecaoIfc, string nomeTipoPerfil)
        {
            if (string.IsNullOrWhiteSpace(nomeSecaoIfc) || string.IsNullOrWhiteSpace(nomeTipoPerfil))
                return 0;

            (string tipoS, IReadOnlyList<double> dimsS) = ExtrairTipoEDimensoes(nomeSecaoIfc);
            (string tipoP, IReadOnlyList<double> dimsP) = ExtrairTipoEDimensoes(nomeTipoPerfil);

            int tipoScore = ScoreTipo(tipoS, tipoP);
            if (tipoScore == 0)
                return 0;

            int dimScore = 0;
            if (dimsS.Count > 0 && dimsP.Count > 0)
            {
                dimScore += DimPts(dimsS[0], dimsP[0], 50, 35, 20, 8);
                if (dimsS.Count > 1 && dimsP.Count > 1)
                    dimScore += DimPts(dimsS[1], dimsP[1], 30, 20, 10, 4);
            }

            return tipoScore + dimScore;
        }

        private static int ScoreTipo(string tipoSecao, string tipoPerfil)
        {
            if (string.IsNullOrEmpty(tipoSecao) || string.IsNullOrEmpty(tipoPerfil))
                return 0;

            if (tipoSecao.Equals(tipoPerfil, StringComparison.OrdinalIgnoreCase))
                return 100;

            if (_tipoMap.TryGetValue(tipoSecao, out string[] compat))
            {
                for (int i = 0; i < compat.Length && i < _tipoPts.Length; i++)
                {
                    if (compat[i].Equals(tipoPerfil, StringComparison.OrdinalIgnoreCase))
                        return _tipoPts[i];
                }
            }

            return 0;
        }

        private static int DimPts(double a, double b, int pt1, int pt2, int pt3, int pt4)
        {
            if (a <= 0 || b <= 0)
                return 0;

            double diff = Math.Abs(a - b) / Math.Max(a, b);

            if (diff < 0.01)
                return pt1;
            if (diff < 0.05)
                return pt2;
            if (diff < 0.15)
                return pt3;
            if (diff < 0.30)
                return pt4;
            return 0;
        }

        private static readonly Regex RxNums = new Regex(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);
        private static readonly Regex RxTipo = new Regex(@"^([A-Za-z]+)", RegexOptions.Compiled);

        // v2.7.8: empty fallback compartilhado pra evitar alocar List vazio em cada miss.
        private static readonly IReadOnlyList<double> EmptyDims = Array.Empty<double>();

        private static (string tipo, IReadOnlyList<double> dims) ExtrairTipoEDimensoes(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return (string.Empty, EmptyDims);

            // v2.7.8: cache hit comum (mesmo perfil aparece ~400× em galpao).
            // GetOrAdd eh atomico (ConcurrentDictionary) e factory so executa
            // em miss — sem trabalho duplicado em corrida thread (defensivo).
            return _extracaoCache.GetOrAdd(nome, ParseSemCache);
        }

        private static (string tipo, IReadOnlyList<double> dims) ParseSemCache(string nome)
        {
            string tipo = string.Empty;
            Match mTipo = RxTipo.Match(nome.Trim());
            if (mTipo.Success)
                tipo = mTipo.Value.ToUpperInvariant();

            var dims = new List<double>();
            foreach (Match m in RxNums.Matches(nome))
            {
                if (double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Any,
                                    CultureInfo.InvariantCulture, out double d))
                    dims.Add(d);
            }

            // Devolve IReadOnlyList pra prevenir mutacao acidental do cache pelo caller.
            // dims.Count == 0 reusa EmptyDims (sem alloc); senao a List<double> propria
            // (ja imutavel via interface readonly — caller nao tem refs com List<>).
            return (tipo, dims.Count == 0 ? EmptyDims : (IReadOnlyList<double>)dims);
        }
    }
}
