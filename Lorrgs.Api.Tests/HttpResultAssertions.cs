using Microsoft.AspNetCore.Http;

namespace Lorrgs.Api.Tests;

internal static class HttpResultAssertions
{
    internal static (int? StatusCode, object? Value) Read(IResult result)
    {
        if (result is INestedHttpResult nested)
            result = nested.Result;
        return (Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode,
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    internal static (int? StatusCode, object? Value) Status(int expected, IResult result)
    {
        var response = Read(result);
        Assert.Equal(expected, response.StatusCode);
        return response;
    }
}
