using System.Runtime.Versioning;

// Marca o assembly inteiro como "Windows only", o que faz sentido para um
// plugin Revit (.NET 8.0-windows) que usa DPAPI, Registry e WPF.
// Isso silencia o CA1416 sem precisar anotar classe-por-classe.
[assembly: SupportedOSPlatform("windows")]
