using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;

[assembly: System.Reflection.AssemblyFileVersion(AppUpdate.Current)]

internal sealed class AppUpdate
{
    public const string Current = "1.3.1";
    const string Repo = "7Myo/My-Mirror";
    string url, digest;
    public string Tag;

    static WebClient Client()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var client = new WebClient();
        client.Headers[HttpRequestHeader.UserAgent] = "My-Mirror/" + Current;
        return client;
    }

    public static async Task<AppUpdate> Check()
    {
        using (var client = Client())
        {
            var json = await client.DownloadStringTaskAsync(new Uri("https://api.github.com/repos/" + Repo + "/releases/latest"));
            var release = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            string tag = release["tag_name"] as string;
            Version version;
            if (tag == null || !tag.StartsWith("v", StringComparison.Ordinal) ||
                !Version.TryParse(tag.Substring(1), out version) || version <= new Version(Current)) return null;
            foreach (Dictionary<string, object> asset in (System.Collections.IEnumerable)release["assets"])
            {
                if (!Equals(asset["name"], "MyMirror.exe")) continue;
                string url = "https://github.com/" + Repo + "/releases/download/" + tag + "/MyMirror.exe";
                object digest;
                if (Equals(asset["browser_download_url"], url) && asset.TryGetValue("digest", out digest) &&
                    digest is string && System.Text.RegularExpressions.Regex.IsMatch((string)digest, "^sha256:[a-fA-F0-9]{64}$"))
                    return new AppUpdate { Tag = tag, url = url, digest = ((string)digest).Substring(7) };
            }
            return null;
        }
    }

    public async Task<string> Download(string target)
    {
        string path = target + "." + Guid.NewGuid().ToString("N") + ".update";
        try
        {
            using (var client = Client()) await client.DownloadFileTaskAsync(new Uri(url), path);
            using (var hash = SHA256.Create())
            using (var file = File.OpenRead(path))
                if (!string.Equals(BitConverter.ToString(hash.ComputeHash(file)).Replace("-", ""), digest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("La mise à jour téléchargée ne correspond pas à la release.");
            return path;
        }
        catch { File.Delete(path); throw; }
    }

    public static void Schedule(string target, string update)
    {
        string backup = target + "." + Guid.NewGuid().ToString("N") + ".bak";
        Func<string, string> literal = value => "'" + value.Replace("'", "''") + "'";
        string script = "$ErrorActionPreference='Stop';" +
            "$old=Get-Process -Id " + Process.GetCurrentProcess().Id + " -ErrorAction SilentlyContinue;" +
            "if($old -and !$old.WaitForExit(30000)){Remove-Item -LiteralPath " + literal(update) + ";exit};" +
            "try{[IO.File]::Replace(" + literal(update) + "," + literal(target) + "," + literal(backup) + ");" +
            "try{Start-Process -FilePath " + literal(target) + "}catch{[IO.File]::Replace(" + literal(backup) + "," + literal(target) + ",$null);throw};" +
            "Remove-Item -LiteralPath " + literal(backup) + "}catch{Add-Type -AssemblyName System.Windows.Forms;[void][Windows.Forms.MessageBox]::Show($_.Exception.Message,'My Mirror')}";
        string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        using (var process = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            "-NoProfile -NonInteractive -EncodedCommand " + encoded) { UseShellExecute = false, CreateNoWindow = true }))
            if (process == null) throw new InvalidOperationException("Impossible de lancer la mise à jour.");
    }
}
