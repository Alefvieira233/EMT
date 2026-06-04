#nullable enable
using Autodesk.Revit.DB;

namespace SteelBIM.Models
{
    /// <summary>
    /// Configuracao de "Gerar Projeto Completo (Portico)": geometria do galpao + perfis
    /// (FamilySymbol ja resolvidos) + flags das secoes opcionais. Consumido pelo
    /// GerarPorticoService, que mapeia os numeros para GerarPorticoEntrada (nucleo puro).
    /// </summary>
    public sealed class GerarPorticoConfig
    {
        // ===== Geometria do galpao =====
        public int NumeroPorticos { get; set; } = 7;
        public double EspacamentoPorticosMm { get; set; } = 5000.0;
        public double VaoGalpaoMm { get; set; } = 15010.0;
        public double AlturaPilarMm { get; set; } = 4000.0;

        // ===== Pilar =====
        public FamilySymbol? SymbolPilar { get; set; }
        public bool PilarCentral { get; set; }   // pilar no meio do vão (mesmo perfil), por pórtico

        // ===== Cobertura: treliça (UsarTrelica=true) OU viga metálica =====
        public bool UsarTrelica { get; set; } = true;

        public TrussPattern PadraoTrelica { get; set; } = TrussPattern.Warren;
        public double AlturaExtremidadeMm { get; set; } = 600.0;   // H (apoio)
        public double AlturaCentralMm { get; set; } = 1600.0;      // B (cumeeira); B>H => duas águas
        public int DivisoesTrelica { get; set; } = 8;
        public FamilySymbol? SymbolBanzoSuperior { get; set; }
        public FamilySymbol? SymbolBanzoInferior { get; set; }
        public FamilySymbol? SymbolDiagonal { get; set; }
        public FamilySymbol? SymbolMontante { get; set; }

        // Rotação da seção do perfil por tipo (graus); default 0 = sem rotação (R4).
        public double RotacaoBanzoSuperiorGraus { get; set; }
        public double RotacaoBanzoInferiorGraus { get; set; }
        public double RotacaoDiagonalGraus { get; set; }
        public double RotacaoMontanteGraus { get; set; }

        public FamilySymbol? SymbolViga { get; set; }
        public double AlturaCumeeiraMm { get; set; } = 1500.0;     // elevacao da agua (modo viga)

        // ===== Terças =====
        public bool LancarTercas { get; set; } = true;
        public FamilySymbol? SymbolTerca { get; set; }
        public double EspacamentoTercasMm { get; set; } = 1500.0;
        public double ElevacaoTercasMm { get; set; } = 150.0;   // eleva a terça acima do banzo superior

        // Ligação de terça (opcional) — conexão inserida em cada cruzamento terça × banzo superior.
        public bool InserirLigacaoTerca { get; set; }
        public FamilySymbol? SymbolLigacaoTerca { get; set; }
        public double LigacaoOffsetZmm { get; set; }      // ajuste vertical (Z) da ligação
        public double LigacaoOffsetXmm { get; set; }      // ajuste lateral (ao longo do eixo da terça)
        public bool LigacaoInverterFace { get; set; }     // inverte a face da terça onde a ligação assenta

        // ===== Contraventamentos =====
        public bool ContravCobertura { get; set; }
        public FamilySymbol? SymbolContravCobertura { get; set; }
        public int TercasPorXCobertura { get; set; } = 2;       // 1 X de cobertura a cada N terças
        public DistribuicaoContravCobertura DistribuicaoContravCobertura { get; set; } = DistribuicaoContravCobertura.Extremidades;
        public bool ContravPilares { get; set; }
        public FamilySymbol? SymbolContravPilares { get; set; }
        public int NumeroXPilares { get; set; } = 2;            // nº de vãos com X vertical (paredes)

        // ===== Linha de corrente =====
        public bool LancarLinhaCorrente { get; set; }
        public FamilySymbol? SymbolLinhaCorrente { get; set; }
        public int NumeroLinhasCorrente { get; set; } = 3;      // nº de fileiras de linha de corrente

        // ===== Fundações =====
        public bool LancarFundacoes { get; set; }
        public FamilySymbol? SymbolFundacao { get; set; }
        public bool LancarArmaduraFundacao { get; set; }        // opt-in, best-effort (requer família rebar-host)

        // ===== Extras =====
        public bool CriarEixos { get; set; } = true;
        public bool LancarPlacasBase { get; set; }
    }
}
