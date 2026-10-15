using FinSight.Application.Features.Products;
using FinSight.Application.Features.Products.Commands.CreateProduct;
using FinSight.Application.Features.Products.Commands.UpdateProduct;
using FinSight.Application.Common.Exceptions;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FluentValidation;

namespace FinSight.UnitTests.Application.Features.Products;

public class ProductCostHandlerTests
{
    private static ProductCostInputs Inputs() => new([new("Rice", 1, IngredientUnit.Kilogram, 12000, 1, IngredientUnit.Kilogram)], 10, 200, 0);

    [Fact]
    public async Task Preview_IsPureAndReturnsCalculatedBreakdown()
    {
        var inputs = Inputs();
        var result = await new PreviewProductCostHandler().Handle(new(inputs), CancellationToken.None);
        Assert.Equal(1400m, result.Result.EstimatedUnitCost);
        Assert.Equal(inputs.UsableOutput, result.Inputs.UsableOutput);
        // Handler has no repository dependency: preview cannot write a product.
        Assert.Empty(typeof(PreviewProductCostHandler).GetConstructors().Single().GetParameters());
    }

    [Fact]
    public async Task Create_SavesBreakdownAndCostTogetherForAuthenticatedBusiness()
    {
        var repository = new StubProductRepository();
        var user = new StubCurrentUserContext(Guid.NewGuid(), Guid.NewGuid());
        await new CreateProductHandler(repository, user).Handle(new("Meal", "Food", null, 5000, 1400) { CostInputs = Inputs() }, default);
        Assert.Equal(user.BusinessId, repository.AddedProduct!.BusinessId);
        Assert.Equal(1400m, repository.AddedProduct.UnitCost);
        Assert.Equal(12000m, repository.AddedProduct.CostBreakdown!.Result.TotalIngredientCost);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Update_MismatchedCostRejectsBeforeAnyMutationOrSave()
    {
        var user = new StubCurrentUserContext(Guid.NewGuid(), Guid.NewGuid());
        var product = new Product(Guid.NewGuid(), user.BusinessId, "Meal", "Food", null, 5000, 1000);
        var repository = new StubProductRepository { TrackedProduct = product };
        await Assert.ThrowsAsync<ValidationException>(() => new UpdateProductHandler(repository, user).Handle(
            new(product.Id, "Changed", "Food", null, 5000, 1) { CostInputs = Inputs() }, default));
        Assert.Equal("Meal", product.Name);
        Assert.Equal(1000m, product.UnitCost);
        Assert.Null(product.CostBreakdown);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Update_MissingTenantProductCannotSaveBreakdown()
    {
        var user = new StubCurrentUserContext(Guid.NewGuid(), Guid.NewGuid());
        var repository = new StubProductRepository();
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateProductHandler(repository, user).Handle(
            new(Guid.NewGuid(), "Meal", "Food", null, 5000, 1400) { CostInputs = Inputs() }, default));
        Assert.Equal(user.BusinessId, repository.RequestedBusinessId);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Validators_RejectMissingAndInvalidBreakdown_ButKeepManualCostOptional()
    {
        Assert.False((await new PreviewProductCostValidator().ValidateAsync(new PreviewProductCostQuery(null!))).IsValid);
        Assert.False((await new PreviewProductCostValidator().ValidateAsync(new PreviewProductCostQuery(Inputs() with { UsableOutput = 0 }))).IsValid);
        Assert.True((await new CreateProductValidator().ValidateAsync(new CreateProductCommand("Meal", "Food", null, 5000, 1400))).IsValid);
        Assert.False((await new CreateProductValidator().ValidateAsync(new CreateProductCommand("Meal", "Food", null, 5000, 1400)
            { CostInputs = Inputs() with { UsableOutput = 0 } })).IsValid);
    }
}
