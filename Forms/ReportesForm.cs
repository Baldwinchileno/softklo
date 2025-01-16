using System;
using System.Windows.Forms;
using System.Drawing;
using System.Data;
using AdminSERMAC.Models;
using AdminSERMAC.Services;
using System.Data.SQLite;

namespace AdminSERMAC.Forms
{
    public partial class ReportesForm : Form
    {
        private readonly ReportGenerator _reportGenerator;
        private readonly SQLiteService _sqliteService;
        private ComboBox cmbTipoReporte;
        private ComboBox cmbPeriodo;
        private ComboBox cmbCategoria;
        private ComboBox cmbProducto;
        private Button btnGenerar;
        private DataGridView dgvResultados;
        private Button btnExportar;

        public ReportesForm(ReportGenerator reportGenerator, SQLiteService sqliteService)
        {
            _reportGenerator = reportGenerator;
            _sqliteService = sqliteService;
            InitializeComponent();
            ConfigureComponents();
            LoadProductos();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Name = "ReportesForm";
            this.Text = "Reportes de Compras";
            this.StartPosition = FormStartPosition.CenterScreen;

            this.ResumeLayout(false);
        }

        private void ConfigureComponents()
        {
            // Tipo de Reporte
            cmbTipoReporte = new ComboBox
            {
                Location = new Point(20, 20),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbTipoReporte.Items.AddRange(new object[] { "Por Categoría", "Por Producto" });
            cmbTipoReporte.SelectedIndex = 0;
            cmbTipoReporte.SelectedIndexChanged += CmbTipoReporte_SelectedIndexChanged;

            // Periodo
            cmbPeriodo = new ComboBox
            {
                Location = new Point(190, 20),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbPeriodo.Items.AddRange(new object[] { "Semanal", "Mensual" });
            cmbPeriodo.SelectedIndex = 0;

            // Categoría
            cmbCategoria = new ComboBox
            {
                Location = new Point(360, 20),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCategoria.Items.AddRange(new object[] { "Pollo", "Vacuno", "Cerdo" });
            cmbCategoria.SelectedIndex = 0;

            // Producto
            cmbProducto = new ComboBox
            {
                Location = new Point(360, 20),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };

            // Botón Generar
            btnGenerar = new Button
            {
                Text = "Generar Reporte",
                Location = new Point(530, 20),
                Size = new Size(120, 25)
            };
            btnGenerar.Click += BtnGenerar_Click;

            // DataGridView
            dgvResultados = new DataGridView
            {
                Location = new Point(20, 60),
                Size = new Size(740, 300),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true
            };

            // Botón Exportar
            btnExportar = new Button
            {
                Text = "Exportar a Excel",
                Location = new Point(20, 370),
                Size = new Size(120, 25)
            };
            btnExportar.Click += BtnExportar_Click;

            // Agregar controles al formulario
            Controls.AddRange(new Control[] {
                cmbTipoReporte,
                cmbPeriodo,
                cmbCategoria,
                cmbProducto,
                btnGenerar,
                dgvResultados,
                btnExportar
            });
        }

        private void LoadProductos()
        {
            try
            {
                using var connection = new SQLiteConnection(_sqliteService.connectionString);
                connection.Open();
                var command = new SQLiteCommand(
                    "SELECT Codigo, Nombre FROM Productos ORDER BY Nombre", connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var item = new ComboBoxItem
                    {
                        Value = reader["Codigo"].ToString(),
                        Text = $"{reader["Codigo"]} - {reader["Nombre"]}"
                    };
                    cmbProducto.Items.Add(item);
                }

                if (cmbProducto.Items.Count > 0)
                    cmbProducto.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar productos: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CmbTipoReporte_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool esPorCategoria = cmbTipoReporte.SelectedItem.ToString() == "Por Categoría";
            cmbCategoria.Visible = esPorCategoria;
            cmbProducto.Visible = !esPorCategoria;
        }

        private async void BtnGenerar_Click(object sender, EventArgs e)
        {
            try
            {
                // Primero mostrar el diagnóstico
                var diagnostico = await _reportGenerator.DiagnosticQuery();
                MessageBox.Show(diagnostico, "Diagnóstico de Datos");

                // Luego continuar con el reporte normal
                var periodo = cmbPeriodo.SelectedItem.ToString();
                var esPorCategoria = cmbTipoReporte.SelectedItem.ToString() == "Por Categoría";

                Report reporte;
                if (esPorCategoria)
                {
                    var categoria = cmbCategoria.SelectedItem.ToString();
                    reporte = await _reportGenerator.GenerateComprasPorCategoriaReport(periodo, categoria);
                }
                else
                {
                    var productoSeleccionado = (ComboBoxItem)cmbProducto.SelectedItem;
                    reporte = await _reportGenerator.GenerateComprasPorProductoReport(periodo, productoSeleccionado.Value);
                }

                dgvResultados.DataSource = reporte.Sections.First().Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el reporte: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "Excel Files|*.xlsx";
                    saveDialog.Title = "Guardar Reporte";
                    saveDialog.FileName = $"Reporte_Compras_{DateTime.Now:yyyyMMdd}";

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        var periodo = cmbPeriodo.SelectedItem.ToString();
                        var esPorCategoria = cmbTipoReporte.SelectedItem.ToString() == "Por Categoría";

                        Report reporte;
                        if (esPorCategoria)
                        {
                            var categoria = cmbCategoria.SelectedItem.ToString();
                            reporte = await _reportGenerator.GenerateComprasPorCategoriaReport(periodo, categoria);
                        }
                        else
                        {
                            var productoSeleccionado = (ComboBoxItem)cmbProducto.SelectedItem;
                            reporte = await _reportGenerator.GenerateComprasPorProductoReport(periodo, productoSeleccionado.Value);
                        }

                        await reporte.ExportToExcel(saveDialog.FileName);
                        MessageBox.Show("Reporte exportado exitosamente", "Éxito",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar el reporte: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class ComboBoxItem
    {
        public string Value { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}