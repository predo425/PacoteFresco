using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace PacoteFresco
{
    public static class Agendador
    {
        const string Tarefa = App.Pasta;
        const string TarefaAntiga = "AtualizadorInstaladores";

        public static string Criar(string hora)
        {
            Rodar("/Delete /TN \"" + TarefaAntiga + "\" /F");
            string exe = Application.ExecutablePath;
            return Rodar("/Create /TN \"" + Tarefa + "\" /TR \"\\\"" + exe + "\\\" /auto\" /SC DAILY /ST " + hora + " /F");
        }

        public static string Remover()
        {
            Rodar("/Delete /TN \"" + TarefaAntiga + "\" /F");
            return Rodar("/Delete /TN \"" + Tarefa + "\" /F");
        }

        static string Rodar(string args)
        {
            var psi = new ProcessStartInfo("schtasks", args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                return (p.ExitCode == 0 ? "OK: " : "ERRO: ") + o.Trim();
            }
        }
    }

    public class ConfigForm : Form
    {
        const AnchorStyles TopoLados = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        readonly Configuracao cfg;
        TextBox txtPasta, txtProxy, txtIdioma;
        CheckBox chkSub;
        RadioButton rbSis, rbMan, rbNenhum;
        DateTimePicker hora;

        public ConfigForm(Configuracao c)
        {
            cfg = c;
            Text = "Configurações";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 504);
            MinimumSize = new Size(600, 540);
            Font = new Font("Segoe UI", 9.5f);

            Controls.Add(new Label { Text = "Pasta padrão dos instaladores", Left = 16, Top = 14, AutoSize = true });
            txtPasta = new TextBox { Left = 16, Top = 38, Width = 544, Text = cfg.PastaPadrao, Anchor = TopoLados };
            var btnPasta = new Button { Text = "...", Left = 568, Top = 36, Width = 56, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnPasta.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog())
                    if (d.ShowDialog(this) == DialogResult.OK) txtPasta.Text = d.SelectedPath;
            };
            Controls.Add(txtPasta); Controls.Add(btnPasta);

            chkSub = new CheckBox { Text = "Criar uma subpasta para cada programa", Left = 16, Top = 72, AutoSize = true, Checked = cfg.SubpastaPorPrograma };
            Controls.Add(chkSub);

            Controls.Add(new Label { Text = "Idioma preferido no winget (opcional):", Left = 16, Top = 106, AutoSize = true });
            txtIdioma = new TextBox { Left = 290, Top = 102, Width = 90, Text = cfg.IdiomaWinget };
            Controls.Add(txtIdioma);
            Controls.Add(new Label
            {
                Text = "Exemplo: pt-BR. Só vale para pacotes que têm um instalador por idioma (poucos). Vazio usa o padrão do pacote.",
                Left = 16, Top = 132, Width = 608, Height = 38, ForeColor = Color.DimGray, Anchor = TopoLados
            });

            var gp = new GroupBox { Text = "Proxy", Left = 16, Top = 178, Width = 608, Height = 156, Anchor = TopoLados };
            rbSis = new RadioButton { Text = "Usar o proxy do Windows (recomendado)", Left = 14, Top = 26, AutoSize = true };
            rbMan = new RadioButton { Text = "Informar manualmente:", Left = 14, Top = 56, AutoSize = true };
            txtProxy = new TextBox { Left = 210, Top = 53, Width = 380, Text = cfg.ProxyEndereco, Anchor = TopoLados };
            rbNenhum = new RadioButton { Text = "Sem proxy", Left = 14, Top = 86, AutoSize = true };
            var dica = new Label
            {
                Text = "O login do Windows é usado automaticamente na autenticação do proxy.",
                Left = 14, Top = 114, Width = 580, Height = 36, ForeColor = Color.DimGray, Anchor = TopoLados
            };
            gp.Controls.AddRange(new Control[] { rbSis, rbMan, txtProxy, rbNenhum, dica });
            Controls.Add(gp);
            if (cfg.ProxyModo == "Manual") rbMan.Checked = true;
            else if (cfg.ProxyModo == "Nenhum") rbNenhum.Checked = true;
            else rbSis.Checked = true;

            var ga = new GroupBox { Text = "Agendamento diário", Left = 16, Top = 344, Width = 608, Height = 108, Anchor = TopoLados };
            ga.Controls.Add(new Label { Text = "Horário:", Left = 14, Top = 33, AutoSize = true });
            hora = new DateTimePicker
            {
                Left = 78, Top = 29, Width = 80, Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm", ShowUpDown = true
            };
            TimeSpan ts;
            if (!TimeSpan.TryParse(cfg.HoraAgendamento, out ts)) ts = new TimeSpan(7, 30, 0);
            hora.Value = DateTime.Today.Add(ts);
            var btnAg = new Button { Text = "Agendar", Left = 174, Top = 26, Width = 100, Height = 30 };
            var btnDes = new Button { Text = "Remover agendamento", Left = 284, Top = 26, Width = 180, Height = 30 };
            btnAg.Click += (s, e) =>
            {
                cfg.HoraAgendamento = hora.Value.ToString("HH:mm");
                cfg.Salvar();
                MessageBox.Show(this, Agendador.Criar(cfg.HoraAgendamento), "Agendamento");
            };
            btnDes.Click += (s, e) => MessageBox.Show(this, Agendador.Remover(), "Agendamento");
            ga.Controls.Add(hora); ga.Controls.Add(btnAg); ga.Controls.Add(btnDes);
            ga.Controls.Add(new Label
            {
                Text = "Roda em segundo plano, só os programas marcados. Mantenha o .exe numa pasta fixa.",
                Left = 14, Top = 68, Width = 580, Height = 36, ForeColor = Color.DimGray, Anchor = TopoLados
            });
            Controls.Add(ga);

            var btnDados = new Button
            {
                Text = "Abrir pasta de dados", Left = 16, Top = 462, Width = 170, Height = 30,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnDados.Click += (s, e) => Process.Start("explorer.exe", App.PastaDados);
            var ok = new Button
            {
                Text = "Salvar", Left = 450, Top = 462, Width = 84, Height = 30,
                DialogResult = DialogResult.OK, Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            var cancel = new Button
            {
                Text = "Cancelar", Left = 540, Top = 462, Width = 84, Height = 30,
                DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            ok.Click += (s, e) =>
            {
                cfg.PastaPadrao = txtPasta.Text.Trim();
                cfg.SubpastaPorPrograma = chkSub.Checked;
                cfg.IdiomaWinget = txtIdioma.Text.Trim();
                cfg.ProxyEndereco = txtProxy.Text.Trim();
                cfg.ProxyModo = rbMan.Checked ? "Manual" : rbNenhum.Checked ? "Nenhum" : "Sistema";
                cfg.HoraAgendamento = hora.Value.ToString("HH:mm");
            };
            Controls.Add(btnDados); Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }
    }
}
