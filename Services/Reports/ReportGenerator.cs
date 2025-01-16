using System;
using System.Data;
using System.Data.SQLite;
using System.Threading.Tasks;
using AdminSERMAC.Models;
using AdminSERMAC.Services;
using Microsoft.Extensions.Logging;
using System.Data;


namespace AdminSERMAC.Services
{
    public class ReportGenerator
    {
        private readonly SQLiteService _sqliteService;
        private readonly ILogger<ReportGenerator> _logger;

        public ReportGenerator(SQLiteService sqliteService, ILogger<ReportGenerator> logger)
        {
            _sqliteService = sqliteService;
            _logger = logger;
        }

        public async Task<Report> GenerateVentasReport(DateTime desde, DateTime hasta)
        {
            try
            {
                using var connection = new SQLiteConnection(_sqliteService.connectionString);
                await connection.OpenAsync();

                var report = new Report { Titulo = "Reporte de Ventas" };

                // Ventas totales
                var ventasTotales = await GetVentasTotales(connection, desde, hasta);
                report.AddSection("Ventas Totales", ventasTotales);

                // Ventas por producto
                var ventasPorProducto = await GetVentasPorProducto(connection, desde, hasta);
                report.AddSection("Ventas por Producto", ventasPorProducto);

                // Ventas por cliente
                var ventasPorCliente = await GetVentasPorCliente(connection, desde, hasta);
                report.AddSection("Ventas por Cliente", ventasPorCliente);

                // Métricas adicionales
                var metricas = await GetMetricas(connection, desde, hasta);
                report.AddSection("Métricas", metricas);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando reporte de ventas");
                throw;
            }
        }

        


        private async Task<DataTable> GetVentasTotales(SQLiteConnection connection, DateTime desde, DateTime hasta)
        {
            var command = new SQLiteCommand(@"
                SELECT 
                    strftime('%Y-%m', FechaVenta) as Mes,
                    COUNT(*) as CantidadVentas,
                    SUM(Total) as MontoTotal,
                    SUM(CASE WHEN PagadoConCredito = 1 THEN Total ELSE 0 END) as VentasCredito,
                    SUM(CASE WHEN PagadoConCredito = 0 THEN Total ELSE 0 END) as VentasContado
                FROM Ventas
                WHERE date(FechaVenta) BETWEEN @desde AND @hasta
                GROUP BY strftime('%Y-%m', FechaVenta)
                ORDER BY Mes", connection);

            command.Parameters.AddWithValue("@desde", desde.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@hasta", hasta.ToString("yyyy-MM-dd"));

            var dataTable = new DataTable();
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);
            return dataTable;
        }

        public async Task<Report> GenerateComprasPorProductoReport(string periodo, string codigoProducto)
        {
            try
            {
                using var connection = new SQLiteConnection(_sqliteService.connectionString);
                await connection.OpenAsync();

                var report = new Report { Titulo = $"Reporte de Compras - Producto {codigoProducto}" };

                var command = new SQLiteCommand(@"
            SELECT 
                CASE 
                    WHEN @periodo = 'Semanal' THEN strftime('%Y-W%W', c.FechaCompra)
                    ELSE strftime('%Y-%m', c.FechaCompra)
                END as Periodo,
                d.Codigo_Producto as CodigoProducto,
                p.Nombre as NombreProducto,
                SUM(d.Cantidad) as Unidades,
                SUM(d.Kilos) as KilosTotales,
                COUNT(DISTINCT c.NumeroCompra) as NumeroCompras,
                SUM(d.Subtotal) as MontoTotal,
                AVG(d.PrecioUnitario) as PrecioPromedio,
                p.Categoria
            FROM Compras c
            INNER JOIN DetallesCompra d ON c.NumeroCompra = d.NumeroCompra
            INNER JOIN Productos p ON d.Codigo_Producto = p.Codigo
            WHERE d.Codigo_Producto = @codigoProducto
            AND date(c.FechaCompra) >= date('now', '-3 months')
            GROUP BY 
                CASE 
                    WHEN @periodo = 'Semanal' THEN strftime('%Y-W%W', c.FechaCompra)
                    ELSE strftime('%Y-%m', c.FechaCompra)
                END, 
                d.Codigo_Producto
            ORDER BY c.FechaCompra DESC", connection);

                command.Parameters.AddWithValue("@periodo", periodo);
                command.Parameters.AddWithValue("@codigoProducto", codigoProducto);

                var dataTable = new DataTable();
                using var adapter = new SQLiteDataAdapter(command);
                adapter.Fill(dataTable);

                report.AddSection("Compras por Producto", dataTable);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando reporte de compras por producto");
                throw;
            }
        }
        public async Task<Report> GenerateComprasPorCategoriaReport(string periodo, string categoria)
        {
            try
            {
                using var connection = new SQLiteConnection(_sqliteService.connectionString);
                await connection.OpenAsync();

                var report = new Report { Titulo = $"Reporte de Compras - {categoria}" };

                var command = new SQLiteCommand(@"
            SELECT 
                CASE 
                    WHEN @periodo = 'Semanal' THEN strftime('%Y-W%W', c.FechaCompra)
                    ELSE strftime('%Y-%m', c.FechaCompra)
                END as Periodo,
                SUM(d.Cantidad) as Unidades,
                SUM(d.Kilos) as KilosTotales,
                COUNT(DISTINCT c.NumeroCompra) as NumeroCompras,
                SUM(d.Subtotal) as MontoTotal,
                AVG(d.PrecioUnitario) as PrecioPromedio
            FROM Compras c
            INNER JOIN DetallesCompra d ON c.NumeroCompra = d.NumeroCompra
            INNER JOIN Productos p ON d.Codigo_Producto = p.Codigo
            WHERE p.Categoria = @categoria
            AND date(c.FechaCompra) >= date('now', '-3 months')
            GROUP BY 
                CASE 
                    WHEN @periodo = 'Semanal' THEN strftime('%Y-W%W', c.FechaCompra)
                    ELSE strftime('%Y-%m', c.FechaCompra)
                END
            ORDER BY c.FechaCompra DESC", connection);

                command.Parameters.AddWithValue("@periodo", periodo);
                command.Parameters.AddWithValue("@categoria", categoria);

                var dataTable = new DataTable();
                using var adapter = new SQLiteDataAdapter(command);
                adapter.Fill(dataTable);

                report.AddSection("Compras por Categoría", dataTable);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando reporte de compras por categoría");
                throw;
            }
        }

        public async Task<string> DiagnosticQuery()
        {
            try
            {
                using var connection = new SQLiteConnection(_sqliteService.connectionString);
                await connection.OpenAsync();

                var result = "Diagnóstico de Datos:\n\n";

                // Verificar CompraRegistros
                var cmdCompras = new SQLiteCommand(
                    "SELECT COUNT(*) as Total, MIN(FechaCompra) as Primera, MAX(FechaCompra) as Ultima FROM CompraRegistros",
                    connection);
                using (var reader = await cmdCompras.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        result += $"CompraRegistros:\n";
                        result += $"Total registros: {reader["Total"]}\n";
                        result += $"Primera compra: {reader["Primera"]}\n";
                        result += $"Última compra: {reader["Ultima"]}\n\n";
                    }
                }

                // Ver algunos registros de muestra
                cmdCompras = new SQLiteCommand(
                    "SELECT FechaCompra, Producto, Cantidad, PrecioUnitario FROM CompraRegistros LIMIT 5",
                    connection);
                using (var reader = await cmdCompras.ExecuteReaderAsync())
                {
                    result += "Últimas 5 compras:\n";
                    while (await reader.ReadAsync())
                    {
                        result += $"Fecha: {reader["FechaCompra"]}, ";
                        result += $"Producto: {reader["Producto"]}, ";
                        result += $"Cantidad: {reader["Cantidad"]}, ";
                        result += $"Precio: {reader["PrecioUnitario"]}\n";
                    }
                }

                // Verificar Productos
                var cmdProductos = new SQLiteCommand(
                    "SELECT COUNT(*) as Total FROM Productos",
                    connection);
                using (var reader = await cmdProductos.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        result += $"\nProductos:\n";
                        result += $"Total registros: {reader["Total"]}\n";
                    }
                }

                // Ver algunos productos de muestra
                cmdProductos = new SQLiteCommand(
                    "SELECT Codigo, Nombre, Categoria FROM Productos LIMIT 5",
                    connection);
                using (var reader = await cmdProductos.ExecuteReaderAsync())
                {
                    result += "Muestra de productos:\n";
                    while (await reader.ReadAsync())
                    {
                        result += $"Código: {reader["Codigo"]}, ";
                        result += $"Nombre: {reader["Nombre"]}, ";
                        result += $"Categoría: {reader["Categoria"]}\n";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error en diagnóstico: {ex.Message}";
            }
        }

        private async Task<DataTable> GetVentasPorProducto(SQLiteConnection connection, DateTime desde, DateTime hasta)
        {
            var command = new SQLiteCommand(@"
                SELECT 
                    v.CodigoProducto,
                    v.Descripcion,
                    COUNT(*) as CantidadVentas,
                    SUM(v.KilosNeto) as KilosTotales,
                    SUM(v.Total) as MontoTotal,
                    AVG(v.Total) as PromedioVenta
                FROM Ventas v
                WHERE date(v.FechaVenta) BETWEEN @desde AND @hasta
                GROUP BY v.CodigoProducto, v.Descripcion
                ORDER BY MontoTotal DESC", connection);

            command.Parameters.AddWithValue("@desde", desde.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@hasta", hasta.ToString("yyyy-MM-dd"));

            var dataTable = new DataTable();
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);
            return dataTable;
        }

        private async Task<DataTable> GetVentasPorCliente(SQLiteConnection connection, DateTime desde, DateTime hasta)
        {
            var command = new SQLiteCommand(@"
                SELECT 
                    c.RUT,
                    c.Nombre,
                    COUNT(*) as CantidadCompras,
                    SUM(v.Total) as MontoTotal,
                    SUM(CASE WHEN v.PagadoConCredito = 1 THEN v.Total ELSE 0 END) as ComprasCredito,
                    c.Deuda as DeudaActual
                FROM Ventas v
                JOIN Clientes c ON v.RUT = c.RUT
                WHERE date(v.FechaVenta) BETWEEN @desde AND @hasta
                GROUP BY c.RUT, c.Nombre
                ORDER BY MontoTotal DESC", connection);

            command.Parameters.AddWithValue("@desde", desde.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@hasta", hasta.ToString("yyyy-MM-dd"));

            var dataTable = new DataTable();
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);
            return dataTable;
        }

        private async Task<DataTable> GetMetricas(SQLiteConnection connection, DateTime desde, DateTime hasta)
        {
            var command = new SQLiteCommand(@"
                SELECT
                    (SELECT COUNT(DISTINCT RUT) FROM Ventas
                     WHERE date(FechaVenta) BETWEEN @desde AND @hasta) as ClientesUnicos,
                    
                    (SELECT COUNT(*) FROM Ventas 
                     WHERE date(FechaVenta) BETWEEN @desde AND @hasta) as TotalVentas,
                    
                    (SELECT SUM(Total) FROM Ventas 
                     WHERE date(FechaVenta) BETWEEN @desde AND @hasta) as MontoTotal,
                    
                    (SELECT AVG(Total) FROM Ventas 
                     WHERE date(FechaVenta) BETWEEN @desde AND @hasta) as PromedioVenta,
                    
                    (SELECT SUM(KilosNeto) FROM Ventas 
                     WHERE date(FechaVenta) BETWEEN @desde AND @hasta) as KilosTotales", connection);

            command.Parameters.AddWithValue("@desde", desde.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("@hasta", hasta.ToString("yyyy-MM-dd"));

            var dataTable = new DataTable();
            using var adapter = new SQLiteDataAdapter(command);
            adapter.Fill(dataTable);
            return dataTable;
        }
    }
}