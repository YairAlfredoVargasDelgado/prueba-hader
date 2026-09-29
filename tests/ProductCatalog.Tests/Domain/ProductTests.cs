using ProductCatalog.Domain.Entities;
using ProductCatalog.Domain.Exceptions;

namespace ProductCatalog.Tests.Domain;

/// <summary>
/// Pruebas de las invariantes del producto. Son el núcleo de la suite porque
/// todas las reglas de negocio viven en la entidad: si estas pasan, ninguna capa
/// superior puede introducir un producto inconsistente.
/// </summary>
public class ProductTests
{
    [Fact]
    public void Create_ConDatosValidos_AsignaLosValores()
    {
        var product = Product.Create("Teclado", "Mecánico", 150000m, 10);

        Assert.Equal("Teclado", product.Name);
        Assert.Equal("Mecánico", product.Description);
        Assert.Equal(150000m, product.Price);
        Assert.Equal(10, product.Stock);
        Assert.Equal(product.CreatedAtUtc, product.UpdatedAtUtc);
    }

    [Fact]
    public void Create_RecortaEspaciosSobrantes()
    {
        var product = Product.Create("  Teclado  ", "  Mecánico  ", 1000m, 1);

        Assert.Equal("Teclado", product.Name);
        Assert.Equal("Mecánico", product.Description);
    }

    [Fact]
    public void Create_SinDescripcion_GuardaCadenaVacia()
    {
        var product = Product.Create("Teclado", null, 1000m, 1);

        Assert.Equal(string.Empty, product.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_SinNombre_Falla(string? name)
    {
        var error = Assert.Throws<BusinessRuleException>(() => Product.Create(name!, "x", 1000m, 1));

        Assert.Contains("nombre", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_ConNombreDemasiadoLargo_Falla()
    {
        var name = new string('a', Product.MaxNameLength + 1);

        Assert.Throws<BusinessRuleException>(() => Product.Create(name, "x", 1000m, 1));
    }

    [Fact]
    public void Create_ConDescripcionDemasiadoLarga_Falla()
    {
        var description = new string('a', Product.MaxDescriptionLength + 1);

        Assert.Throws<BusinessRuleException>(() => Product.Create("Teclado", description, 1000m, 1));
    }

    [Fact]
    public void Create_ConPrecioNegativo_Falla()
    {
        Assert.Throws<BusinessRuleException>(() => Product.Create("Teclado", "x", -0.01m, 1));
    }

    [Fact]
    public void Create_ConMasDeDosDecimales_Falla()
    {
        Assert.Throws<BusinessRuleException>(() => Product.Create("Teclado", "x", 10.123m, 1));
    }

    [Fact]
    public void Create_NormalizaLaEscalaDelPrecioADosDecimales()
    {
        var product = Product.Create("Teclado", "x", 99900m, 1);

        // Se compara la representación porque 99900 y 99900.00 son iguales
        // numéricamente pero se serializan distinto en el JSON de respuesta.
        Assert.Equal("99900.00", product.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Create_ConStockInicialNegativo_Falla()
    {
        Assert.Throws<BusinessRuleException>(() => Product.Create("Teclado", "x", 1000m, -1));
    }

    [Fact]
    public void Create_ConStockInicialCero_EsValido()
    {
        var product = Product.Create("Teclado", "x", 1000m, 0);

        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void AdjustStock_ConCantidadPositiva_Suma()
    {
        var product = Product.Create("Teclado", "x", 1000m, 10);

        product.AdjustStock(5);

        Assert.Equal(15, product.Stock);
    }

    [Fact]
    public void AdjustStock_ConCantidadNegativa_Resta()
    {
        var product = Product.Create("Teclado", "x", 1000m, 10);

        product.AdjustStock(-4);

        Assert.Equal(6, product.Stock);
    }

    [Fact]
    public void AdjustStock_HastaDejarloEnCero_EsValido()
    {
        var product = Product.Create("Teclado", "x", 1000m, 10);

        product.AdjustStock(-10);

        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void AdjustStock_ConCero_Falla()
    {
        var product = Product.Create("Teclado", "x", 1000m, 10);

        Assert.Throws<BusinessRuleException>(() => product.AdjustStock(0));
    }

    [Fact]
    public void AdjustStock_QueDejariaElStockNegativo_FallaYNoModificaElProducto()
    {
        var product = Product.Create("Teclado", "x", 1000m, 10);

        var error = Assert.Throws<InsufficientStockException>(() => product.AdjustStock(-11));

        Assert.Equal(10, error.CurrentStock);
        Assert.Equal(-11, error.RequestedChange);

        // La regla clave de la prueba: el rechazo deja el producto intacto.
        Assert.Equal(10, product.Stock);
    }

    [Fact]
    public void AdjustStock_QueDesbordariaElEntero_Falla()
    {
        var product = Product.Create("Teclado", "x", 1000m, int.MaxValue);

        Assert.Throws<BusinessRuleException>(() => product.AdjustStock(1));
    }

    [Fact]
    public void AdjustStock_ActualizaLaFechaDeModificacion()
    {
        var product = Product.Create("Teclado", "x", 1000m, 10);
        var before = product.UpdatedAtUtc;

        Thread.Sleep(5);
        product.AdjustStock(1);

        Assert.True(product.UpdatedAtUtc > before);
    }

    [Fact]
    public void UpdateDetails_CambiaLosDatosPeroNoElStock()
    {
        var product = Product.Create("Teclado", "x", 1000m, 10);

        product.UpdateDetails("Teclado Pro", "Switch rojo", 2000m);

        Assert.Equal("Teclado Pro", product.Name);
        Assert.Equal("Switch rojo", product.Description);
        Assert.Equal(2000m, product.Price);
        Assert.Equal(10, product.Stock);
    }

    [Fact]
    public void UpdateDetails_ConDatosInvalidos_Falla()
    {
        var product = Product.Create("Teclado", "x", 1000m, 10);

        Assert.Throws<BusinessRuleException>(() => product.UpdateDetails("", "x", 1000m));
    }
}
