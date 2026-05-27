#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Services
{
    /// <summary>
    /// Servico do comando "Inserir Conexao de Terca" (v2.8.1, Victor).
    ///
    /// <para>Lanca instancias de familia de conexao estrutural nas extremidades
    /// e/ou no meio das tercas selecionadas, posicionadas na face inferior da
    /// seçao (= encostadas no topo da viga de apoio).</para>
    ///
    /// <para>Algoritmo:</para>
    /// <list type="number">
    ///   <item>Para cada terca, calcula direcao XY + azimute + meia-altura da
    ///         seçao (procura param d/H/h/Altura).</item>
    ///   <item>Gera pontos candidatos: extremidades (p1,p2) e/ou meio.</item>
    ///   <item>Deduplica por proximidade XY (50mm) — quando duas tercas se
    ///         encontram no mesmo nó, insere UMA conexao só.</item>
    ///   <item>Aplica parametros customizados ao FamilySymbol antes do Activate.</item>
    ///   <item>Insere com Z = base - meiaAltura - offsetV (face inferior).</item>
    ///   <item>Rotaciona em 2 passos:
    ///         <list type="bullet">
    ///           <item>Eixo Z — alinha azimute com direcao da terça + offset usuario.</item>
    ///           <item>Eixo horizontal da terça — -90° pra erguer chapa de plano
    ///                 horizontal pra vertical (encosta na alma).</item>
    ///         </list>
    ///   </item>
    /// </list>
    /// </summary>
    public class ConexaoTercasService
    {
        // Tolerancia XY delegada para o helper puro (testavel sem Revit).
        // Ver ConexaoTercasMath.DedupToleranceMm (50.0) e DedupToleranceFt.

        public Result Executar(UIDocument uidoc, Document doc, ConexaoTercasConfig config, IList<Reference> refs)
        {
            if (config?.SymbolSelecionado == null)
                return Result.Failed;

            if (refs == null || refs.Count == 0)
            {
                AppDialogService.ShowWarning("Conexão de Terça", "Nenhuma terça foi selecionada.", "Selecao vazia");
                return Result.Failed;
            }

            double rotOffsetRad = RevitUtils.DegToRad(config.OffsetRotacaoGraus);
            double offsetVFt = config.OffsetVerticalAdicionalMm * RevitUtils.FT_PER_MM;

            List<PontoCon> pontos = new List<PontoCon>();

            foreach (Reference r in refs)
            {
                Element el = doc.GetElement(r);
                Curve? curve = RevitUtils.GetElementCurve(el);
                if (curve == null)
                    continue;

                XYZ p1 = curve.GetEndPoint(0);
                XYZ p2 = curve.GetEndPoint(1);
                XYZ dir = RevitUtils.SafeNormalize(p2 - p1);
                if (RevitUtils.IsZeroVector(dir))
                    continue;

                // Componente horizontal da direcao — usada como eixo de inclinacao.
                XYZ dirXY = new XYZ(dir.X, dir.Y, 0);
                if (dirXY.GetLength() > RevitUtils.EPS)
                    dirXY = dirXY.Normalize();
                else
                    dirXY = XYZ.BasisX;

                Level? nivel = RevitUtils.GetElementLevel(doc, el);
                double halfH = GetHalfSectionHeightFt(el);
                double azimuth = Math.Atan2(dirXY.Y, dirXY.X);
                double rotTotal = azimuth + rotOffsetRad;

                if (config.ColocarExtremidades)
                {
                    pontos.Add(new PontoCon(p1, halfH, rotTotal, dirXY, nivel));
                    pontos.Add(new PontoCon(p2, halfH, rotTotal, dirXY, nivel));
                }
                if (config.ColocarMeio)
                {
                    pontos.Add(new PontoCon((p1 + p2) / 2.0, halfH, rotTotal, dirXY, nivel));
                }
            }

            // Dedup por proximidade XY: quando duas tercas se encontram no mesmo nó,
            // coloca apenas uma conexao — ela conecta as duas tercas.
            List<XYZ> colocados = new List<XYZ>();
            int count = 0;

            using (Transaction t = new Transaction(doc, "Inserir Conexões de Terça"))
            {
                t.Start();

                if (!config.SymbolSelecionado.IsActive)
                    config.SymbolSelecionado.Activate();

                foreach (KeyValuePair<string, double> kvp in config.ParametrosInternos)
                {
                    Parameter p = config.SymbolSelecionado.LookupParameter(kvp.Key);
                    if (p != null && !p.IsReadOnly && p.StorageType == StorageType.Double)
                        p.Set(kvp.Value);
                }

                doc.Regenerate();

                foreach (PontoCon pt in pontos)
                {
                    bool jaColocado = colocados.Any(c => IsWithinDedupToleranceXY(c, pt.Base));
                    if (jaColocado)
                        continue;

                    InserirConexao(doc, pt.Base, pt.HalfH, offsetVFt, pt.Rot, pt.DirXY,
                                   config.SymbolSelecionado, pt.Nivel);
                    colocados.Add(pt.Base);
                    count++;
                }

                t.Commit();
            }

            AppDialogService.ShowInfo(
                "Conexão de Terça",
                $"{count} conexão(ões) inserida(s) com sucesso.",
                "Concluído");

            return Result.Succeeded;
        }

        /// <summary>
        /// True se os pontos <paramref name="a"/> e <paramref name="b"/> estao
        /// dentro de <see cref="ConexaoTercasMath.DedupToleranceMm"/> mm no
        /// plano XY (Z ignorado). Delega ao math puro pra permitir teste sem
        /// Revit runtime.
        /// </summary>
        internal static bool IsWithinDedupToleranceXY(XYZ a, XYZ b)
            => ConexaoTercasMath.IsWithinDistanceXY(a.X, a.Y, b.X, b.Y, ConexaoTercasMath.DedupToleranceFt);

        // Insere e orienta uma instancia da conexao:
        //  1. Posicao Z na face inferior da terça (= face superior da viga de apoio).
        //  2. Rotacao Z para alinhar o azimute com a direcao da terça (+ offset do usuario).
        //  3. Rotacao -90° ao redor do eixo horizontal da terça (dirXY) para erguer a
        //     familia de plano horizontal para vertical, encostando na alma da terça.
        private static void InserirConexao(
            Document doc,
            XYZ basePt,
            double halfHeightFt,
            double offsetVertFt,
            double rotacaoRad,
            XYZ dirXY,
            FamilySymbol symbol,
            Level? nivel)
        {
            XYZ insertPt = new XYZ(basePt.X, basePt.Y, basePt.Z - halfHeightFt - offsetVertFt);

            FamilyInstance fi = nivel != null
                ? doc.Create.NewFamilyInstance(insertPt, symbol, nivel, StructuralType.NonStructural)
                : doc.Create.NewFamilyInstance(insertPt, symbol, StructuralType.NonStructural);

            if (fi == null)
                return;

            // Passo 1: alinha o azimute com a direcao da terça.
            if (Math.Abs(rotacaoRad) > RevitUtils.EPS)
            {
                Line eixoZ = Line.CreateBound(insertPt, insertPt + XYZ.BasisZ);
                ElementTransformUtils.RotateElement(doc, fi.Id, eixoZ, rotacaoRad);
            }

            // Passo 2: ergue a familia de plano horizontal para vertical.
            // Rotacao de -90° ao redor do eixo horizontal da terça (dirXY).
            // Antes: plano da chapa = XY (normal = Z).
            // Depois: plano da chapa = vertical (normal = perp a terça em XY),
            //         que é a orientacao correta para encostar na alma da terça.
            //
            // NOTA: validacao em Revit pendente (Victor nao validou no env dele).
            // Se a chapa sair invertida -> trocar -Math.PI / 2.0 por Math.PI / 2.0.
            if (dirXY.GetLength() > RevitUtils.EPS)
            {
                Line eixoTerca = Line.CreateBound(insertPt, insertPt + dirXY);
                ElementTransformUtils.RotateElement(doc, fi.Id, eixoTerca, -Math.PI / 2.0);
            }
        }

        /// <summary>
        /// Extrai a meia-altura da seçao da terça em pés. Procura parametros
        /// "d", "H", "h", "Altura", "altura" no FamilySymbol. Retorna 0 se nao
        /// encontrar (resulta em conexao no eixo, comportamento defensivo).
        /// </summary>
        internal static double GetHalfSectionHeightFt(Element el)
        {
            if (el is not FamilyInstance fi)
                return 0;
            foreach (string name in new[] { "d", "H", "h", "Altura", "altura" })
            {
                Parameter p = fi.Symbol.LookupParameter(name);
                if (p?.StorageType == StorageType.Double)
                {
                    double v = p.AsDouble();
                    // Sanity: maior que EPS e menor que 5 pes (1.5m) — secao razoavel.
                    if (v > RevitUtils.EPS && v < 5.0)
                        return v / 2.0;
                }
            }
            return 0;
        }

        private readonly struct PontoCon
        {
            public XYZ Base { get; }
            public double HalfH { get; }
            public double Rot { get; }
            public XYZ DirXY { get; }
            public Level? Nivel { get; }

            public PontoCon(XYZ b, double h, double r, XYZ d, Level? n)
            {
                Base = b;
                HalfH = h;
                Rot = r;
                DirXY = d;
                Nivel = n;
            }
        }
    }
}
