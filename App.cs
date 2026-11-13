using System;
using System.IO;
using System.Windows.Forms;

namespace PacoteFresco
{
    public static class App
    {
        public const string Nome = "Pacote Fresco";
        public const string Pasta = "PacoteFresco";

        static string pastaDados;

        public static string Versao
        {
            get { return Application.ProductVersion.Split('+')[0]; }
        }

        public static string PastaDados
        {
            get
            {
                if (pastaDados == null)
                {
                    string p = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Pasta);
                    try
                    {
                        Directory.CreateDirectory(p);
                        new DirectoryInfo(p).Attributes |= FileAttributes.Hidden;
                    }
                    catch { }
                    pastaDados = p;
                }
                return pastaDados;
            }
        }
    }
}
