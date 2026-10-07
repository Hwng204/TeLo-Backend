using Application.Common;
using Application.DTOs;
using Xunit;

namespace Application.Tests;

public sealed class EmailTemplateRulesTests
{
    [Theory]
    [InlineData("{{password}}")]
    [InlineData("{{matrixName}}")]
    [InlineData("{{actorName")]
    [InlineData("actorName}}")]
    [InlineData("{{actor.name}}")]
    public void AssignmentTemplateRejectsUnknownOrMalformedVariables(string body)
    {
        var request = ValidTemplate();
        request.Body = body;
        Assert.Contains("body", EmailTemplateRules.ValidateTemplate(request).Keys);
    }

    [Theory]
    [InlineData("Subject\r\nBcc: attacker@example.invalid")]
    [InlineData("Subject\nAnother header")]
    public void SubjectCannotContainHeaderLineBreaks(string subject)
    {
        var request = ValidTemplate();
        request.Subject = subject;
        Assert.Contains("subject", EmailTemplateRules.ValidateTemplate(request).Keys);
    }

    [Fact]
    public void RenderSubstitutesOnlyNamedValuesAndDoesNotInterpretInsertedContent()
    {
        const string template = "{{actorName}} giao {{taskName}} tại {{ schoolName }}";
        var rendered = EmailTemplateRules.Render(template, "MATRIX_ASSIGNED", new Dictionary<string, string>
        {
            ["actorName"] = "Cô An", ["taskName"] = "{{password}} <b>Toán</b>", ["schoolName"] = "Trường A"
        });
        Assert.Equal("Cô An giao {{password}} <b>Toán</b> tại Trường A", rendered);
    }

    [Fact]
    public void RenderMissingRequiredValueFailsRatherThanSendingUnresolvedTemplate() =>
        Assert.Throws<InvalidOperationException>(() => EmailTemplateRules.Render("{{schoolName}}", "MATRIX_ASSIGNED", new Dictionary<string, string>()));

    [Fact]
    public void ValidVietnameseTemplateAndEventSpecificVariablesAreAccepted() =>
        Assert.Empty(EmailTemplateRules.ValidateTemplate(ValidTemplate()));

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void PaginationRejectsInvalidAndOverflowingRanges(int page, int pageSize) =>
        Assert.NotEmpty(EmailTemplateRules.Query(new EmailListQuery { Page = page, PageSize = pageSize }));

    [Fact]
    public void TemplateRejectsOversizeContentAndUnsupportedEvent()
    {
        var request = ValidTemplate();
        request.Subject = new string('a', 201);
        request.Body = new string('a', 10_001);
        request.EventCode = "UNSUPPORTED_EVENT";
        var errors = EmailTemplateRules.ValidateTemplate(request);
        Assert.Contains("subject", errors.Keys);
        Assert.Contains("body", errors.Keys);
        Assert.Contains("eventCode", errors.Keys);
    }

    [Theory]
    [InlineData("schoolName", "TEXT")]
    [InlineData("topic", "SCRIPT")]
    [InlineData("1name", "TEXT")]
    public void CustomEventRejectsReservedNamesInvalidTypesAndInvalidVariableNames(string name, string type) =>
        Assert.Contains("variableDefinitions", EmailTemplateRules.ValidateEvent(new SaveEmailEventRequest {
            Code = "NOTICE", Name = "Notice", VariableDefinitions = [new(name, "Label", type)] }));

    [Theory]
    [InlineData("DATE", "2027-02-30")]
    [InlineData("URL", "javascript:alert(1)")]
    [InlineData("URL", "https://user:password@example.invalid")]
    [InlineData("NUMBER", "not-a-number")]
    [InlineData("NUMBER", "1,2")]
    [InlineData("NUMBER", "1-")]
    public void CustomValuesRejectInvalidTypes(string type, string value)
    {
        var definition = new EmailEventItem("NOTICE", "Notice", ["field"], VariableDefinitions: [new("field", "Field", type)]);
        Assert.Contains("values.field", EmailTemplateRules.ValidateValues(definition, new Dictionary<string,string> { ["field"] = value }));
    }

    [Fact]
    public void CustomTemplatesRenderDeclaredVariablesWithoutInterpretingTheirValues()
    {
        var definition = new EmailEventItem("NOTICE", "Notice", ["meeting_topic"], VariableDefinitions: [new("meeting_topic", "Topic")]);
        Assert.Equal("{{untrusted}}", EmailTemplateRules.Render("{{meeting_topic}}", definition, new Dictionary<string,string> { ["meeting_topic"] = "{{untrusted}}" }));
        Assert.Contains("code", EmailTemplateRules.ValidateEvent(new SaveEmailEventRequest { Code = new string('A',95), Name = "Notice" }));
    }

    private static SaveEmailTemplateRequest ValidTemplate() => new()
    {
        Code = "MATRIX_NOTICE", Name = "Thông báo giao ma trận", EventCode = "MATRIX_ASSIGNED",
        Subject = "Nhiệm vụ tại {{schoolName}}", Body = "{{actorName}} giao {{taskName}}, hạn {{dueAt}}. {{actionUrl}}"
    };
}
