using System.Reflection;
using System.Runtime.InteropServices;

// Metadados do assembly FerramentaEMT.
// <GenerateAssemblyInfo>false</GenerateAssemblyInfo> no csproj significa que o SDK
// nao cria esses atributos automaticamente — declaramos manualmente aqui
// para que o .dll saia com versao, titulo e copyright corretos em vez de 0.0.0.0.
[assembly: AssemblyTitle("FerramentaEMT")]
[assembly: AssemblyDescription("Plugin Revit 2025 para automacao de estruturas metalicas e pre-fabricado de concreto.")]
[assembly: AssemblyCompany("EMT")]
[assembly: AssemblyProduct("FerramentaEMT")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Alef Christian Gomes Vieira")]
[assembly: AssemblyTrademark("FerramentaEMT")]
[assembly: AssemblyCulture("")]

// Versao — manter sincronizado com CHANGELOG.md e o badge do README.
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3.0")]

// ComVisible(false) para evitar expor todos os tipos via COM acidentalmente.
[assembly: ComVisible(false)]
