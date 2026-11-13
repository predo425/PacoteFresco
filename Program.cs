using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PacoteFresco
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            var cfg = Configuracao.Carregar();

            if (args.Any(a => a.Equals("/auto", StringComparison.OrdinalIgnoreCase)))
            {
                var motor = new Motor(cfg);
                motor.ExecutarAsync(cfg.Programas.Where(p => p.Ativo).ToList(), CancellationToken.None)
                     .GetAwaiter().GetResult();
                cfg.Salvar();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(cfg));
        }
    }
}
