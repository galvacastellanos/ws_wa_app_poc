using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;

namespace ws_wa_app_remesas.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PocController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PocController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class NotifCargaDto
        {
            public string agencia { get; set; }
            public string region { get; set; }
            public string tipo_tiend { get; set; }
        }

        [HttpGet("existerecurso")]
        public IActionResult ExisteRecurso()
        {
            var connectionString = "galva:" + _configuration.GetConnectionString("Default");
            return Ok(connectionString);
        }

        [HttpPost("buscar")]
        public async Task<IActionResult> Buscar([FromBody] string codAgencia)
        {
            if (string.IsNullOrWhiteSpace(codAgencia))
            {
                return BadRequest("El parámetro 'codAgencia' es requerido.");
            }

            var connStr = _configuration.GetConnectionString("DefaultConnection");
            var results = new List<NotifCargaDto>();

            using (var conn = new SqlConnection(connStr))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = @"select agencia,region,tipo_tiend from notif_carga_saldo where cod_agencia=@codAgencia";
                cmd.Parameters.Add(new SqlParameter("@codAgencia", SqlDbType.VarChar, 50) { Value = codAgencia });

                await conn.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        results.Add(new NotifCargaDto
                        {
                            agencia = reader["agencia"] as string,
                            region = reader["region"] as string,
                            tipo_tiend = reader["tipo_tiend"] as string,
                        });
                    }
                }
            }

            return Ok(results);
        }
    }
}
