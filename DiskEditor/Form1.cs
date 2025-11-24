using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;

namespace DiskEditor
{
    public partial class Form1 : Form
    {
        //definição que cada linha vai mostrar 16 bytes e cada setor 512 bytes(padrão)
        const int BYTES_PER_ROW = 16;
        const int SECTOR_SIZE = 512;

        byte[] data = Array.Empty<byte>(); // armazena os 512 bites do setor atual
        string? currentPath = null; //guarda o caminho do disco atual
        long currentOffset = 0; // deslocamento do disco
        int currentSector = 0; // número do setor atual
        DriveInfo? selectedDrive = null; //Drive escolhido

        // Parte visual, botões e barra de busca
        DataGridView grid = new DataGridView();
        StatusStrip status = new StatusStrip();
        ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();

        TextBox gotoBox = new TextBox();
        Button gotoBtn = new Button();
        TextBox findBox = new TextBox();
        Button findBtn = new Button();

        Button prevSectorBtn = new Button();
        Button nextSectorBtn = new Button();

        // Referência aos botões para manipulação do disco
        Button openBtn = new Button();
        Button saveBtn = new Button();
        Button closeBtn = new Button();

        public Form1()
        {
            InitializeComponent();
            BuildUI();
        }


        //Build da interface
        private void BuildUI()
        {
            this.Text = "Disk Editor";
            this.Width = 1100;
            this.Height = 700;

            // Painel de botões (Topo)
            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };

            openBtn.Text = "Open"; openBtn.AutoSize = true;
            saveBtn.Text = "Save"; saveBtn.AutoSize = true; saveBtn.Enabled = false;
            closeBtn.Text = "Close"; closeBtn.AutoSize = true;

            openBtn.Click += OpenBtn_Click;
            saveBtn.Click += SaveBtn_Click;
            closeBtn.Click += CloseBtn_Click;

            topPanel.Controls.Add(openBtn);
            topPanel.Controls.Add(saveBtn);
            topPanel.Controls.Add(closeBtn);

            // Botões de navegação
            prevSectorBtn.Text = "<< Sector"; prevSectorBtn.AutoSize = true; prevSectorBtn.Enabled = false;
            prevSectorBtn.Click += PrevSectorBtn_Click;

            nextSectorBtn.Text = "Sector >>"; nextSectorBtn.AutoSize = true; nextSectorBtn.Enabled = false;
            nextSectorBtn.Click += NextSectorBtn_Click;

            topPanel.Controls.Add(prevSectorBtn);
            topPanel.Controls.Add(nextSectorBtn);

            topPanel.Controls.Add(new Label { Text = "Offset (hex ou dec):", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            gotoBox.Width = 120;
            gotoBtn.Text = "Go"; gotoBtn.AutoSize = true;
            gotoBtn.Click += GotoBtn_Click;
            topPanel.Controls.Add(gotoBox);
            topPanel.Controls.Add(gotoBtn);

            topPanel.Controls.Add(new Label { Text = "Buscar (ex: DEADBEEF):", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            findBox.Width = 140;
            findBtn.Text = "Find"; findBtn.AutoSize = true;
            findBtn.Click += FindBtn_Click;
            topPanel.Controls.Add(findBox);
            topPanel.Controls.Add(findBtn);

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 25,
                BackColor = Color.LightGray,
                Padding = new Padding(0)
            };

            headerPanel.Controls.Add(new Label
            {
                Text = "Offset",
                Width = 120,
                Left = 0,
                TextAlign = ContentAlignment.MiddleLeft
            });

            for (int i = 0; i < BYTES_PER_ROW; i++)
            {
                headerPanel.Controls.Add(new Label
                {
                    Text = i.ToString("X2"),
                    Width = 40,
                    Left = 120 + (i * 40),
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }

            headerPanel.Controls.Add(new Label
            {
                Text = "ASCII",
                Width = 250,
                Left = 120 + (BYTES_PER_ROW * 40),
                TextAlign = ContentAlignment.MiddleLeft
            });

            // Grid de dados
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.DefaultCellStyle.Font = new Font("Consolas", 10);
            grid.ColumnHeadersVisible = false;
            // Configurar colunas de dados hexadecimais para serem editáveis
            for (int i = 1; i <= BYTES_PER_ROW; i++)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "H" + (i - 1), HeaderText = (i - 1).ToString("X2"), Width = 40, ReadOnly = false });
            }
            grid.Columns.Insert(0, new DataGridViewTextBoxColumn { Name = "Offset", HeaderText = "Offset", ReadOnly = true, Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ASCII", HeaderText = "ASCII", ReadOnly = true, Width = 250 });

            grid.CellEndEdit += Grid_CellEndEdit;
            grid.CellDoubleClick += Grid_CellDoubleClick;

            status.Items.Add(statusLabel);
            statusLabel.Text = "Nenhum disco carregado";

            this.Controls.Add(grid);
            this.Controls.Add(headerPanel);
            this.Controls.Add(topPanel);
            this.Controls.Add(status);
        }


        private void OpenBtn_Click(object? sender, EventArgs e)
        {
            // Lista todos os Drives disponíveis, exceto CD/DVD
            var drives = DriveInfo.GetDrives().Where(d => d.DriveType != DriveType.CDRom).ToArray();
            
            using var driveSelectorForm = new Form
            {
                Text = "Select Drive",
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(400, 200)
            };
            
            // cria um listView para mostrar os valores dos Drives
            var listView = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            listView.Columns.Add("Nome", 50);
            listView.Columns.Add("Tipo", 80);
            listView.Columns.Add("Volume", 80);
            listView.Columns.Add("Formato", 80);
            listView.Columns.Add("Tamanho", 80);

            // Converte os bites para GB
            var unMedida = 1024 * 1024 * 1024;

            // Preenchimento do listView
            foreach (var driveInfo in drives)
            {
                var item = new ListViewItem(driveInfo.Name);
                item.SubItems.Add(driveInfo.DriveType.ToString());
                if (driveInfo.IsReady)
                {
                    item.SubItems.Add(driveInfo.VolumeLabel);
                    item.SubItems.Add(driveInfo.DriveFormat);
                    item.SubItems.Add((driveInfo.TotalSize / unMedida).ToString() + " GB");
                }
                else
                {
                    item.SubItems.Add("N/A"); item.SubItems.Add("N/A"); item.SubItems.Add("N/A");
                }
                item.Tag = driveInfo;
                listView.Items.Add(item);
            }

            var selectBtn = new Button { Text = "Select", Dock = DockStyle.Bottom };
            selectBtn.Click += (s, ev) =>
            {
                if (listView.SelectedItems.Count > 0)
                {
                    selectedDrive = listView.SelectedItems[0].Tag as DriveInfo;
                    driveSelectorForm.DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show("Selecione um drive.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            driveSelectorForm.Controls.Add(listView);
            driveSelectorForm.Controls.Add(selectBtn);

            if (driveSelectorForm.ShowDialog() == DialogResult.OK && selectedDrive != null)
            {
                data = Array.Empty<byte>();
                currentPath = selectedDrive.Name;
                currentSector = 0;
                // leitura do setor 0
                if (ReadSector(0))
                {
                    statusLabel.Text = $"{selectedDrive.Name} - Setor {currentSector} (Offset 0x{currentOffset:X})";
                    nextSectorBtn.Enabled = true;
                    prevSectorBtn.Enabled = false;
                    saveBtn.Enabled = true;
                }
                else
                {
                    CloseBtn_Click(null, EventArgs.Empty);
                }
            }
        }

        // fecha o disco atual
        private void CloseBtn_Click(object? sender, EventArgs e)
        {
            var ans = MessageBox.Show("Fechar disco atual?", "Fechar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ans != DialogResult.Yes) return;
            data = Array.Empty<byte>();
            currentPath = null;
            selectedDrive = null;
            grid.Rows.Clear();
            statusLabel.Text = "Nenhum disco carregado";
            prevSectorBtn.Enabled = false;
            nextSectorBtn.Enabled = false;
            saveBtn.Enabled = false;
        }

        private void SaveBtn_Click(object? sender, EventArgs e)
        {
            // verificação simples se um disco está selecionado
            if (selectedDrive != null)
            {
                // Mensagem de aviso
                var res = MessageBox.Show($"ATENÇÃO: Você está prestes a ESCREVER os dados editados no Setor {currentSector} do disco {selectedDrive.Name}.\nIsso pode corromper o sistema de arquivos ou o boot loader.\nVocê tem certeza que deseja continuar?",
                                          "CONFIRMAÇÃO DE ESCRITA DE DISCO", MessageBoxButtons.YesNo, MessageBoxIcon.Stop);
                if (res != DialogResult.Yes) return;

                WriteSector(currentSector);
                return;
            }

         
        }


        private bool ReadSector(int sectorNumber)
        {
            if (selectedDrive == null) return false;

            currentSector = sectorNumber;
            currentOffset = (long)sectorNumber * SECTOR_SIZE;

            try
            {
                // Acesso de leitura
                using var fs = new FileStream(
                    @"\\.\" + selectedDrive.Name.TrimEnd('\\'),
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // vai para o setor desejado
                fs.Seek(currentOffset, SeekOrigin.Begin);
                data = new byte[SECTOR_SIZE];
                int bytesRead = fs.Read(data, 0, SECTOR_SIZE);

                if (bytesRead != SECTOR_SIZE)
                {
                    Array.Resize(ref data, SECTOR_SIZE);
                }

                PopulateGrid();
                statusLabel.Text = $"{selectedDrive.Name} - Setor {currentSector} (Offset 0x{currentOffset:X})";

                prevSectorBtn.Enabled = currentSector > 0;
                if (selectedDrive.IsReady && selectedDrive.TotalSize > (currentOffset + SECTOR_SIZE))
                    nextSectorBtn.Enabled = true;
                else
                    nextSectorBtn.Enabled = true;

                return true;
            }
             // tratamento de exeção
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show($"Permissão negada para acessar o drive {selectedDrive.Name}. Execute como Administrador.", "Erro de Permissão", MessageBoxButtons.OK, MessageBoxIcon.Error);
                data = new byte[SECTOR_SIZE];
                PopulateGrid();
                statusLabel.Text = $"{selectedDrive.Name} - Setor {currentSector} (Leitura Falhou - ADMINISTRADOR?)";
                prevSectorBtn.Enabled = false;
                nextSectorBtn.Enabled = false;
                saveBtn.Enabled = false;
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao tentar ler o setor {sectorNumber}: {ex.Message}", "Erro de Leitura", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CloseBtn_Click(null, EventArgs.Empty);
                return false;
            }
        }

        private void WriteSector(int sectorNumber)
        {
            if (selectedDrive == null) return;

            try
            {
                // Acesso de escrita
                using var fs = new FileStream(
                    @"\\.\" + selectedDrive.Name.TrimEnd('\\'),
                    FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

                fs.Seek(currentOffset, SeekOrigin.Begin);
                // escreve os dados do setor
                fs.Write(data, 0, SECTOR_SIZE);

                MessageBox.Show($"Setor {sectorNumber} escrito com sucesso no disco {selectedDrive.Name}.", "Escrita OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                statusLabel.Text = $"{selectedDrive.Name} - Setor {currentSector} (Offset 0x{currentOffset:X}) - SALVO!";
            }
            //Tratamento de exceção
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show($"Permissão negada para escrever no drive {selectedDrive.Name}. Execute como Administrador.", "Erro de Permissão", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao tentar escrever no setor {sectorNumber}: {ex.Message}", "Erro de Escrita", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        
        // click do botão anterior
        private void PrevSectorBtn_Click(object? sender, EventArgs e)
        {
            if (currentSector > 0)
            {
                ReadSector(currentSector - 1);
            }
        }

        //click botão próximo
        private void NextSectorBtn_Click(object? sender, EventArgs e)
        {
            ReadSector(currentSector + 1);
        }

        

        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            // só permite que a edição aconteca nas colunas hexadecimais e pega o texto digitado
            if (e.RowIndex < 0 || e.ColumnIndex < 1 || e.ColumnIndex > BYTES_PER_ROW) return;
            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var s = (cell.Value ?? "").ToString()!.Trim();// garante que o valor não seja nulo
            s = string.Concat(s.Where(ch => !char.IsWhiteSpace(ch)));

            // se por acaso for apagado tudo, volta ao byte original
            if (s.Length == 0)
            {
                int idx = e.RowIndex * BYTES_PER_ROW + (e.ColumnIndex - 1);
                if (idx < data.Length) cell.Value = data[idx].ToString("X2");
                return;
            }
            // validação regex no formato hexadecimal
            if (!System.Text.RegularExpressions.Regex.IsMatch(s, "^(?:[0-9A-Fa-f]{1,2})$"))
            {
                MessageBox.Show("Entrada inválida. Use 1 ou 2 dígitos hex (0-9, A-F).", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                int idr = e.RowIndex * BYTES_PER_ROW + (e.ColumnIndex - 1);
                if (idr < data.Length) cell.Value = data[idr].ToString("X2");
                return;
            }
            // se tiver apenas um digito, adiciona um zero antes
            if (s.Length == 1) s = "0" + s;
            int val = Convert.ToInt32(s, 16); // converção de hex para numero
            int dataIndex = e.RowIndex * BYTES_PER_ROW + (e.ColumnIndex - 1);
            if (dataIndex >= data.Length)
            {
                MessageBox.Show("Offset fora do setor atual.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                cell.Value = "";
                return;
            }

            
            data[dataIndex] = (byte)val;
            cell.Value = val.ToString("X2");

            // Atualiza a representação ASCII na mesma linha
            var sb = new StringBuilder();
            int baseIdx = e.RowIndex * BYTES_PER_ROW;
            for (int i = 0; i < BYTES_PER_ROW; i++)
            {
                int idx = baseIdx + i;
                if (idx < data.Length)
                {
                    byte b = data[idx];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                else sb.Append(' ');
            }
            grid.Rows[e.RowIndex].Cells[1 + BYTES_PER_ROW].Value = sb.ToString();
            statusLabel.Text = $"Editado offset global 0x{currentOffset + dataIndex:X8} -> {val:X2}. Pressione SALVAR para escrever no disco.";
        }

        //praticidade entende que dois clicks tamber é para editar
        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 1 && e.ColumnIndex <= BYTES_PER_ROW)
                grid.BeginEdit(true);
        }

        private void PopulateGrid()
        {
            grid.Rows.Clear();
            int rows = (data.Length + BYTES_PER_ROW - 1) / BYTES_PER_ROW;
            for (int r = 0; r < rows; r++)
            {
                var rowIdx = grid.Rows.Add();
                var row = grid.Rows[rowIdx];
                long offset = currentOffset + (long)r * BYTES_PER_ROW;
                row.Cells[0].Value = string.Format("0x{0:X8}", offset);
                var sb = new StringBuilder();
                for (int c = 0; c < BYTES_PER_ROW; c++)
                {
                    int idx = r * BYTES_PER_ROW + c;
                    if (idx < data.Length)
                    {
                        row.Cells[1 + c].Value = data[idx].ToString("X2");
                        byte b = data[idx];
                        sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                    }
                    else
                    {
                        row.Cells[1 + c].Value = "";
                        sb.Append(' ');
                    }
                }
                row.Cells[1 + BYTES_PER_ROW].Value = sb.ToString();
            }
        }

        // botão para ir direto a um setor expecifico
        private void GotoBtn_Click(object? sender, EventArgs e)
        {
            if (data.Length == 0) return;
            var txt = gotoBox.Text.Trim();
            if (string.IsNullOrEmpty(txt)) return;
            try
            {
                long targetOffset;
                if (txt.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(txt, "^[0-9A-Fa-f]+$"))
                {
                    if (txt.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) txt = txt[2..];
                    targetOffset = Convert.ToInt64(txt, 16);
                }
                else
                    targetOffset = Convert.ToInt64(txt);

                if (selectedDrive != null)
                {
                    if (targetOffset < currentOffset || targetOffset >= currentOffset + SECTOR_SIZE)
                    {
                        int targetSector = (int)(targetOffset / SECTOR_SIZE);
                        ReadSector(targetSector);
                        long offsetInGrid = targetOffset - (long)targetSector * SECTOR_SIZE;
                        int row = (int)(offsetInGrid / BYTES_PER_ROW);
                        grid.ClearSelection();
                        if (row < grid.Rows.Count)
                        {
                            grid.Rows[row].Selected = true;
                            grid.FirstDisplayedScrollingRowIndex = row;
                        }
                        return;
                    }
                    else
                    {
                        long offsetInGrid = targetOffset - currentOffset;
                        int row = (int)(offsetInGrid / BYTES_PER_ROW);
                        grid.ClearSelection();
                        if (row < grid.Rows.Count)
                        {
                            grid.Rows[row].Selected = true;
                            grid.FirstDisplayedScrollingRowIndex = row;
                        }
                        return;
                    }
                }

                if (targetOffset < 0 || targetOffset >= data.Length)
                {
                    MessageBox.Show("Offset fora do arquivo.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int rowFile = (int)(targetOffset / BYTES_PER_ROW);
                grid.ClearSelection();
                grid.Rows[rowFile].Selected = true;
                grid.FirstDisplayedScrollingRowIndex = rowFile;
            }
            catch
            {
                MessageBox.Show("Formato de offset inválido.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // busca um padrão de bytes em hex se achar ele mostra
        private void FindBtn_Click(object? sender, EventArgs e)
        {
            var pattern = (findBox.Text ?? "").Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(pattern)) return;

            if (!System.Text.RegularExpressions.Regex.IsMatch(pattern, "^(?:[0-9A-Fa-f]{2})+$")) // verifica se tem apenas pares hex
            {
                MessageBox.Show("Digite pares hex válidos, ex: DEADBEEF", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            byte[] pat = new byte[pattern.Length / 2]; // converte hex para array de bytes
            for (int i = 0; i < pat.Length; i++) pat[i] = Convert.ToByte(pattern.Substring(i * 2, 2), 16);
            int idx = IndexOf(data, pat);

            if (idx >= 0)
            {
                long globalOffset = currentOffset + idx;
                int row = idx / BYTES_PER_ROW;
                grid.ClearSelection();
                grid.Rows[row].Selected = true;
                grid.FirstDisplayedScrollingRowIndex = row;
                MessageBox.Show($"Encontrado em offset global 0x{globalOffset:X} ({globalOffset})\nNo setor atual, offset 0x{idx:X}",
                                 "Encontrado", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
                MessageBox.Show("Padrão não encontrado no setor atual.", "Resultado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ajuda na busca
        private static int IndexOf(byte[] data, byte[] pat)
        {
            if (pat.Length == 0 || data.Length < pat.Length) return -1;
            for (int i = 0; i <= data.Length - pat.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < pat.Length; j++)
                    if (data[i + j] != pat[j]) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }

        // apenas uma formatação de bytes para KB,MB, GB dependendo do tamanho
        private static string HumanReadable(long bytes)
        {
            long unit = 1024;
            if (bytes < unit) return bytes + " B";
            int exp = (int)(Math.Log(bytes) / Math.Log(unit));
            string pre = "KMGTPE"[exp - 1].ToString();
            return $"{bytes / Math.Pow(unit, exp):0.0} {pre}B";
        }

        #region Designer mínimo
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Name = "Form1";
            this.ResumeLayout(false);
        }
        #endregion
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}