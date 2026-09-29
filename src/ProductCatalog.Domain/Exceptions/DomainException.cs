namespace ProductCatalog.Domain.Exceptions;

/// <summary>
/// Clase base de todos los errores previsibles del dominio.
/// Permite que la capa de API distinga un fallo de negocio (esperado, culpa del
/// cliente o del estado de los datos) de un fallo técnico inesperado.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
