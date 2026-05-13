using FerramentaEMT.Models.Privacy;

namespace FerramentaEMT.Infrastructure.Privacy
{
    /// <summary>
    /// Abstracao sobre a persistencia de PrivacySettings em privacy.json.
    /// Existe para mockagem nos testes do UpdateCheckService.
    /// </summary>
    public interface IPrivacySettingsStore
    {
        /// <summary>
        /// Carrega settings do disco. Se arquivo nao existe, JSON corrompido
        /// ou erro de IO, retorna PrivacySettings com defaults (todos Unset).
        /// </summary>
        PrivacySettings Load();

        /// <summary>
        /// Persiste settings no disco. Falhas de IO sao logadas mas nao re-lancadas.
        /// </summary>
        void Save(PrivacySettings settings);
    }
}
