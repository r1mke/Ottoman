using FluentAssertions;
using Ottoman.Application.Salon;

namespace Ottoman.Application.Tests;

public class MapsLinkTests
{
    [Theory]
    [InlineData("https://www.google.com/maps/place/Ferhadija/@43.8591,18.4308,17z/data=!3m1", 43.8591, 18.4308)]
    [InlineData("https://www.google.com/maps/place/X/@43.0,18.0,17z/data=!3d43.8586!4d18.4310", 43.8586, 18.4310)]
    [InlineData("https://www.google.com/maps/search/?api=1&query=43.8586,18.4310", 43.8586, 18.4310)]
    [InlineData("https://www.google.com/maps/search/?api=1&query=43.8586%2C18.4310", 43.8586, 18.4310)]
    [InlineData("https://maps.google.com/?q=-33.86,151.21", -33.86, 151.21)]
    public void Coordinates_are_found_in_the_usual_link_forms(string url, double lat, double lng)
    {
        MapsLink.TryExtractCoordinates(url, out var la, out var lo).Should().BeTrue();
        la.Should().BeApproximately(lat, 1e-9);
        lo.Should().BeApproximately(lng, 1e-9);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://www.google.com/maps/search/?api=1&query=Ferhadija+12+Sarajevo")]
    [InlineData("https://maps.app.goo.gl/abc123")]
    [InlineData("https://www.google.com/maps/@95.0,18.0,17z")]
    public void Links_without_usable_coordinates_give_nothing(string? url) =>
        MapsLink.TryExtractCoordinates(url, out _, out _).Should().BeFalse();
}
