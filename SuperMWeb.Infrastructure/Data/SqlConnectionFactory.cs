using Microsoft.Data.SqlClient;

namespace SuperMWeb.Infrastructure.Data;

// Fabrica de SqlConnection con la cadena de conexion inyectada.
// Sustituye a la antigua SuperM.Clases.Conexion (que leia
// ConfigurationManager). Aqui la cadena la trae el llamador,
// lo que permite inyectar la real desde App.config o Kestrel.
public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La cadena de conexion no puede estar vacia.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public SqlConnection Crear() => new SqlConnection(_connectionString);
}
