using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SteelBIM.Models.DiagramaMontagem;
using SteelBIM.Utils;

namespace SteelBIM.Views
{
    public partial class DiagramaMontagemWindow : Window
    {
        private readonly UIDocument _uidoc;
        private readonly List<ElementId> _ids;

        public DiagramaMontagemWindow(UIDocument uidoc, List<ElementId> ids)
        {
            InitializeComponent();
            RevitWindowThemeService.Attach(this);
            _uidoc = uidoc;
            _ids = ids ?? new List<ElementId>();
            txtResumo.Text = $"{_ids.Count} elemento(s) pre-selecionado(s) para o diagrama.";
        }

        public DiagramaMontagemConfig BuildConfig()
        {
            var c = new DiagramaMontagemConfig();

            // Orientacao (existente)
            if (rbX.IsChecked == true)
                c.Orientacao = OrientacaoDiagrama.ParaleloEixoX;
            else if (rbY.IsChecked == true)
                c.Orientacao = OrientacaoDiagrama.ParaleloEixoY;
            else
                c.Orientacao = OrientacaoDiagrama.Auto;

            // Conteudo basico (existente)
            c.MostrarEixos = chkEixos.IsChecked == true;
            c.AdicionarCotasEntreEixos = chkCotas.IsChecked == true;
            c.AdicionarTagsMarca = chkTags.IsChecked == true;

            // Cotagem adicional (novo v2.4.0)
            c.AdicionarCotasVerticais = chkCotasVerticais.IsChecked == true;
            c.AdicionarCotaTotalConjunto = chkCotaTotal.IsChecked == true;
            c.MostrarSimboloDeNivel = chkMostrarNiveis.IsChecked == true;
            c.AdicionarComprimentosIndividuais = chkComprimentos.IsChecked == true;

            // Folha (novo v2.4.0)
            c.ColocarEmFolha = chkFolha.IsChecked == true;
            c.NumeroFolha = txtNumeroFolha.Text?.Trim() ?? "EM-XX";
            c.NomeFolha = txtNomeFolha.Text?.Trim() ?? "";

            return c;
        }

        private void BtnGerar_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
