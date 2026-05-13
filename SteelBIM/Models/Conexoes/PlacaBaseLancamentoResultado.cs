namespace SteelBIM.Models.Conexoes
{
    public sealed class PlacaBaseLancamentoResultado
    {
        public int PilaresAnalisados { get; set; }

        public int PlacasInseridas { get; set; }

        public int PilaresSemApoio { get; set; }

        public int PilaresSemFaceSuperior { get; set; }

        public int FalhasInsercao { get; set; }

        public string Resumo()
        {
            return
                $"Pilares analisados: {PilaresAnalisados}\n" +
                $"Placas inseridas: {PlacasInseridas}\n" +
                $"Sem apoio compatível: {PilaresSemApoio}\n" +
                $"Sem face superior válida: {PilaresSemFaceSuperior}\n" +
                $"Falhas de inserção: {FalhasInsercao}";
        }
    }
}
