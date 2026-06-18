using NeuroMentor.Api.Services;
using Xunit;

namespace NeuroMentor.Api.Tests;

public class RepositoryInfoTests
{
    [Fact]
    public void QaValidatorNameIsSet()
    {
        Assert.Equal("ADM", RepositoryInfo.QaValidator);
    }

    [Fact]
    public void RepositoryHasQaValidationNote()
    {
        Assert.Equal("QA Validator: ADM", RepositoryInfo.QaValidationNote);
    }
}
