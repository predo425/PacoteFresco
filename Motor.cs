using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PacoteFresco
{
    public class Motor
    {
        static readonly string[] Ext = { ".exe", ".msi", ".msix", ".msixbundle", ".appx" };
        static readonly string LogPath = Path.Combine(App.PastaDados, "log.txt");

        readonly Configuracao cfg;
        public bool Forcar;
        public event Action<string, string> Log;
        public event Action<int, int> Progresso;
        public event Action EstadoMudou;

        class Baixa { public string Arquivo; public string Versao; public bool NaoMudou; }
        class Proc { public int Codigo; public string Saida; }

        public Motor(Configuracao c)
        {
            cfg = c;
            try
            {
                var fi = new FileInfo(LogPath);
                if (fi.Exists && fi.Length > 1048576) { File.Copy(LogPath, LogPath + ".old", true); File.Delete(LogPath); }
            }
            catch { }
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        }

        void L(string nivel, string msg)
        {
            try
            {
                File.AppendAllText(LogPath, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") +
                    "  [" + nivel.ToUpper() + "] " + msg + Environment.NewLine);
            }
            catch { }
            var h = Log; if (h != null) h(nivel, msg);
        }

        void Mudou() { var h = EstadoMudou; if (h != null) h(); }

        public async Task ExecutarAsync(List<Programa> lista, CancellationToken ct)
        {
            int atualizados = 0, iguais = 0, erros = 0, n = 0;
            L("info", "Iniciando: " + lista.Count + " programa(s).");
            foreach (var p in lista)
            {
                if (ct.IsCancellationRequested) { L("aviso", "Cancelado pelo usuário."); break; }
                var pg = Progresso; if (pg != null) pg(n, lista.Count);
                p.Status = "Verificando..."; Mudou();
                try
                {
                    if (await ProcessarAsync(p, ct)) atualizados++; else iguais++;
                }
                catch (OperationCanceledException)
                {
                    p.Status = "Cancelado"; Mudou(); L("aviso", "Cancelado pelo usuário."); break;
                }
                catch (Exception ex)
                {
                    erros++; p.Status = "Erro"; L("erro", p.Nome + ": " + ex.Message);
                }
                n++; Mudou();
            }
            var pg2 = Progresso; if (pg2 != null) pg2(n, lista.Count);
            L("info", "Concluído: " + atualizados + " atualizado(s), " + iguais + " sem mudança, " + erros + " erro(s).");
        }

        async Task<bool> ProcessarAsync(Programa p, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(p.Alvo)) throw new Exception("o ID/link está vazio.");
            if (string.IsNullOrWhiteSpace(p.Nome)) p.Nome = "Programa";

            string pasta = !string.IsNullOrWhiteSpace(p.Destino) ? p.Destino
                : cfg.SubpastaPorPrograma ? Path.Combine(cfg.PastaPadrao, Limpar(p.Nome))
                : cfg.PastaPadrao;
            string atual = string.IsNullOrEmpty(p.UltimoArquivo) ? null : Path.Combine(pasta, p.UltimoArquivo);
            bool atualExiste = atual != null && File.Exists(atual);

            string tmp = Path.Combine(Path.GetTempPath(), App.Pasta, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                Baixa b = p.Tipo.Equals("winget", StringComparison.OrdinalIgnoreCase)
                    ? await BaixarWinget(p, tmp, atualExiste, ct)
                    : await BaixarLink(p, tmp, atualExiste, ct);

                if (b.NaoMudou)
                {
                    p.Status = "Sem mudança";
                    L("info", p.Nome + ": já está na versão mais recente.");
                    return false;
                }

                p.Status = "Verificando hash...";  Mudou();
                string hash = Sha256(b.Arquivo);
                if (!string.IsNullOrWhiteSpace(p.Sha256Esperado) &&
                    !hash.Equals(p.Sha256Esperado.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("HASH DIVERGENTE. Esperado " + p.Sha256Esperado.Trim() + ", obtido " + hash);

                string[] sig = await Assinatura(b.Arquivo);
                if (sig[0] != "Valid")
                    L("aviso", p.Nome + ": assinatura digital = " + sig[0] + " (confira a origem do arquivo).");

                if (atualExiste && Sha256(atual) == hash)
                {
                    p.UltimaVersao = b.Versao;
                    p.Status = "Sem mudança";
                    L("info", p.Nome + ": o arquivo baixado é idêntico ao que você já tem.");
                    return false;
                }

                Directory.CreateDirectory(pasta);
                string destino = Path.Combine(pasta, Path.GetFileName(b.Arquivo));
                File.Copy(b.Arquivo, destino, true);
                if (atualExiste && !string.Equals(atual, destino, StringComparison.OrdinalIgnoreCase))
                    File.Delete(atual);

                p.UltimoArquivo = Path.GetFileName(destino);
                p.UltimoHash = hash;
                p.UltimaVersao = b.Versao;
                p.UltimaAtualizacao = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                p.Status = "Atualizado";
                L("ok", p.Nome + ": atualizado -> " + p.UltimoArquivo);
                L("info", "     SHA256: " + hash);
                L("info", "     Assinatura: " + sig[0] + (sig[1] != "" ? " (" + sig[1] + ")" : ""));
                return true;
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { }
            }
        }

        async Task<Baixa> BaixarWinget(Programa p, string tmp, bool atualExiste, CancellationToken ct)
        {
            var rv = await Executar("winget", "show --id \"" + p.Alvo + "\" -e --accept-source-agreements", ct);
            string versao = "";
            var m = Regex.Match(rv.Saida, @"^(Version|Versão)\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (m.Success) versao = m.Groups[2].Value.Trim();
            string loc = (cfg.IdiomaWinget ?? "").Trim();
            string chave = versao == "" ? "" : versao + "|" + loc;
            if (!Forcar && atualExiste && chave != "" && chave == p.UltimaVersao)
                return new Baixa { NaoMudou = true };

            string baseArgs = "download --id \"" + p.Alvo + "\" -e --architecture x64 -d \"" + tmp +
                          "\" --accept-package-agreements --accept-source-agreements";
            if (cfg.ProxyModo == "Manual" && !string.IsNullOrWhiteSpace(cfg.ProxyEndereco))
                baseArgs += " --proxy " + cfg.ProxyEndereco.Trim();

            p.Status = "Baixando..."; Mudou();
            var r = await Executar("winget", loc != "" ? baseArgs + " --locale " + loc : baseArgs, ct);
            ct.ThrowIfCancellationRequested();
            if (r.Codigo != 0 && loc != "")
            {
                L("aviso", p.Nome + ": sem instalador em " + loc + ", usando o idioma padrão do pacote.");
                foreach (var f in Directory.GetFiles(tmp)) { try { File.Delete(f); } catch { } }
                r = await Executar("winget", baseArgs, ct);
                ct.ThrowIfCancellationRequested();
            }
            if (r.Codigo != 0) throw new Exception("winget falhou (código " + r.Codigo + "). " + Resumo(r.Saida));

            string arq = Directory.GetFiles(tmp)
                .Where(f => Ext.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            if (arq == null) throw new Exception("o winget não gerou um instalador para esse ID.");
            if (loc != "" && Path.GetFileName(arq).IndexOf(loc, StringComparison.OrdinalIgnoreCase) < 0)
                L("aviso", p.Nome + ": o pacote não tem instalador separado em " + loc + "; o idioma depende do próprio instalador.");
            string limpo = Regex.Replace(arq, @"_en-US(?=\.[^.\\]+$)", "");
            if (limpo != arq) { File.Move(arq, limpo); arq = limpo; }
            return new Baixa { Arquivo = arq, Versao = chave };
        }

        HttpClientHandler CriarHandler()
        {
            var h = new HttpClientHandler { AllowAutoRedirect = false };
            if (cfg.ProxyModo == "Nenhum") { h.UseProxy = false; return h; }
            IWebProxy px;
            if (cfg.ProxyModo == "Manual" && !string.IsNullOrWhiteSpace(cfg.ProxyEndereco))
                px = new WebProxy(cfg.ProxyEndereco.Trim(), true);
            else
                px = WebRequest.GetSystemWebProxy();
            px.Credentials = CredentialCache.DefaultCredentials;
            h.Proxy = px; h.UseProxy = true;
            return h;
        }

        async Task<Baixa> BaixarLink(Programa p, string tmp, bool atualExiste, CancellationToken ct)
        {
            using (var cli = new HttpClient(CriarHandler()) { Timeout = TimeSpan.FromMinutes(30) })
            {
                cli.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) " + App.Pasta + "/" + App.Versao);
                p.Status = "Conectando..."; Mudou();

                Uri uri = new Uri(p.Alvo);
                HttpResponseMessage resp = null;
                for (int i = 0; i < 10; i++)
                {
                    resp = await cli.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                    int cod = (int)resp.StatusCode;
                    if (cod >= 300 && cod < 400 && resp.Headers.Location != null)
                    {
                        uri = new Uri(uri, resp.Headers.Location);
                        resp.Dispose(); resp = null;
                        continue;
                    }
                    break;
                }
                if (resp == null) throw new Exception("redirecionamentos demais.");

                using (resp)
                {
                    if ((int)resp.StatusCode == 407)
                        throw new Exception("o proxy exigiu autenticação (407). Confira Configurações > Proxy.");
                    resp.EnsureSuccessStatusCode();

                    string tipo = resp.Content.Headers.ContentType != null ? resp.Content.Headers.ContentType.MediaType : "";
                    if (tipo == "text/html")
                        throw new Exception("o link devolveu uma página web (login/bloqueio?), não um instalador.");

                    string versao = (resp.Headers.ETag != null ? resp.Headers.ETag.Tag : "") + "|" +
                                    resp.Content.Headers.LastModified + "|" +
                                    resp.Content.Headers.ContentLength + "|" + uri;
                    if (!Forcar && atualExiste && versao == p.UltimaVersao)
                        return new Baixa { NaoMudou = true };

                    string nome = null;
                    var cd = resp.Content.Headers.ContentDisposition;
                    if (cd != null) nome = cd.FileNameStar ?? cd.FileName;
                    nome = (nome ?? "").Trim('"');
                    if (nome == "") nome = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
                    nome = Limpar(nome);
                    if (!Ext.Contains(Path.GetExtension(nome).ToLowerInvariant()))
                        nome = Limpar(p.Nome) + ".exe";

                    p.Status = "Baixando..."; Mudou();
                    string dest = Path.Combine(tmp, nome);
                    using (var rs = await resp.Content.ReadAsStreamAsync())
                    using (var fs = File.Create(dest))
                        await rs.CopyToAsync(fs, 81920, ct);
                    return new Baixa { Arquivo = dest, Versao = versao };
                }
            }
        }

        static async Task<Proc> Executar(string exe, string args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            using (var pr = new Process { StartInfo = psi })
            {
                var sb = new StringBuilder();
                DataReceivedEventHandler h = (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                pr.OutputDataReceived += h; pr.ErrorDataReceived += h;
                try { pr.Start(); }
                catch (Win32Exception)
                {
                    return new Proc { Codigo = -1, Saida = "não foi possível iniciar '" + exe + "' (instalado? bloqueado?)." };
                }
                pr.BeginOutputReadLine(); pr.BeginErrorReadLine();
                using (ct.Register(() => { try { pr.Kill(); } catch { } }))
                    await Task.Run(() => pr.WaitForExit());
                return new Proc { Codigo = pr.ExitCode, Saida = sb.ToString() };
            }
        }

        static async Task<string[]> Assinatura(string arq)
        {
            try
            {
                string cmd = "$s = Get-AuthenticodeSignature -LiteralPath '" + arq.Replace("'", "''") +
                             "'; $s.Status; if ($s.SignerCertificate) { $s.SignerCertificate.Subject }";
                var r = await Executar("powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + cmd + "\"", CancellationToken.None);
                var l = r.Saida.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (l.Length == 0) return new[] { "Desconhecido", "" };
                string cn = "";
                if (l.Length > 1)
                {
                    var m = Regex.Match(l[1], @"CN=([^,]+)");
                    cn = m.Success ? m.Groups[1].Value.Trim('"') : l[1].Trim();
                }
                return new[] { l[0].Trim(), cn };
            }
            catch { return new[] { "Desconhecido", "" }; }
        }

        static string Sha256(string f)
        {
            using (var s = File.OpenRead(f))
            using (var h = SHA256.Create())
                return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "");
        }

        static string Limpar(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Trim();
        }

        static string Resumo(string s)
        {
            var l = s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(x => x.Trim()).Where(x => x.Length > 3).ToArray();
            string t = string.Join(" / ", l.Skip(Math.Max(0, l.Length - 3)));
            return t.Length > 300 ? t.Substring(0, 300) : t;
        }
    }
}
