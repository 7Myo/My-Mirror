using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Threading.Tasks;

internal static class ReceiverSetup
{
    const string Bonjour = "Bonjour Service";
    const string InstallerUrl = "https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplaywindows-installer.msi";
    const string InstallerHash = "D499873A18403E7594CA5E780CC0833CFA58D6E3A794FA1D360F41BE3EB78077";

    public static bool NeedsSetup(string engine)
    {
        return !File.Exists(engine) || BonjourStatus() != ServiceControllerStatus.Running;
    }

    public static async Task Prepare(string engine, IProgress<string> progress)
    {
        if (!File.Exists(engine))
        {
            progress.Report("Téléchargement du moteur AirPlay…");
            string directory = Path.Combine(Path.GetTempPath(), "MyMirror-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string msi = Path.Combine(directory, "uxplaywindows-installer.msi");
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                    await client.DownloadFileTaskAsync(new Uri(InstallerUrl), msi).ConfigureAwait(false);
                using (var sha256 = SHA256.Create())
                using (var file = File.OpenRead(msi))
                {
                    string hash = BitConverter.ToString(sha256.ComputeHash(file)).Replace("-", "");
                    if (!string.Equals(hash, InstallerHash, StringComparison.Ordinal))
                        throw new InvalidDataException("Le téléchargement UxPlay ne correspond pas à la version attendue.");
                }
                progress.Report("Installation du moteur AirPlay…");
                RunElevated(Path.Combine(Environment.SystemDirectory, "msiexec.exe"), "/i \"" + msi + "\" /passive /norestart", true);
            }
            finally
            {
                try { File.Delete(msi); Directory.Delete(directory); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (!File.Exists(engine))
                throw new InvalidOperationException("L’installation n’a pas placé UxPlay dans le dossier attendu.");
        }

        if (BonjourStatus() == null)
        {
            progress.Report("Installation du service Bonjour…");
            string responder = Path.Combine(Path.GetDirectoryName(engine), "mDNSResponder.exe");
            if (!File.Exists(responder))
                throw new FileNotFoundException("Le moteur installé ne fournit pas Bonjour.");
            RunElevated(responder, "-install", false);
        }
        if (BonjourStatus() == ServiceControllerStatus.Stopped)
        {
            progress.Report("Démarrage du service Bonjour…");
            RunElevated(Path.Combine(Environment.SystemDirectory, "net.exe"), "start \"" + Bonjour + "\"", false);
        }
        using (var service = new ServiceController(Bonjour))
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
    }

    static ServiceControllerStatus? BonjourStatus()
    {
        using (var service = new ServiceController(Bonjour))
        {
            try { return service.Status; }
            catch (InvalidOperationException e)
            {
                var error = e.InnerException as Win32Exception;
                if (error != null && error.NativeErrorCode == 1060) return null;
                throw;
            }
        }
    }

    static void RunElevated(string executable, string arguments, bool installer)
    {
        var start = new ProcessStartInfo(executable, arguments) { UseShellExecute = true, Verb = "runas" };
        try
        {
            using (var process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("Impossible de lancer la préparation AirPlay.");
                process.WaitForExit();
                if (installer && (process.ExitCode == 3010 || process.ExitCode == 1641))
                    throw new InvalidOperationException("Redémarre Windows, puis relance My Mirror.");
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("La préparation AirPlay a échoué (code " + process.ExitCode + ").");
            }
        }
        catch (Win32Exception e)
        {
            if (e.NativeErrorCode == 1223)
                throw new InvalidOperationException("Installation annulée. Tu peux relancer la préparation AirPlay.");
            throw;
        }
    }
}
