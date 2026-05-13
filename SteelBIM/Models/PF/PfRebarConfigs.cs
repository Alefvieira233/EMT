using System.Collections.Generic;

namespace FerramentaEMT.Models.PF
{
    // =========================================================================
    // ENUMS
    // =========================================================================

    public enum PfBeamBarEndMode
    {
        Reta = 0,
        DobraInterna = 1
    }

    /// <summary>
    /// Modo de lancamento de barras em pilar/viga.
    /// Automatico: distribuicao paramétrica padrao (qtdLargura x qtdProfundidade ou ao longo do vao).
    /// Coordenadas: usuario fornece XCm/YCm de cada barra na lista <c>Coordenadas</c>.
    /// </summary>
    public enum PfRebarPlacementMode
    {
        Automatico = 0,
        Coordenadas = 1
    }

    public enum PfRebarSectionShape
    {
        Retangular = 0,
        Circular = 1
    }

    /// <summary>Angulo do gancho do estribo (135° default por NBR 6118).</summary>
    public enum PfStirrupHookAngle
    {
        Graus135 = 135,
        Graus90 = 90
    }

    /// <summary>Superficie da barra para calculo de aderencia eta1 (NBR 6118).</summary>
    public enum PfBarSurfaceType
    {
        Lisa = 0,
        Entalhada = 1,
        Nervurada = 2
    }

    /// <summary>Zona de aderencia para calculo eta2 (NBR 6118).</summary>
    public enum PfBondZone
    {
        Boa = 0,
        Ruim = 1
    }

    /// <summary>Tipo de ancoragem para calculo de comprimento (NBR 6118).</summary>
    public enum PfAnchorageType
    {
        Reta = 0,
        Gancho90 = 90,
        Gancho180 = 180,
        Gancho45 = 45
    }

    // =========================================================================
    // COORDINATES & PREVIEW (modo Coordenadas + UI de preview)
    // =========================================================================

    public sealed class PfColumnBarCoordinate
    {
        public double XCm { get; set; }
        public double YCm { get; set; }
    }

    public sealed class PfBeamBarCoordinate
    {
        public string BarTypeName { get; set; } = string.Empty;
        public string Posicao { get; set; } = string.Empty;
        public double XCm { get; set; }
        public double YCm { get; set; }
    }

    /// <summary>Snapshot da secao de pilar/viga para preview na UI (cm, local).</summary>
    public sealed class PfRebarSectionPreview
    {
        public PfRebarSectionShape Shape { get; set; } = PfRebarSectionShape.Retangular;
        public double MinXCm { get; set; }
        public double MaxXCm { get; set; }
        public double MinYCm { get; set; }
        public double MaxYCm { get; set; }
        public double RadiusCm { get; set; }
        public double WidthCm => MaxXCm - MinXCm;
        public double HeightCm => MaxYCm - MinYCm;
        public bool IsCircular => Shape == PfRebarSectionShape.Circular;
    }

    // =========================================================================
    // STIRRUPS — DUAL MODE (zoneamento NBR 6118 + simples)
    // =========================================================================

    /// <summary>
    /// Config de estribos de pilar.
    /// Suporta dois modos de espacamento:
    ///   - Zoneamento NBR 6118 (default): EspacamentoInferior/Central/Superior + AlturaZonaExtremidadeCm
    ///   - Simples (UsarEspacamentoUnico=true): EspacamentoCm uniforme
    /// O service decide qual usar com base na flag UsarEspacamentoUnico.
    /// </summary>
    public sealed class PfColumnStirrupsConfig
    {
        public string BarTypeName { get; set; } = string.Empty;

        /// <summary>Nome do RebarShape do projeto Revit (vazio = automatico).</summary>
        public string ShapeName { get; set; } = string.Empty;

        /// <summary>Diametro nominal da barra (mm), usado quando criamos shape automatico.</summary>
        public double DiametroMm { get; set; } = 6.3;

        public double CobrimentoCm { get; set; } = 3.0;

        // --- modo simples (Victor) ---
        public double EspacamentoCm { get; set; } = 12.0;
        public PfStirrupHookAngle Dobra { get; set; } = PfStirrupHookAngle.Graus135;

        /// <summary>
        /// Se <c>true</c>, o service usa <see cref="EspacamentoCm"/> uniformemente.
        /// Se <c>false</c> (default), o service usa o zoneamento NBR 6118 com os 3 espacamentos abaixo.
        /// </summary>
        public bool UsarEspacamentoUnico { get; set; } = false;

        // --- modo zoneamento NBR 6118 (preservado da nossa logica) ---
        public double EspacamentoInferiorCm { get; set; } = 12.0;
        public double EspacamentoCentralCm { get; set; } = 20.0;
        public double EspacamentoSuperiorCm { get; set; } = 12.0;
        public double AlturaZonaExtremidadeCm { get; set; } = 60.0;
    }

    /// <summary>
    /// Config de estribos de viga (analoga a <see cref="PfColumnStirrupsConfig"/>).
    /// Suporta dois modos: zoneamento NBR 6118 (apoio/central) ou espacamento unico.
    /// </summary>
    public sealed class PfBeamStirrupsConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public string ShapeName { get; set; } = string.Empty;
        public double DiametroMm { get; set; } = 6.3;
        public double CobrimentoCm { get; set; } = 3.0;

        // --- modo simples (Victor) ---
        public double EspacamentoCm { get; set; } = 12.0;
        public PfStirrupHookAngle Dobra { get; set; } = PfStirrupHookAngle.Graus135;

        /// <summary>
        /// Se <c>true</c>, o service usa <see cref="EspacamentoCm"/> uniformemente.
        /// Se <c>false</c> (default), o service usa o zoneamento por apoio/central.
        /// </summary>
        public bool UsarEspacamentoUnico { get; set; } = false;

        // --- modo zoneamento NBR 6118 (preservado da nossa logica) ---
        public double EspacamentoApoioCm { get; set; } = 12.0;
        public double EspacamentoCentralCm { get; set; } = 20.0;
        public double ComprimentoZonaApoioCm { get; set; } = 60.0;
    }

    // =========================================================================
    // BARS — modo Automatico ou Coordenadas
    // =========================================================================

    public sealed class PfColumnBarsConfig
    {
        public string BarTypeName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 3.0;
        public PfRebarPlacementMode ModoLancamento { get; set; } = PfRebarPlacementMode.Automatico;

        public int QuantidadeLargura { get; set; } = 2;
        public int QuantidadeProfundidade { get; set; } = 2;
        public int QuantidadeCircular { get; set; } = 8;

        public PfLapSpliceConfig Traspasse { get; } = new PfLapSpliceConfig();
        public List<PfColumnBarCoordinate> Coordenadas { get; } = new List<PfColumnBarCoordinate>();
    }

    public sealed class PfBeamBarsConfig
    {
        public string BarTypeSuperiorName { get; set; } = string.Empty;
        public string BarTypeInferiorName { get; set; } = string.Empty;
        public string BarTypeLateralName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 3.0;
        public PfRebarPlacementMode ModoLancamento { get; set; } = PfRebarPlacementMode.Automatico;

        public int QuantidadeSuperior { get; set; } = 2;
        public int QuantidadeInferior { get; set; } = 2;
        public int QuantidadeLateral { get; set; } = 0;

        public double ComprimentoGanchoCm { get; set; } = 10.0;
        public PfBeamBarEndMode ModoPonta { get; set; } = PfBeamBarEndMode.DobraInterna;

        public PfLapSpliceConfig Traspasse { get; } = new PfLapSpliceConfig();
        public List<PfBeamBarCoordinate> Coordenadas { get; } = new List<PfBeamBarCoordinate>();
    }

    // =========================================================================
    // LAP SPLICE (NBR 6118)
    // =========================================================================

    /// <summary>
    /// Configuracao de traspasse (lap splice) por NBR 6118.
    /// Quando <see cref="Enabled"/> = true e a barra ultrapassar <see cref="MaxBarLengthCm"/>,
    /// o service insere traspasse calculado por <c>PfNbr6118AnchorageService</c>.
    /// </summary>
    public sealed class PfLapSpliceConfig
    {
        public bool Enabled { get; set; }
        public double MaxBarLengthCm { get; set; } = 1200.0;
        public double ConcreteFckMpa { get; set; } = 25.0;
        public double SteelFykMpa { get; set; } = 500.0;
        public PfBarSurfaceType BarSurface { get; set; } = PfBarSurfaceType.Nervurada;
        public PfBondZone BondZone { get; set; } = PfBondZone.Boa;
        public PfAnchorageType AnchorageType { get; set; } = PfAnchorageType.Reta;
        public double SplicePercentage { get; set; } = 50.0;
        public double BarSpacingCm { get; set; } = 8.0;
        public double AsCalcCm2 { get; set; }
        public double AsEfCm2 { get; set; }
    }

    // =========================================================================
    // CONSOLO
    // =========================================================================

    public sealed class PfConsoloRebarConfig
    {
        public string BarTypeTiranteName { get; set; } = string.Empty;
        public int NumeroTirantes { get; set; } = 4;
        public double ComprimentoTiranteCm { get; set; } = 100.0;
        public string BarTypeSuspensaoName { get; set; } = string.Empty;
        public int NumeroSuspensoes { get; set; } = 4;
        public double ComprimentoSuspensaoCm { get; set; } = 60.0;
        public string BarTypeEstriboVerticalName { get; set; } = string.Empty;
        public int QuantidadeEstribosVerticais { get; set; } = 5;
        public string BarTypeEstriboHorizontalName { get; set; } = string.Empty;
        public int QuantidadeEstribosHorizontais { get; set; } = 5;
    }

    // =========================================================================
    // BLOCO DE DUAS ESTACAS (Victor)
    // =========================================================================

    /// <summary>
    /// Config do comando "Bloco 2 Estacas" — analogo a viga, com barras superior/inferior/lateral.
    /// </summary>
    public sealed class PfTwoPileCapRebarConfig
    {
        public string BarTypeSuperiorName { get; set; } = string.Empty;
        public string BarTypeInferiorName { get; set; } = string.Empty;
        public string BarTypeLateralName { get; set; } = string.Empty;
        public double CobrimentoCm { get; set; } = 5.0;
        public int QuantidadeSuperior { get; set; } = 4;
        public int QuantidadeInferior { get; set; } = 4;
        public int QuantidadeLateral { get; set; } = 0;
        public double ComprimentoGanchoCm { get; set; } = 10.0;
        public PfBeamBarEndMode ModoPonta { get; set; } = PfBeamBarEndMode.DobraInterna;
    }
}
