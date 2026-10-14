namespace Apipj.Tests;

public class CnpjTests
{
    [Theory]
    [InlineData("12.ABC.345/01DE-35", true)]
    [InlineData("12ABC34501DE35", true)]
    [InlineData("12.abc.345/01de-35", true)]
    [InlineData("12.ABC.345/01DE-36", false)]
    [InlineData("00.000.000/0000-00", false)]
    [InlineData("00000000000000", false)]
    [InlineData("11.378.117/0001-20", true)]
    [InlineData("11378117000121", false)]
    [InlineData("00000000000191", true)]
    [InlineData("191", true)]               // perdeu os zeros à esquerda
    [InlineData("12ABC34501DE3A", false)]   // DV com letra
    [InlineData("12ABC34501D#35", false)]   // caractere inválido
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("123456789012345", false)]
    public void Valido(string? entrada, bool esperado) => Assert.Equal(esperado, Cnpj.Valido(entrada));

    [Theory]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
    [InlineData(" 11.378.117/0001-20 ", "11378117000120")]
    [InlineData("191", "00000000000191")]
    [InlineData("6074694800011", "06074694800011")]
    [InlineData("12ABC", "12ABC")]          // alfanumérico curto: não completa
    [InlineData(null, "")]
    public void Normalizar(string? entrada, string esperado) => Assert.Equal(esperado, Cnpj.Normalizar(entrada));

    [Theory]
    [InlineData("11378117000120", "11.378.117/0001-20")]
    [InlineData("12abc34501de35", "12.ABC.345/01DE-35")]
    [InlineData("191", "00.000.000/0001-91")]
    [InlineData("12ABC", "12ABC")]
    public void Formatar(string entrada, string esperado) => Assert.Equal(esperado, Cnpj.Formatar(entrada));

    [Fact]
    public void CalcularDv_alfanumerico_e_numerico()
    {
        Assert.Equal("35", Cnpj.CalcularDv("12ABC34501DE"));
        Assert.Equal("91", Cnpj.CalcularDv("000000000001"));
        Assert.Throws<ArgumentException>(() => Cnpj.CalcularDv("12ABC"));
    }

    [Fact]
    public void ValidarLocal_mesmo_formato_da_api()
    {
        var v = Cnpj.ValidarLocal("12.abc.345/01de-35");
        Assert.True(v.Valido);
        Assert.True(v.Alfanumerico);
        Assert.Equal("12ABC34501DE35", v.Cnpj);
        Assert.Equal("12.ABC.345/01DE-35", v.Formatado);
        Assert.Null(v.Erro);

        var r = Cnpj.ValidarLocal("12ABC34501DE36");
        Assert.False(r.Valido);
        Assert.Null(r.Formatado);
        Assert.Equal("Dígitos verificadores inválidos", r.Erro);

        Assert.Equal("CNPJ zerado é inválido", Cnpj.ValidarLocal("0").Erro);
        Assert.Equal("CNPJ vazio", Cnpj.ValidarLocal("  ").Erro);
        Assert.False(Cnpj.ValidarLocal("11378117000120").Alfanumerico);
    }
}
