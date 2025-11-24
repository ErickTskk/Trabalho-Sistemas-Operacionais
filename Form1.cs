using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing; // Necessário para cores e fontes

namespace DiskEditor
{
    public partial class Form1 : Form
    {
        const int BYTES_PER_ROW = 16;
        const int SECTOR_SIZE = 512; // Tamanho comum de um setor

        byte[] data = Array.Empty<byte>();
        string? currentPath = null;
        long currentOffset = 0; // Offset atual no drive (em bytes)
        int currentSector = 0; // Setor atual sendo exibido
        DriveInfo? selectedDrive = null;

        DataGridView grid = new DataGridView();
        StatusStrip status = new StatusStrip();
        ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();

        TextBox gotoBox = new TextBox();
        Button gotoBtn = new Button();
        TextBox findBox = new TextBox();
        Button findBtn = new Button();

        // Botões de navegação por setor (novo)
        Button prevSectorBtn = new Button();
        Button nextSectorBtn = new Button();

        public Form1()
        {
            InitializeComponent();
            BuildUI();
            // Ao iniciar, o botão "Open" será o ponto de entrada para selecionar o drive.
            // A lógica de listagem de drives será movida para o OpenBtn_Click.
        }

        private void BuildUI()
        {
            this.Text = "Disk Editor";
            this.Width = 1100;
            this.Height = 700;

            // Painel de botões (Topo)
            var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
            var openBtn = new Button { Text = "Open", AutoSize = true };
            var saveBtn = new Button { Text = "Save", AutoSize = true, Enabled = false }; // Desabilitado para leitura de disco
            var closeBtn = new Button { Text = "Close", AutoSize = true };

            openBtn.Click += OpenBtn_Click;
            saveBtn.Click += SaveBtn_Click; // Mantido para possível expansão futura (edição do setor)
            closeBtn.Click += CloseBtn_Click;

            topPanel.Controls.Add(openBtn);
            topPanel.Controls.Add(saveBtn);
            topPanel.Controls.Add(closeBtn);

            // --- Novos botões de navegação ---
            prevSectorBtn.Text = "<< Sector";
            prevSectorBtn.AutoSize = true;
            prevSectorBtn.Enabled = false;
            prevSectorBtn.Click += PrevSectorBtn_Click;

            nextSectorBtn.Text = "Sector >>";
            nextSectorBtn.AutoSize = true;
            nextSectorBtn.Enabled = false;
            nextSectorBtn.Click += NextSectorBtn_Click;

            topPanel.Controls.Add(prevSectorBtn);
            topPanel.Controls.Add(nextSectorBtn);
            // ----------------------------------

            topPanel.Controls.Add(new Label { Text = "Offset (hex ou dec):", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            gotoBox.Width = 120;
            gotoBtn.Text = "Go";
            gotoBtn.AutoSize = true;
            gotoBtn.Click += GotoBtn_Click;
            topPanel.Controls.Add(gotoBox);
            topPanel.Controls.Add(gotoBtn);

            topPanel.Controls.Add(new Label { Text = "Buscar (ex: DEADBEEF):", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
            findBox.Width = 140;
            findBtn.Text = "Find";
            findBtn.AutoSize = true;
            findBtn.Click += FindBtn_Click;
            topPanel.Controls.Add(findBox);
            topPanel.Controls.Add(findBtn);

            // === Cabeçalho Fixo ===
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

            // === GRID ===
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.DefaultCellStyle.Font = new Font("Consolas", 10);
            grid.ColumnHeadersVisible = false;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Offset", HeaderText = "Offset", ReadOnly = true, Width = 120 });
            for (int i = 0; i < BYTES_PER_ROW; i++)
            {
                var c = new DataGridViewTextBoxColumn { Name = "H" + i, HeaderText = i.ToString("X2"), Width = 40 };
                grid.Columns.Add(c);
            }
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ASCII", HeaderText = "ASCII", ReadOnly = true, Width = 250 });

            grid.CellEndEdit += Grid_CellEndEdit;
            grid.CellDoubleClick += Grid_CellDoubleClick;

            // === Status bar ===
            status.Items.Add(statusLabel);
            statusLabel.Text = "Nenhum arquivo/disco carregado";

            // === Ordem de adição corrigida ===
            this.Controls.Add(grid);
            this.Controls.Add(headerPanel);
            this.Controls.Add(topPanel);
            this.Controls.Add(status);
        }

        // =========================================================================
        // === MÉTODOS DE MANIPULAÇÃO DE DISCO (Substituindo OpenBtn_Click original) ===
        // =========================================================================

        private void OpenBtn_Click(object? sender, EventArgs e)
        {
            // 1. Apresentar a lista de drives
            var drives = DriveInfo.GetDrives().Where(d => d.DriveType != DriveType.CDRom).ToArray();
            if (drives.Length == 0)
            {
                MessageBox.Show("Nenhum drive de disco encontrado.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Cria um Form de seleção de drives simples
            using var driveSelectorForm = new Form
            {
                Text = "Select Drive",
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(400, 200)
            };

            var listView = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
            listView.Columns.Add("Nome", 50);
            listView.Columns.Add("Tipo", 80);
            listView.Columns.Add("Volume", 80);
            listView.Columns.Add("Formato", 80);
            listView.Columns.Add("Tamanho", 80);

            var unMedida = 1024 * 1024 * 1024;
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
                    item.SubItems.Add("N/A");
                    item.SubItems.Add("N/A");
                    item.SubItems.Add("N/A");
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
                // Limpa o estado anterior
                data = Array.Empty<byte>();
                currentPath = selectedDrive.Name;
                currentSector = 0; // Começa sempre no setor 0

                // 2. Tenta ler o primeiro setor (Setor 0)
                if (ReadSector(0))
                {
                    statusLabel.Text = $"{selectedDrive.Name} - Setor {currentSector} (Offset 0x{currentOffset:X})";
                    nextSectorBtn.Enabled = true; // Habilita navegação
                    prevSectorBtn.Enabled = false; // Desabilita o anterior (estamos no 0)
                }
                else
                {
                    CloseBtn_Click(null, EventArgs.Empty);
                }
            }
        }

        // Simula a leitura do setor (Substitui File.ReadAllBytes)
        private bool ReadSector(int sectorNumber)
        {
            if (selectedDrive == null) return false;

            currentSector = sectorNumber;
            currentOffset = (long)sectorNumber * SECTOR_SIZE;

            // Simulação: Na leitura de um disco real, você precisaria de acesso de baixo nível
            // (Windows API ou P/Invoke, o que está fora do escopo do System.IO padrão).
            // Para fins de demonstração e para manter o código compilável, vamos *simular*
            // o conteúdo do setor com 512 bytes de dados pseudo-aleatórios ou zeros.
            // Para ler um disco fisicamente, em C#, você precisaria de privilégios e métodos
            // não triviais como CreateFile com o flag FILE_FLAG_NO_BUFFERING e FILE_SHARE_READ.

            try
            {
                // Tenta abrir o handle do drive (REQUER EXECUÇÃO COMO ADMINISTRADOR!)
                using var fs = new FileStream(
                    @"\\.\" + selectedDrive.Name.TrimEnd('\\'),
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                fs.Seek(currentOffset, SeekOrigin.Begin);
                data = new byte[SECTOR_SIZE];
                int bytesRead = fs.Read(data, 0, SECTOR_SIZE);

                if (bytesRead != SECTOR_SIZE)
                {
                    // Se não leu o setor inteiro, preenche o resto com zeros
                    Array.Resize(ref data, SECTOR_SIZE);
                }

                // Populaciona o grid com o conteúdo do setor
                PopulateGrid();
                statusLabel.Text = $"{selectedDrive.Name} - Setor {currentSector} (Offset 0x{currentOffset:X})";

                // Atualiza o estado dos botões de navegação
                prevSectorBtn.Enabled = currentSector > 0;
                // No contexto de leitura de disco, não há um "fim" definido facilmente,
                // então podemos manter o Next ativado ou limitar se soubermos o tamanho exato.
                // Aqui, mantemos simples: se o drive for local e tiver espaço > offset
                if (selectedDrive.IsReady && selectedDrive.TotalSize > (currentOffset + SECTOR_SIZE))
                {
                    nextSectorBtn.Enabled = true;
                }
                else
                {
                    // Simplesmente deixamos avançar para fins de demonstração
                    nextSectorBtn.Enabled = true;
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show($"Permissão negada para acessar o drive {selectedDrive.Name}. Execute como Administrador.", "Erro de Permissão", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Gera 512 bytes de '00' para fins de visualização do editor, se falhar a leitura real.
                data = new byte[SECTOR_SIZE];
                PopulateGrid();
                statusLabel.Text = $"{selectedDrive.Name} - Setor {currentSector} (Leitura Falhou - ADMINISTRADOR?)";
                prevSectorBtn.Enabled = false;
                nextSectorBtn.Enabled = false;
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao tentar ler o setor {sectorNumber}: {ex.Message}", "Erro de Leitura", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CloseBtn_Click(null, EventArgs.Empty);
                return false;
            }
        }

        private void PrevSectorBtn_Click(object? sender, EventArgs e)
        {
            if (currentSector > 0)
            {
                ReadSector(currentSector - 1);
            }
        }

        private void NextSectorBtn_Click(object? sender, EventArgs e)
        {
            // Nota: Para leitura de disco físico, é comum não saber o tamanho exato,
            // então avançamos até que a leitura falhe ou encontre um final lógico.
            // Aqui, simplesmente avançamos.
            ReadSector(currentSector + 1);
        }

        // =========================================================================
        // === MÉTODOS EXISTENTES DO DiskEditor (Adaptados) ===
        // =========================================================================

        private void CloseBtn_Click(object? sender, EventArgs e)
        {
            var ans = MessageBox.Show("Fechar disco/arquivo atual?", "Fechar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (ans != DialogResult.Yes) return;
            data = Array.Empty<byte>();
            currentPath = null;
            selectedDrive = null;
            grid.Rows.Clear();
            statusLabel.Text = "Nenhum arquivo/disco carregado";
            prevSectorBtn.Enabled = false;
            nextSectorBtn.Enabled = false;
        }

        private void SaveBtn_Click(object? sender, EventArgs e)
        {
            // Implementação de salvamento de arquivo (mantida do código original)
            if (selectedDrive != null)
            {
                MessageBox.Show("Salvar diretamente em um setor de disco não está implementado (requer privilégios elevados e é perigoso).", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (currentPath == null)
            {
                MessageBox.Show("Nenhum arquivo aberto.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var res = MessageBox.Show($"Salvar alterações em {Path.GetFileName(currentPath)}?",
                                     "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes) return;

            try
            {
                File.WriteAllBytes(currentPath, data);
                MessageBox.Show("Arquivo salvo com sucesso.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                statusLabel.Text = "Salvo: " + Path.GetFileName(currentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            // Edição desabilitada para leitura de disco para evitar corrupção
            if (selectedDrive != null)
            {
                int idr = e.RowIndex * BYTES_PER_ROW + (e.ColumnIndex - 1);
                if (idr < data.Length) grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = data[idr].ToString("X2");
                return;
            }

            // ... (o resto da lógica de edição de arquivo permanece para compatibilidade)
            if (e.RowIndex < 0 || e.ColumnIndex < 1 || e.ColumnIndex > BYTES_PER_ROW) return;
            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            var s = (cell.Value ?? "").ToString()!.Trim();
            s = string.Concat(s.Where(ch => !char.IsWhiteSpace(ch)));

            if (s.Length == 0)
            {
                int idx = e.RowIndex * BYTES_PER_ROW + (e.ColumnIndex - 1);
                if (idx < data.Length) cell.Value = data[idx].ToString("X2");
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(s, "^(?:[0-9A-Fa-f]{1,2})$"))
            {
                MessageBox.Show("Entrada inválida. Use 1 ou 2 dígitos hex (0-9, A-F).", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                int idr = e.RowIndex * BYTES_PER_ROW + (e.ColumnIndex - 1);
                if (idr < data.Length) cell.Value = data[idr].ToString("X2");
                return;
            }

            if (s.Length == 1) s = "0" + s;
            int val = Convert.ToInt32(s, 16);
            int dataIndex = e.RowIndex * BYTES_PER_ROW + (e.ColumnIndex - 1);
            if (dataIndex >= data.Length)
            {
                MessageBox.Show("Offset fora do arquivo.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                cell.Value = "";
                return;
            }

            data[dataIndex] = (byte)val;
            cell.Value = val.ToString("X2");

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
            statusLabel.Text = $"Editado offset 0x{dataIndex:X8} -> {val:X2}";
        }

        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 1 && e.ColumnIndex <= BYTES_PER_ROW)
                grid.BeginEdit(true);
        }

        private void GotoBtn_Click(object? sender, EventArgs e)
        {
            // Esta lógica foi mantida
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

                // Se estivermos no modo disco, o offset é global.
                if (selectedDrive != null)
                {
                    if (targetOffset < currentOffset || targetOffset >= currentOffset + SECTOR_SIZE)
                    {
                        // Se o offset estiver fora do setor atual, navegamos para o setor
                        int targetSector = (int)(targetOffset / SECTOR_SIZE);
                        ReadSector(targetSector);
                        // O offset real dentro do grid agora será a diferença
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
                        // O offset está no setor atual, apenas navega no grid
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

                // Lógica original para arquivo
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

        private void FindBtn_Click(object? sender, EventArgs e)
        {
            // A lógica de Find permanece a mesma, mas só busca no *buffer* (setor ou arquivo)
            var pattern = (findBox.Text ?? "").Trim().Replace(" ", "");
            if (string.IsNullOrEmpty(pattern)) return;

            if (!System.Text.RegularExpressions.Regex.IsMatch(pattern, "^(?:[0-9A-Fa-f]{2})+$"))
            {
                MessageBox.Show("Digite pares hex válidos, ex: DEADBEEF", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            byte[] pat = new byte[pattern.Length / 2];
            for (int i = 0; i < pat.Length; i++) pat[i] = Convert.ToByte(pattern.Substring(i * 2, 2), 16);
            int idx = IndexOf(data, pat); // Busca no buffer atual (setor)

            if (idx >= 0)
            {
                long globalOffset = currentOffset + idx; // Calcula o offset global
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
            // Certifique-se de que esta chamada está correta para seu ambiente .NET (Ex: ApplicationConfiguration.Initialize();)
            // Mantendo a estrutura do segundo código como base.
            if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        // P/Invoke para tentar corrigir problemas de DPI no Windows
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}