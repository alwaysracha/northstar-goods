using System.ComponentModel.DataAnnotations;
using EcommerceApp.Web.ViewModels;

namespace EcommerceApp.UnitTests;

public sealed class CheckoutModelTests
{
    [Fact]
    public void Address_and_contact_are_required_and_no_card_fields_exist()
    {
        var model = new CheckoutModel();
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(model.ContactEmail)));
        Assert.DoesNotContain(typeof(CheckoutModel).GetProperties(), x => x.Name.Contains("Card", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Cvv", StringComparison.OrdinalIgnoreCase));
    }
}
