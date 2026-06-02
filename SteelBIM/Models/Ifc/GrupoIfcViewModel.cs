#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Autodesk.Revit.DB;
using SteelBIM.Forms;

namespace SteelBIM.Models.Ifc
{
    public class GrupoIfcViewModel : INotifyPropertyChanged
    {
        private readonly IReadOnlyList<SymbolItem> _allPerfis;
        private bool _selecionado = true;
        private string? _familiaSelecionada;
        private SymbolItem? _perfilSelecionado;

        public GrupoIfcViewModel(IReadOnlyList<SymbolItem> allPerfis)
        {
            _allPerfis = allPerfis ?? new List<SymbolItem>();
            Familias = _allPerfis
                .Select(s => s.Symbol.FamilyName)
                .Distinct()
                .OrderBy(f => f)
                .ToList();
            ElementIds = new List<ElementId>();
        }

        public List<ElementId> ElementIds { get; }
        public int Quantidade => ElementIds.Count;
        public string? SecaoSugerida { get; set; }
        public string? ValorIfc { get; set; }

        public bool Selecionado
        {
            get => _selecionado;
            set { _selecionado = value; Notify(nameof(Selecionado)); }
        }

        public IReadOnlyList<string> Familias { get; }

        public string? FamiliaSelecionada
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

        public SymbolItem? PerfilSelecionado
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
