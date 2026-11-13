using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace PacoteFresco
{
    public class Programa
    {
        public bool Ativo { get; set; } = true;
        public string Nome { get; set; } = "";
        public string Tipo { get; set; } = "winget";
        public string Alvo { get; set; } = "";
        public string Destino { get; set; } = "";
        public string Sha256Esperado { get; set; } = "";

        public string UltimoArquivo { get; set; } = "";
        public string UltimoHash { get; set; } = "";
        public string UltimaVersao { get; set; } = "";
        public string UltimaAtualizacao { get; set; } = "";
        [XmlIgnore] public string Status { get; set; } = "";
    }

    public class Configuracao
    {
        public string PastaPadrao { get; set; } = @"C:\Instaladores";
        public bool SubpastaPorPrograma { get; set; } = false;
        public string IdiomaWinget { get; set; } = "";
        public string ProxyModo { get; set; } = "Sistema";
        public string ProxyEndereco { get; set; } = "";
        public string HoraAgendamento { get; set; } = "07:30";
        public List<Programa> Programas { get; set; } = new List<Programa>();

        static string Caminho
        {
            get { return Path.Combine(App.PastaDados, "config.xml"); }
        }

        public static Configuracao Carregar()
        {
            try
            {
                string antigo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.xml");
                if (!File.Exists(Caminho) && File.Exists(antigo)) File.Move(antigo, Caminho);
            }
            catch { }
            try
            {
                if (File.Exists(Caminho))
                    using (var s = File.OpenRead(Caminho))
                        return (Configuracao)new XmlSerializer(typeof(Configuracao)).Deserialize(s);
            }
            catch { }
            return Padrao();
        }

        public void Salvar()
        {
            try
            {
                using (var s = File.Create(Caminho))
                    new XmlSerializer(typeof(Configuracao)).Serialize(s, this);
            }
            catch { }
        }

        static Programa P(string nome, string tipo, string alvo)
        {
            return new Programa { Nome = nome, Tipo = tipo, Alvo = alvo };
        }

        static Configuracao Padrao()
        {
            var c = new Configuracao();
            c.Programas.AddRange(new[]
            {
                P("7-Zip", "winget", "7zip.7zip"),
                P("Acrobat Reader", "winget", "Adobe.Acrobat.Reader.64-bit"),
                P("FastCopy", "winget", "FastCopy.FastCopy"),
                P("HandBrake", "winget", "HandBrake.HandBrake"),
                P("K-Lite Codec Pack", "winget", "CodecGuide.K-LiteCodecPack.Standard"),
                P("PowerToys", "winget", "Microsoft.PowerToys"),
                P("TightVNC", "winget", "GlavSoft.TightVNC"),
                P("Chrome Enterprise", "Link", "https://dl.google.com/dl/chrome/install/googlechromestandaloneenterprise64.msi"),
                P("Firefox MSI", "Link", "https://download.mozilla.org/?product=firefox-msi-latest-ssl&os=win64&lang=pt-BR"),
                P("Teams (MSIX)", "Link", "https://go.microsoft.com/fwlink/?linkid=2196106"),
                P("Teams Bootstrapper", "Link", "https://go.microsoft.com/fwlink/?linkid=2243204"),
            });
            return c;
        }
    }
}
