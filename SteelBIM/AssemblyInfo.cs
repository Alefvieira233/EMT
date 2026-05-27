using System.Reflection;
using System.Runtime.InteropServices;

// Metadados do assembly SteelBIM.
// <GenerateAssemblyInfo>false</GenerateAssemblyInfo> no csproj significa que o SDK
// nao cria esses atributos automaticamente — declaramos manualmente aqui
// para que o .dll saia com versao, titulo e copyright corretos em vez de 0.0.0.0.
[assembly: AssemblyTitle("SteelBIM")]
[assembly: AssemblyDescription("Plugin Revit 2025 SteelBIM — automacao de estruturas metalicas e pre-fabricado de concreto.")]
[assembly: AssemblyCompany("EMT")]
[assembly: AssemblyProduct("SteelBIM")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Alef Christian Gomes Vieira")]
[assembly: AssemblyTrademark("SteelBIM")]
[assembly: AssemblyCulture("")]

// Versao — manter sincronizado com CHANGELOG.md e o badge do README.
[assembly: AssemblyVersion("2.8.1.0")]
[assembly: AssemblyFileVersion("2.8.1.0")]
[assembly: AssemblyInformationalVersion("2.8.1")]

// ComVisible(false) para evitar expor todos os tipos via COM acidentalmente.
[assembly: ComVisible(false)]
