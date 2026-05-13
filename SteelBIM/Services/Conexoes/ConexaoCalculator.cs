using System;
using System.Globalization;
using FerramentaEMT.Models.Conexoes;

namespace FerramentaEMT.Services.Conexoes
{
    /// <summary>
    /// Classe pura (sem Revit) com lógica matemática e de cálculo para conexões.
    /// Métodos aqui podem ser testados independentemente e linkados para testes.
    /// </summary>
    public static class ConexaoCalculator
    {
        /// <summary>
        /// Calcula o número total de parafusos necessários para múltiplas conexões.
        /// </summary>
        /// <param name="config">Configuração da conexão.</param>
        /// <param name="numConexoes">Número de conexões a gerar.</param>
        /// <returns>Total de parafusos.</returns>
        public static int ContarParafusosTotal(ConexaoConfig config, int numConexoes)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (numConexoes <= 0)
                return 0;

            int parafusosPorConexao = config.Tipo switch
            {
                TipoConexao.ChapaDePonta => config.ChapaPonta.NumParafusos,
                TipoConexao.DuplaCantoneira => config.Cantoneira.NumParafusosPorCantoneira * 2, // dupla
                TipoConexao.ChapaGusset => config.Gusset.NumParafusos,
                _ => 0
            };

            return parafusosPorConexao * numConexoes;
        }

        /// <summary>
        /// Gera um identificador (marker/tag) para a conexão baseado em sua configuração.
        /// Formato exemplo: "CP-12-150x250-4xM19" para Chapa de Ponta.
        /// </summary>
        /// <param name="config">Configuração da conexão.</param>
        /// <returns>String de marcação da conexão.</returns>
        public static string GerarMarcadorConexao(ConexaoConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return config.Tipo switch
            {
                TipoConexao.ChapaDePonta =>
                    GenerarMarcadorChapaPonta(config.ChapaPonta),
                TipoConexao.DuplaCantoneira =>
                    GenerarMarcadorCantoneira(config.Cantoneira),
                TipoConexao.ChapaGusset =>
                    GenerarMarcadorGusset(config.Gusset),
                _ => "DESCONHECIDO"
            };
        }

        /// <summary>
        /// Cultura usada para formatar marcadores. Marcadores viajam em nomes de arquivo,
        /// CNC, DSTV, desenhos de fabrica — SEMPRE culture-invariant (ponto como decimal),
        /// nunca dependente de locale da maquina (pt-BR usa virgula).
        /// </summary>
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Gera marcador para chapa de ponta. Exemplo: "CP-12.7-150x250-4xM19".</summary>
        private static string GenerarMarcadorChapaPonta(ConfiguracaoChapaPonta config)
        {
            int diam = (int)Math.Round(config.DiamParafusoMm);
            return string.Format(Inv, "CP-{0:F1}-{1:F0}x{2:F0}-{3}xM{4}",
                config.EspessuraMm, config.LarguraMm, config.AlturaMm, config.NumParafusos, diam);
        }

        /// <summary>Gera marcador para dupla cantoneira. Exemplo: "DC-100-12.7-3xM19".</summary>
        private static string GenerarMarcadorCantoneira(ConfiguracaoCantoneira config)
        {
            int diam = (int)Math.Round(config.DiamParafusoMm);
            return string.Format(Inv, "DC-{0:F0}-{1:F1}-{2}xM{3}",
                config.LarguraMm, config.EspessuraMm, config.NumParafusosPorCantoneira, diam);
        }

        /// <summary>Gera marcador para chapa gusset. Exemplo: "GS-300x300-45d-6xM19".</summary>
        private static string GenerarMarcadorGusset(ConfiguracaoGusset config)
        {
            int diam = (int)Math.Round(config.DiamParafusoMm);
            int angulo = (int)Math.Round(config.AnguloDiagonalDeg);
            return string.Format(Inv, "GS-{0:F0}x{1:F0}-{2}d-{3}xM{4}",
                config.LarguraMm, config.AlturaMm, angulo, config.NumParafusos, diam);
        }
    }
}
