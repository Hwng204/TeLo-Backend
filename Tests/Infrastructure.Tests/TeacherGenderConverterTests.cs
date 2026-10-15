using Domain.Entities.Identity;
using Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public sealed class TeacherGenderConverterTests
{
    [Theory]
    [InlineData(true, "NAM")]
    [InlineData(false, "NU")]
    public void GenderRoundTripsAsStudentTextValues(bool gender, string stored)
    {
        var converter = Converter();
        Assert.Equal(stored, converter.ConvertToProvider(gender));
        Assert.Equal(gender, converter.ConvertFromProvider(stored));
    }

    [Fact]
    public void UnknownStoredTextReadsAsNull() => Assert.Null(Converter().ConvertFromProvider("KHAC"));

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter Converter()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=127.0.0.1;Database=model_validation;User=test;Password=test;", new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        using var context = new ApplicationDbContext(options);
        return context.Model.FindEntityType(typeof(Teacher))!.FindProperty(nameof(Teacher.Gender))!.GetValueConverter()!;
    }
}
