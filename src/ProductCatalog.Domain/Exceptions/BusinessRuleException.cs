namespace ProductCatalog.Domain.Exceptions;

/// <summary>
/// Se lanza cuando los datos recibidos violan una invariante del dominio
/// (nombre vacío, precio negativo, stock inicial negativo, etc.).
/// La API la traduce a un <c>400 Bad Request</c>.
/// </summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
