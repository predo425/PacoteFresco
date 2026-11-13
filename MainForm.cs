using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PacoteFresco
{
    public class MainForm : Form
    {
        static readonly Color Azul = Color.FromArgb(20, 33, 61);
        static readonly Color Destaque = Color.FromArgb(37, 99, 235);
        static readonly Color Fundo = Color.FromArgb(245, 247, 250);

        readonly Configuracao cfg;
        readonly BindingList<Programa> dados;
        DataGridView grid;
        RichTextBox log;
        ProgressBar barra;
        CheckBox chkForcar;
        Button btnAtualizar, btnLinha, btnCancelar, btnAdd, btnRem, btnCfg, btnAbrir;
        CancellationTokenSource cts;

        public MainForm(Configuracao c)
        {
            cfg = c;
            dados = new BindingList<Programa>(cfg.Programas);
            Text = App.Nome + " v" + App.Versao;
            ClientSize = new Size(1120, 720);
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Fundo;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            MontarGrade();
            var rodape = MontarRodape();
            var ferramentas = MontarFerramentas();
            var cab = MontarCabecalho();

            Controls.Add(grid); Controls.Add(rodape); Controls.Add(ferramentas); Controls.Add(cab);

            FormClosing += (s, e) => { grid.EndEdit(); cfg.Salvar(); };
            AddLog("info", "Pronto. Marque os programas e clique em \"Atualizar marcados\".");
        }

        Control MontarCabecalho()
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Azul };
            var fonteTitulo = new Font("Segoe UI Semibold", 17f);
            p.Controls.Add(new Label
            {
                Text = App.Nome, ForeColor = Color.White, AutoSize = true,
                Font = fonteTitulo, Left = 20, Top = 8
            });
            int w = TextRenderer.MeasureText(App.Nome, fonteTitulo).Width;
            p.Controls.Add(new Label
            {
                Text = "v" + App.Versao, ForeColor = Color.FromArgb(120, 200, 255), AutoSize = true,
                Left = 20 + w + 6, Top = 22
            });
            p.Controls.Add(new Label
            {
                Text = "Instaladores sempre fresquinhos: baixa as versões mais novas, confere hash e assinatura e prepara a pasta para o pen drive.",
                ForeColor = Color.FromArgb(180, 192, 214), AutoSize = true, Left = 22, Top = 46
            });
            return p;
        }

        Button Btn(string texto, bool destaque)
        {
            var b = new Button
            {
                Text = texto, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 34), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                BackColor = destaque ? Destaque : Color.White,
                ForeColor = destaque ? Color.White : Color.FromArgb(30, 41, 59),
                Margin = new Padding(0, 0, 8, 0), Padding = new Padding(8, 0, 8, 0)
            };
            b.FlatAppearance.BorderColor = destaque ? Destaque : Color.FromArgb(203, 213, 225);
            return b;
        }

        Control MontarFerramentas()
        {
            var f = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 54, Padding = new Padding(12, 10, 12, 8),
                BackColor = Color.White, WrapContents = false
            };
            btnAdd = Btn("+ Adicionar", false);
            btnRem = Btn("Remover", false);
            btnAtualizar = Btn("Atualizar marcados", true);
            btnLinha = Btn("Só a linha atual", false);
            btnCancelar = Btn("Cancelar", false); btnCancelar.Enabled = false;
            chkForcar = new CheckBox { Text = "Forçar novo download", AutoSize = true, Margin = new Padding(8, 7, 16, 0) };
            btnCfg = Btn("Configurações", false);
            btnAbrir = Btn("Abrir pasta", false);
            f.Controls.AddRange(new Control[] { btnAdd, btnRem, btnAtualizar, btnLinha, btnCancelar, chkForcar, btnCfg, btnAbrir });

            btnAdd.Click += (s, e) =>
            {
                dados.Add(new Programa { Nome = "Novo programa" });
                grid.CurrentCell = grid.Rows[grid.RowCount - 1].Cells[1];
                grid.BeginEdit(true);
            };
            btnRem.Click += (s, e) =>
            {
                if (grid.CurrentRow == null) return;
                var p = (Programa)grid.CurrentRow.DataBoundItem;
                if (MessageBox.Show(this, "Remover \"" + p.Nome + "\" da lista?\n(Os arquivos já baixados não são apagados.)",
                    "Remover", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    dados.Remove(p);
            };
            btnAtualizar.Click += async (s, e) => await RodarAsync(dados.Where(p => p.Ativo).ToList());
            btnLinha.Click += async (s, e) =>
            {
                var p = grid.CurrentRow != null ? grid.CurrentRow.DataBoundItem as Programa : null;
                if (p != null) await RodarAsync(new System.Collections.Generic.List<Programa> { p });
            };
            btnCancelar.Click += (s, e) => { if (cts != null) cts.Cancel(); };
            btnCfg.Click += (s, e) =>
            {
                using (var d = new ConfigForm(cfg))
                    if (d.ShowDialog(this) == DialogResult.OK) cfg.Salvar();
            };
            btnAbrir.Click += (s, e) =>
            {
                try { Directory.CreateDirectory(cfg.PastaPadrao); Process.Start("explorer.exe", cfg.PastaPadrao); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message); }
            };
            return f;
        }

        void MontarGrade()
        {
            grid = new DataGridView
            {
                Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 36, GridColor = Color.FromArgb(226, 232, 240),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };
            grid.RowTemplate.Height = 30;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Azul;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Azul;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

            grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Ativo", HeaderText = "Atualizar", Width = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Nome", HeaderText = "Programa", Width = 160 });
            var tipo = new DataGridViewComboBoxColumn { DataPropertyName = "Tipo", HeaderText = "Origem", Width = 80, FlatStyle = FlatStyle.Flat };
            tipo.Items.AddRange("winget", "Link");
            grid.Columns.Add(tipo);
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Alvo", HeaderText = "ID do winget ou link de download",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Destino", HeaderText = "Pasta de destino (vazio = padrão)", Width = 230 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "colPasta", HeaderText = "", Text = "...", UseColumnTextForButtonValue = true, Width = 34, FlatStyle = FlatStyle.Flat });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UltimoArquivo", HeaderText = "Arquivo atual", Width = 210, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UltimaAtualizacao", HeaderText = "Atualizado em", Width = 120, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Status", Width = 120, ReadOnly = true });
            grid.DataSource = dados;

            grid.DataError += (s, e) => { e.ThrowException = false; };
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != "colPasta") return;
                using (var d = new FolderBrowserDialog { Description = "Pasta de destino para este programa" })
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        ((Programa)grid.Rows[e.RowIndex].DataBoundItem).Destino = d.SelectedPath;
                        grid.InvalidateRow(e.RowIndex);
                    }
            };
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].DataPropertyName != "Status") return;
                string v = e.Value as string ?? "";
                e.CellStyle.ForeColor = v.StartsWith("Erro") ? Color.Firebrick
                                      : v == "Atualizado" ? Color.SeaGreen : Color.DimGray;
            };
        }

        Control MontarRodape()
        {
            var p = new Panel { Dock = DockStyle.Bottom, Height = 210 };
            log = new RichTextBox
            {
                Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.FromArgb(203, 213, 225),
                Font = new Font("Consolas", 9.5f)
            };
            barra = new ProgressBar { Dock = DockStyle.Top, Height = 8, Style = ProgressBarStyle.Continuous };
            p.Controls.Add(log); p.Controls.Add(barra);
            return p;
        }

        void Ui(Action a) { if (InvokeRequired) BeginInvoke(a); else a(); }

        void AddLog(string nivel, string msg)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => AddLog(nivel, msg))); return; }
            Color c = nivel == "ok" ? Color.FromArgb(74, 222, 128)
                    : nivel == "erro" ? Color.FromArgb(248, 113, 113)
                    : nivel == "aviso" ? Color.FromArgb(251, 191, 36)
                    : Color.FromArgb(203, 213, 225);
            log.SelectionStart = log.TextLength; log.SelectionLength = 0; log.SelectionColor = c;
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + "\n");
            log.ScrollToCaret();
        }

        void Ocupado(bool b)
        {
            btnAtualizar.Enabled = btnLinha.Enabled = btnAdd.Enabled = btnRem.Enabled = btnCfg.Enabled = !b;
            btnCancelar.Enabled = b;
            grid.Enabled = !b;
        }

        async Task RodarAsync(System.Collections.Generic.List<Programa> lista)
        {
            grid.EndEdit();
            if (lista.Count == 0)
            {
                MessageBox.Show(this, "Nenhum programa marcado.", App.Nome);
                return;
            }
            Ocupado(true);
            cts = new CancellationTokenSource();
            var motor = new Motor(cfg) { Forcar = chkForcar.Checked };
            motor.Log += AddLog;
            motor.Progresso += (a, t) => Ui(() => { barra.Maximum = Math.Max(t, 1); barra.Value = Math.Min(a, barra.Maximum); });
            motor.EstadoMudou += () => Ui(() => grid.Invalidate());
            try { await motor.ExecutarAsync(lista, cts.Token); }
            catch (Exception ex) { AddLog("erro", ex.Message); }
            finally
            {
                cfg.Salvar();
                Ocupado(false);
                grid.Invalidate();
            }
        }
    }
}
