#nullable enable

namespace SteelBIM.Services.CncExport
{
    /// <summary>
    /// v2.8.11 (A6): escolha PURA da marca da peça para o cabeçalho DSTV/NC1, extraida de
    /// <c>DstvHeaderBuilder.GetPieceMark</c> (Revit-bound, sem testes). Precedencia:
    /// parametro configurado (se nao-vazio) -> ALL_MODEL_MARK (se nao-vazio) -> "ID-{id}".
    /// </summary>
    public static class DstvPieceMark
    {
        public static string Escolher(string? valorParametroConfig, string? valorMark, long elementId)
        {
            if (!string.IsNullOrWhiteSpace(valorParametroConfig))
                return valorParametroConfig!;
            if (!string.IsNullOrWhiteSpace(valorMark))
                return valorMark!;
            return $"ID-{elementId}";
        }
    }
}
