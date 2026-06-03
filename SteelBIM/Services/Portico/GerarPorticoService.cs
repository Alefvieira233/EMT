#nullable enable
using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using SteelBIM.Infrastructure;
using SteelBIM.Models;
using SteelBIM.Utils;

namespace SteelBIM.Services.Portico
{
    /// <summary>
    /// v2.8.14: "Gerar Projeto Completo (Portico)" — servico HEADLESS que materializa um galpao
    /// inteiro numa unica transacao a partir do GerarPorticoConfig. A geometria vem do nucleo PURO
    /// <see cref="PorticoGeometriaCalculator"/>; a treliça de cada portico reusa
    /// <c>TrelicaService.GerarTrelicaCompletaNoEixo</c>. Sem nenhum pick interativo.
    /// </summary>
    public sealed class GerarPorticoService
    {
        private static readonly double FtPerMm = RevitUtils.FT_PER_MM;

        public void Executar(UIDocument uidoc, GerarPorticoConfig config)
        {
            Document doc = uidoc.Document;

            Level? nivel = ResolverNivel(doc);
            if (nivel == null)
            {
                AppDialogService.ShowError("Gerar Pórtico", "Nenhum nível encontrado no projeto.", "Nível ausente");
                return;
            }
            if (config.SymbolPilar == null)
            {
                AppDialogService.ShowError("Gerar Pórtico", "Selecione o perfil do pilar.", "Configuração incompleta");
                return;
            }

            GerarPorticoEntrada entrada = MapearEntrada(config);
            PorticoLayout layout = PorticoGeometriaCalculator.Calcular(entrada);
            if (layout.XPorticosMm.Count == 0)
            {
                AppDialogService.ShowWarning(
                    "Gerar Pórtico",
                    "Geometria inválida. Verifique nº de pórticos (≥2), espaçamento e vão.",
                    "Nada gerado");
                return;
            }

            int pilares = 0;
            int trelicas = 0;
            int membrosTrelica = 0;
            int vigas = 0;

            using (Transaction t = new Transaction(doc, "Gerar Pórtico Completo"))
            {
                t.Start();
                AtivarSimbolos(doc, config);

                // ===== PILARES =====
                foreach (Segmento s in layout.Pilares)
                {
                    if (CriarPilar(doc, config.SymbolPilar, nivel, ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B)))
                        pilares++;
                }

                // ===== COBERTURA: treliça OU viga =====
                if (config.UsarTrelica)
                {
                    TrelicaConfig tc = MapearTrelicaConfig(config);
                    TrelicaService trelicaService = new TrelicaService();
                    foreach (Segmento s in layout.EixosInferioresTrelica)
                    {
                        Line eixo = Line.CreateBound(ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B));
                        membrosTrelica += trelicaService.GerarTrelicaCompletaNoEixo(doc, nivel, eixo, tc);
                        trelicas++;
                    }
                }
                else
                {
                    foreach (Segmento s in layout.Vigas)
                    {
                        if (CriarBarra(doc, config.SymbolViga, nivel, ParaXYZ(nivel, s.A), ParaXYZ(nivel, s.B)))
                            vigas++;
                    }
                }

                t.Commit();
            }

            string resumo = $"Pórtico gerado.\nPilares: {pilares}";
            if (config.UsarTrelica)
                resumo += $"\nTreliças: {trelicas} ({membrosTrelica} membros)";
            else
                resumo += $"\nVigas: {vigas}";

            Logger.Info("[GerarPortico] pilares={P} trelicas={T} membros={M} vigas={V}",
                pilares, trelicas, membrosTrelica, vigas);
            AppDialogService.ShowInfo("Gerar Pórtico", resumo, "Concluído");
        }

        // ===== Nivel =====
        private static Level? ResolverNivel(Document doc)
        {
            Level? ativo = doc.ActiveView?.GenLevel;
            if (ativo != null)
                return ativo;

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();
        }

        // ===== Mapeamentos config -> entrada pura / TrelicaConfig =====
        private static GerarPorticoEntrada MapearEntrada(GerarPorticoConfig c) => new GerarPorticoEntrada
        {
            NumeroPorticos = c.NumeroPorticos,
            EspacamentoPorticosMm = c.EspacamentoPorticosMm,
            VaoGalpaoMm = c.VaoGalpaoMm,
            AlturaPilarMm = c.AlturaPilarMm,
            UsarTrelica = c.UsarTrelica,
            AlturaExtremidadeMm = c.AlturaExtremidadeMm,
            AlturaCentralMm = c.AlturaCentralMm,
            AlturaCumeeiraMm = c.AlturaCumeeiraMm,
            LancarTercas = c.LancarTercas,
            EspacamentoTercasMm = c.EspacamentoTercasMm,
            ContravCobertura = c.ContravCobertura,
            ContravPilares = c.ContravPilares,
            LancarLinhaCorrente = c.LancarLinhaCorrente
        };

        private static TrelicaConfig MapearTrelicaConfig(GerarPorticoConfig c) => new TrelicaConfig
        {
            TrelicaCompleta = true,
            Padrao = c.PadraoTrelica,
            AlturaExtremidadeMm = c.AlturaExtremidadeMm,
            AlturaCentralMm = c.AlturaCentralMm,
            Quantidade = Math.Max(1, c.DivisoesTrelica),
            ModoEspacamento = TrussSpacingMode.Uniforme,
            LancarMontante = true,
            LancarDiagonal = true,
            MontantesExtremidade = true,
            DiagonaisExtremidade = true,
            SymbolBanzoSuperior = c.SymbolBanzoSuperior,
            SymbolBanzoInferior = c.SymbolBanzoInferior,
            SymbolBanzo = c.SymbolBanzoInferior ?? c.SymbolBanzoSuperior, // fallback p/ guards internos
            SymbolDiagonal = c.SymbolDiagonal,
            SymbolMontante = c.SymbolMontante
        };

        private static void AtivarSimbolos(Document doc, GerarPorticoConfig c)
        {
            FamilySymbol?[] simbolos =
            {
                c.SymbolPilar, c.SymbolViga, c.SymbolTerca,
                c.SymbolContravCobertura, c.SymbolContravPilares, c.SymbolLinhaCorrente,
                c.SymbolBanzoSuperior, c.SymbolBanzoInferior, c.SymbolDiagonal, c.SymbolMontante
            };
            foreach (FamilySymbol? s in simbolos)
            {
                if (s != null && !s.IsActive)
                    s.Activate();
            }
            doc.Regenerate();
        }

        // ===== Criacao de membros =====
        private static bool CriarPilar(Document doc, FamilySymbol symbol, Level nivel, XYZ baseP, XYZ topo)
        {
            if (baseP.DistanceTo(topo) < RevitUtils.EPS)
                return false;

            bool ehColuna = symbol.Category?.Id.Value == (long)BuiltInCategory.OST_StructuralColumns;
            if (!ehColuna)
                return CriarBarra(doc, symbol, nivel, baseP, topo); // perfil nao-coluna: vira viga reta

            FamilyInstance fi = doc.Create.NewFamilyInstance(baseP, symbol, nivel, StructuralType.Column);
            if (fi == null)
                return false;

            SetParamId(fi, BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, nivel.Id);
            SetParamId(fi, BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, nivel.Id);
            SetParamDouble(fi, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM, baseP.Z - nivel.Elevation);
            SetParamDouble(fi, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM, topo.Z - nivel.Elevation);
            return true;
        }

        private static bool CriarBarra(Document doc, FamilySymbol? symbol, Level nivel, XYZ a, XYZ b)
        {
            if (symbol == null || a.DistanceTo(b) < RevitUtils.EPS)
                return false;

            Line line = Line.CreateBound(a, b);
            FamilyInstance fi = doc.Create.NewFamilyInstance(line, symbol, nivel, StructuralType.Beam);
            if (fi == null)
                return false;

            RevitUtils.DisallowJoins(fi);
            return true;
        }

        // ===== Helpers =====
        private static XYZ ParaXYZ(Level nivel, Ponto3D p) =>
            new XYZ(p.XMm * FtPerMm, p.YMm * FtPerMm, nivel.Elevation + p.ZMm * FtPerMm);

        private static void SetParamId(FamilyInstance fi, BuiltInParameter bip, ElementId valor)
        {
            try
            {
                Parameter p = fi.get_Parameter(bip);
                if (p != null && !p.IsReadOnly)
                    p.Set(valor);
            }
            catch
            {
                // familia sem o parametro: ignora.
            }
        }

        private static void SetParamDouble(FamilyInstance fi, BuiltInParameter bip, double valor)
        {
            try
            {
                Parameter p = fi.get_Parameter(bip);
                if (p != null && !p.IsReadOnly)
                    p.Set(valor);
            }
            catch
            {
                // familia sem o parametro: ignora.
            }
        }
    }
}
