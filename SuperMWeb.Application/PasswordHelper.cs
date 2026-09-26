using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperMWeb.Application
{
    public class PasswordHelper
    {
        //genera el hash al registrar usuario
        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        //verifica en el login que la contraseña ingresada coincida con el hash almacenado
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            }
            catch (Exception)
            {
                // puede salir exception si el hash no es valido, por ejemplo si se guarda en la base de datos un valor vacio, nulo o texto normal
                return false;
            }
            
        }

        public static bool EsHashBCrypt(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return false;

            return valor.Length == 60 &&
                   (valor.StartsWith("$2a$") ||
                    valor.StartsWith("$2b$") ||
                    valor.StartsWith("$2y$"));
        }
    }
}
