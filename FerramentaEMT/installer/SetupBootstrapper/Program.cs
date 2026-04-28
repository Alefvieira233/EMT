using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace FerramentaEMT.SetupBootstrapper;

internal static class Program
{
    private const string ResourceName = "FerramentaEMT.SetupBootstrapper.Package.zip";

    [STAThread]
    private static int Main(string[] args)
    {
        bool quiet = args.Any(IsQuietArgument);

        try
        {
            using InstallerSession session = new InstallerSession(quiet);
            session.Run();
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 2;
        }
        catch (Exception ex)
        {
            ReportError(quiet, ex);
            return 1;
        }
    }

    private static bool IsQuietArgument(string arg) =>
        string.Equals(arg, "/quiet", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "--quiet", StringComparison.OrdinalIgnoreCase);

    private static void ReportError(bool quiet, Exception ex)
    {
        string mensagem = ex.Message;
        if (!quiet)
        {
            MessageBox.Show(
                mensagem,
                "FerramentaEMT Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        try
        {
            Console.Error.WriteLine(mensagem);
        }
        catch
        {
        }
    }

    private sealed class InstallerSession : IDisposable
    {
        private readonly bool _quiet;
        private readonly string _tempRoot;

        public InstallerSession(bool quiet)
        {
            _quiet = quiet;
            _tempRoot = Path.Combine(Path.GetTempPath(), "FerramentaEMT-Setup-" + Guid.NewGuid().ToString("N"));
        }

        public void Run()
        {
            if (!_quiet)
            {
                DialogResult resposta = MessageBox.Show(
                    "Deseja instalar o add-in FerramentaEMT para o Revit?",
                    "FerramentaEMT Setup",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question);

                if (resposta != DialogResult.OK)
                    throw new OperationCanceledException();
            }

            // PR-6 (P0.5 da auditoria): EULA prompt antes da instalacao.
            // Atualmente DESABILITADO (ShowEulaPrompt = false em
            // EulaConfirmationForm.cs) porque os documentos legais
            // (EULA.draft.md, PRIVACY.draft.md, TOS.draft.md) ainda estao
            // em revisao juridica. Quando advogado aprovar, basta flipar
            // a const ShowEulaPrompt para true.
            // Em modo quiet (instalacao silenciosa por script corporativo),
            // RequestAcceptance retorna true automaticamente — caller eh
            // responsavel pelo aceite legal.
            if (!EulaConfirmation.RequestAcceptance(_quiet))
            {
                throw new OperationCanceledException();
            }

            ExtractPackageToTemp();

            PackageMetadata metadata = LoadMetadata();
            string revitYear = GetOverride("FERRAMENTAEMT_REVITYEAR") ?? metadata.RevitYear;
            string addinsRoot = ResolveAddinsRoot(revitYear);
            string installRoot = ResolveInstallRoot(addinsRoot, metadata.InstallFolderName);
            string manifestName = GetOverride("FERRAMENTAEMT_MANIFESTNAME") ?? metadata.ManifestFileName;
            string manifestPath = Path.Combine(addinsRoot, manifestName);
            string payloadRoot = Path.Combine(_tempRoot, "payload", metadata.InstallFolderName);

            if (!Directory.Exists(payloadRoot))
                throw new InvalidOperationException($"Pasta payload nao encontrada: {payloadRoot}");

            EnsureSafeChildPath(installRoot, addinsRoot);

            Directory.CreateDirectory(addinsRoot);
            ResetDirectory(installRoot);
            CopyDirectory(payloadRoot, installRoot);

            string assemblyPath = Path.Combine(installRoot, metadata.AssemblyFile);
            if (!File.Exists(assemblyPath))
                throw new InvalidOperationException($"Assembly principal nao encontrado apos a copia: {assemblyPath}");

            File.WriteAllText(manifestPath, BuildManifest(metadata, assemblyPath), new UTF8Encoding(false));

            List<string> duplicados = FindDuplicateManifests(addinsRoot, manifestPath, metadata.AddInId);
            ReportSuccess(installRoot, manifestPath, duplicados);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempRoot))
                    Directory.Delete(_tempRoot, true);
            }
            catch
            {
            }
        }

        private void ExtractPackageToTemp()
        {
            Directory.CreateDirectory(_tempRoot);

            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream is null)
                throw new InvalidOperationException("Pacote embutido nao encontrado no setup.exe.");

            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
            archive.ExtractToDirectory(_tempRoot, true);
        }

        private PackageMetadata LoadMetadata()
        {
            string metadataPath = Path.Combine(_tempRoot, "package-metadata.json");
            if (!File.Exists(metadataPath))
                throw new InvalidOperationException($"Arquivo package-metadata.json nao encontrado em {metadataPath}");

            PackageMetadata? metadata = JsonSerializer.Deserialize<PackageMetadata>(
                File.ReadAllText(metadataPath),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            if (metadata is null)
                throw new InvalidOperationException("Nao foi possivel ler o package-metadata.json.");

            metadata.Validate();
            return metadata;
        }

        private string ResolveAddinsRoot(string revitYear)
        {
            string? overridePath = GetOverride("FERRAMENTAEMT_ADDINSROOT");
            if (!string.IsNullOrWhiteSpace(overridePath))
                return Path.GetFullPath(overridePath);

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Autodesk", "Revit", "Addins", revitYear);
        }

        private string ResolveInstallRoot(string addinsRoot, string defaultFolderName)
        {
            string? overridePath = GetOverride("FERRAMENTAEMT_INSTALLROOT");
            if (!string.IsNullOrWhiteSpace(overridePath))
                return Path.GetFullPath(overridePath);

            return Path.Combine(addinsRoot, defaultFolderName);
        }

        private static string? GetOverride(string envVar)
        {
            string? value = Environment.GetEnvironmentVariable(envVar);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static void EnsureSafeChildPath(string childPath, string rootPath)
        {
            string childFull = Path.GetFullPath(childPath).TrimEnd(Path.DirectorySeparatorChar);
            string rootFull = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);

            if (!childFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Destino fora da raiz permitida: {childFull}");
        }

        private static void ResetDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            Directory.CreateDirectory(path);
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, directory);
                Directory.CreateDirectory(Path.Combine(destDir, relative));
            }

            foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceDir, filePath);
                string destination = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(filePath, destination, true);
            }
        }

        private static string BuildManifest(PackageMetadata metadata, string assemblyPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>");
            sb.AppendLine("<RevitAddIns>");
            sb.AppendLine($"    <AddIn Type=\"{metadata.AddInType}\">");
            sb.AppendLine($"        <Name>{metadata.Name}</Name>");
            sb.AppendLine($"        <Assembly>{assemblyPath}</Assembly>");
            sb.AppendLine($"        <AddInId>{metadata.AddInId}</AddInId>");
            sb.AppendLine($"        <FullClassName>{metadata.FullClassName}</FullClassName>");
            sb.AppendLine($"        <VendorId>{metadata.VendorId}</VendorId>");
            sb.AppendLine($"        <VendorDescription>{metadata.VendorDescription}</VendorDescription>");
            sb.AppendLine("    </AddIn>");
            sb.AppendLine("</RevitAddIns>");
            return sb.ToString();
        }

        private static List<string> FindDuplicateManifests(string addinsRoot, string manifestPath, string addInId)
        {
            List<string> duplicados = new List<string>();

            if (!Directory.Exists(addinsRoot))
                return duplicados;

            foreach (string arquivo in Directory.GetFiles(addinsRoot, "*.addin", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFullPath(arquivo), Path.GetFullPath(manifestPath), StringComparison.OrdinalIgnoreCase))
                    continue;

                string conteudo = File.ReadAllText(arquivo);
                if (conteudo.IndexOf(addInId, StringComparison.OrdinalIgnoreCase) >= 0)
                    duplicados.Add(arquivo);
            }

            return duplicados;
        }

        private void ReportSuccess(string installRoot, string manifestPath, List<string> duplicados)
        {
            if (_quiet)
                return;

            StringBuilder mensagem = new StringBuilder();
            mensagem.AppendLine("Instalacao concluida.");
            mensagem.AppendLine();
            mensagem.AppendLine($"Arquivos copiados para: {installRoot}");
            mensagem.AppendLine($"Manifesto criado em:   {manifestPath}");

            if (duplicados.Count > 0)
            {
                mensagem.AppendLine();
                mensagem.AppendLine("Foram encontrados outros manifestos com o mesmo AddInId:");
                foreach (string duplicado in duplicados)
                    mensagem.AppendLine($"- {duplicado}");
            }

            MessageBox.Show(
                mensagem.ToString(),
                "FerramentaEMT Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private sealed class PackageMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string AddInType { get; set; } = string.Empty;
        public string AddInId { get; set; } = string.Empty;
        public string FullClassName { get; set; } = string.Empty;
        public string VendorId { get; set; } = string.Empty;
        public string VendorDescription { get; set; } = string.Empty;
        public string AssemblyFile { get; set; } = string.Empty;
        public string InstallFolderName { get; set; } = string.Empty;
        public string ManifestFileName { get; set; } = string.Empty;
        public string RevitYear { get; set; } = string.Empty;

        public void Validate()
        {
            string[] obrigatorios =
            {
                Name,
                AddInType,
                AddInId,
                FullClassName,
                VendorId,
                VendorDescription,
                AssemblyFile,
                InstallFolderName,
                ManifestFileName,
                RevitYear
            };

            if (obrigatorios.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("package-metadata.json esta incompleto.");
        }
    }
}
