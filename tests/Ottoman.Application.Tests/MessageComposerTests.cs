using FluentAssertions;
using Ottoman.Application.Notifications;
using Ottoman.Contracts;
using Ottoman.Domain;

namespace Ottoman.Application.Tests;

public class MessageComposerTests
{
    private static readonly Party Client = new("c1", "Ana Anić", "ana@x.ba");
    private static readonly Party Barber = new("e1", "Kerim Begić", "kerim@x.ba");
    private static readonly ReservationFacts Res = new(7, Client, Barber, "07.01.2030. u 10:00", "Šišanje, Brada", 40m);
    private static readonly OrderFacts Order = new(9, Client, 22.5m, "2× Vosak");

    [Fact]
    public void A_new_reservation_tells_both_the_client_and_the_barber()
    {
        var m = MessageComposer.ReservationCreated(Res);
        m.Select(x => x.UserId).Should().BeEquivalentTo("c1", "e1");
        m.Single(x => x.UserId == "c1").Body.Should().Contain("07.01.2030. u 10:00").And.Contain("Kerim Begić").And.Contain("40.00 KM");
        m.Single(x => x.UserId == "e1").Body.Should().Contain("Ana Anić");
    }

    [Fact]
    public void When_the_client_cancels_the_barber_is_told_and_the_client_gets_no_email()
    {
        var m = MessageComposer.ReservationCancelled(Res, "Client", null);
        m.Single(x => x.UserId == "e1").SendEmail.Should().BeTrue();
        m.Single(x => x.UserId == "c1").SendEmail.Should().BeFalse("they just did it themselves");
    }

    [Fact]
    public void When_the_salon_cancels_the_client_is_told_with_the_reason()
    {
        var m = MessageComposer.ReservationCancelled(Res, "Admin", "frizer je bolestan");
        m.Single(x => x.UserId == "c1").Body.Should().Contain("frizer je bolestan");
        m.Select(x => x.UserId).Should().BeEquivalentTo("c1", "e1");
    }

    [Fact]
    public void When_the_barber_cancels_only_the_client_is_told()
    {
        MessageComposer.ReservationCancelled(Res, "Employee", null).Select(x => x.UserId).Should().BeEquivalentTo("c1");
    }

    [Fact]
    public void Reschedule_mentions_old_and_new_time()
    {
        var m = MessageComposer.ReservationRescheduled(Res, "06.01.2030. u 09:00");
        m.Should().OnlyContain(x => x.Body.Contains("06.01.2030. u 09:00") && x.Body.Contains("07.01.2030. u 10:00"));
    }

    [Theory]
    [InlineData(ReminderKind.Day, "sutra")]
    [InlineData(ReminderKind.Hours, "uskoro")]
    public void Reminders_go_to_the_client_only(ReminderKind kind, string word)
    {
        var m = MessageComposer.Reminder(Res, kind).Single();
        m.UserId.Should().Be("c1");
        m.Title.Should().Contain(word);
    }

    [Fact]
    public void A_card_order_asks_to_pay_and_an_on_receipt_order_says_where()
    {
        MessageComposer.OrderPlaced(Order, "Card").First().Body.Should().Contain("čeka plaćanje");
        MessageComposer.OrderPlaced(Order, "OnReceipt").First().Body.Should().Contain("pri preuzimanju");
    }

    [Fact]
    public void New_orders_alert_the_admins_in_app_only()
    {
        var admin = MessageComposer.OrderPlaced(Order, "OnReceipt").Single(x => x.ToAdmins);
        admin.SendEmail.Should().BeFalse();
        admin.InApp.Should().BeTrue();
    }

    [Fact]
    public void Low_stock_goes_to_the_admins_and_sold_out_is_worded_differently()
    {
        var low = MessageComposer.LowStock(new ProductLowStock(1, "Vosak", 2, 5)).Single();
        low.ToAdmins.Should().BeTrue();
        low.Body.Should().Contain("ostalo 2");
        MessageComposer.LowStock(new ProductLowStock(1, "Vosak", 0, 5)).Single().Body.Should().Contain("rasprodan");
    }

    [Fact]
    public void The_existing_account_notice_goes_to_the_address_by_email_only()
    {
        var m = MessageComposer.ExistingAccountAttempt(new RegistrationAttemptedForExistingAccount("ana@x.ba", "Ana")).Single();
        m.Email.Should().Be("ana@x.ba");
        m.InApp.Should().BeFalse();
    }

    [Fact]
    public void The_reset_mail_carries_the_token()
    {
        var m = MessageComposer.PasswordReset(new PasswordResetRequested("c1", "ana@x.ba", "Ana", "TOKEN-123")).Single();
        m.Body.Should().Contain("TOKEN-123");
        m.InApp.Should().BeFalse("a password token must never sit in an inbox");
    }
}

public class EmailTemplateTests
{
    [Fact]
    public void User_text_is_escaped_so_it_cannot_inject_markup()
    {
        var html = EmailTemplate.Html("<b>Naslov</b>", "Zdravo <script>alert(1)</script> & \"dobro\"");
        html.Should().NotContain("<script>").And.NotContain("<b>Naslov</b>");
        html.Should().Contain("&lt;script&gt;").And.Contain("&lt;b&gt;Naslov&lt;/b&gt;").And.Contain("&amp;");
    }

    [Fact]
    public void Paragraphs_and_line_breaks_are_kept()
    {
        var html = EmailTemplate.Html("T", "Prvi red\nDrugi red\n\nNovi odlomak");
        html.Should().Contain("Prvi red<br>Drugi red");
        html.Split("<p ").Length.Should().Be(3); // two paragraphs
    }
}

public class AnnouncementValidatorTests
{
    private static readonly AnnouncementRequestValidator V = new();

    [Fact]
    public void A_group_announcement_is_valid() =>
        V.Validate(new AnnouncementRequest("Akcija", "Sve šišanje -10%", AnnouncementAudience.Clients)).IsValid.Should().BeTrue();

    [Fact]
    public void A_personal_message_needs_a_target() =>
        V.Validate(new AnnouncementRequest("Pozdrav", "Tekst", AnnouncementAudience.User)).IsValid.Should().BeFalse();

    [Fact]
    public void A_group_message_must_not_name_a_target() =>
        V.Validate(new AnnouncementRequest("Pozdrav", "Tekst", AnnouncementAudience.All, "u1")).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("", "Tekst")]
    [InlineData("Naslov", "")]
    public void Title_and_body_are_required(string title, string body) =>
        V.Validate(new AnnouncementRequest(title, body, AnnouncementAudience.All)).IsValid.Should().BeFalse();

    [Fact]
    public void Lengths_are_limited() =>
        V.Validate(new AnnouncementRequest(new string('a', 121), "x", AnnouncementAudience.All)).IsValid.Should().BeFalse();
}
