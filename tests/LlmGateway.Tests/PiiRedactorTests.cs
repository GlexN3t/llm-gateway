using LlmGateway.Core.Privacy;

namespace LlmGateway.Tests;

public class PiiRedactorTests
{
    private readonly PiiRedactor _redactor = new();

    [Fact]
    public void Redacts_email_and_restore_round_trips()
    {
        var result = _redactor.Redact(["contact me at jane.doe@example.com please"]);

        Assert.Equal("contact me at [EMAIL_1] please", result.Texts[0]);
        Assert.Equal("jane.doe@example.com", result.Map["[EMAIL_1]"]);
        Assert.Equal("contact me at jane.doe@example.com please", _redactor.Restore(result.Texts[0], result.Map));
    }

    [Fact]
    public void Redacts_valid_credit_card()
    {
        var result = _redactor.Redact(["my card is 4111 1111 1111 1111 ok"]);

        Assert.Equal("my card is [CARD_1] ok", result.Texts[0]);
        Assert.Equal("4111 1111 1111 1111", result.Map["[CARD_1]"]);
    }

    [Fact]
    public void Leaves_invalid_card_untouched()
    {
        var result = _redactor.Redact(["number 4111 1111 1111 1112 here"]);

        Assert.Contains("4111 1111 1111 1112", result.Texts[0]);
        Assert.Empty(result.Map);
    }

    [Fact]
    public void Redacts_valid_iban_only()
    {
        var valid = _redactor.Redact(["pay to GB82WEST12345698765432 now"]);
        var invalid = _redactor.Redact(["reference GB82WEST12345698765433 here"]);

        Assert.Equal("pay to [IBAN_1] now", valid.Texts[0]);
        Assert.Contains("GB82WEST12345698765433", invalid.Texts[0]);
    }

    [Fact]
    public void Redacts_international_phone()
    {
        var result = _redactor.Redact(["call +1 (555) 123-4567 today"]);

        Assert.Equal("call [PHONE_1] today", result.Texts[0]);
    }

    [Fact]
    public void Leaves_plain_text_and_dates_untouched()
    {
        var result = _redactor.Redact(["Meeting on 2026-10-17 at 9am about Q4 numbers"]);

        Assert.Equal("Meeting on 2026-10-17 at 9am about Q4 numbers", result.Texts[0]);
        Assert.Empty(result.Map);
    }

    [Fact]
    public void Batch_numbering_is_unique_across_messages()
    {
        var result = _redactor.Redact([
            "first a@example.com",
            "second b@example.com",
        ]);

        Assert.Equal("first [EMAIL_1]", result.Texts[0]);
        Assert.Equal("second [EMAIL_2]", result.Texts[1]);
        Assert.Equal(2, result.Map.Count);
    }
}
