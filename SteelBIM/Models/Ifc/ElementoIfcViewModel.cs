using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Autodesk.Revit.DB;
using SteelBIM.Forms;
using SteelBIM.Services.Ifc;

namespace SteelBIM.Models.Ifc
{
    public class ElementoIfcViewModel : INotifyPropertyChanged
    {
        private readonly IReadOnlyList<SymbolItem> _allPerfis;
        private bool _selecionado = true;
        private string _familiaSelecionada;
        private SymbolItem _perfilSelecionado;
        private string _ifcMaterial;
        private string _secaoSugerida;

        public ElementoIfcViewModel(IReadOnlyList<SymbolItem> allPerfis)
        {
            _allPerfis = allPerfis ?? new List<SymbolItem>();
            Familias = _allPerfis
                .Select(s => s.Symbol.FamilyName)
                .Distinct()
                .OrderBy(f => f)
                .ToList();
            ParametrosIfc = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<SymbolItem> AllPerfis => _allPerfis;

        public ElementId ElementId { get; set; }
        public string IfcName { get; set; }
        public string Categoria { get; set; }

        /// <summary>Todos os parametros IFC encontrados no elemento (nome -> valor).</summary>
        public Dictionary<string, string> ParametrosIfc { get; }

        public string IfcMaterial
        {
            get => _ifcMaterial;
            set { _ifcMaterial = value; Notify(nameof(IfcMaterial)); }
        }

        public string SecaoSugerida
        {
            get => _secaoSugerida;
            set { _secaoSugerida = value; Notify(nameof(SecaoSugerida)); }
        }

        public bool Selecionado
        {
            get => _selecionado;
            set { _selecionado = value; Notify(nameof(Selecionado)); }
        }

        public IReadOnlyList<string> Familias { get; }

        public string FamiliaSelecionada
        {
            get => _familiaSelecionada;
            set
            {
                if (_familiaSelecionada == value)
                    return;
                _familiaSelecionada = value;
                Notify(nameof(FamiliaSelecionada));
                Notify(nameof(TiposDisponiveis));

                if (_perfilSelecionado?.Symbol?.FamilyName != value)
                {
                    _perfilSelecionado = null;
                    Notify(nameof(PerfilSelecionado));
                    Notify(nameof(PerfilNome));
                }
            }
        }

        public List<SymbolItem> TiposDisponiveis =>
            string.IsNullOrEmpty(_familiaSelecionada)
                ? new List<SymbolItem>(_allPerfis)
                : _allPerfis.Where(s => s.Symbol.FamilyName == _familiaSelecionada).ToList();

        public SymbolItem PerfilSelecionado
        {
            get => _perfilSelecionado;
            set
            {
                _perfilSelecionado = value;
                Notify(nameof(PerfilSelecionado));
                Notify(nameof(PerfilNome));

                if (value != null && _familiaSelecionada != value.Symbol.FamilyName)
                {
                    _familiaSelecionada = value.Symbol.FamilyName;
                    Notify(nameof(FamiliaSelecionada));
                    Notify(nameof(TiposDisponiveis));
                }
            }
        }

        public string PerfilNome => _perfilSelecionado?.Symbol?.Name ?? "(nenhum)";

        /// <summary>
        /// Atualiza IfcMaterial, SecaoSugerida e PerfilSelecionado usando o valor
        /// do parametro IFC indicado. Chamado quando o usuario muda o campo de extracao.
        /// </summary>
        public void RecalcularSugestao(string nomeParam)
        {
            string valor = ParametrosIfc.TryGetValue(nomeParam, out string v) ? v : string.Empty;
            IfcMaterial = valor;

            string secao = IfcMaterialParser.ExtrairNomeSecao(valor);
            SecaoSugerida = secao;

            SymbolItem melhor = null;
            // v2.7.0 BUG 3: 49 -> 59 (alinhado com ScoreMinimo 60 do command)
            int melhorScore = 59;
            foreach (SymbolItem si in _allPerfis)
            {
                int score = IfcMaterialParser.CalcularScore(secao, si.Symbol.Name);
                if (score > melhorScore)
                {
                    melhorScore = score;
                    melhor = si;
                }
            }

            PerfilSelecionado = melhor;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
