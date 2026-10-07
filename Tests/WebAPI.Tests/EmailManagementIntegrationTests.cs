using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Domain.Entities.Academic;
using Domain.Entities.QuestionBank;
using Domain.Entities.Organization;
using Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using Xunit;

namespace WebAPI.Tests;

public sealed class EmailManagementIntegrationTests
{
    [IdentityMySqlFact]
    public async Task MatrixApproval_QueuesAtomically_AndHttpRetryDoesNotQueueAgain()
    {
        await using var fixture = await EmailFixture.CreateAsync();
        using var admin = await fixture.LoginAsync("admin");
        using var pht = await fixture.LoginAsync("phtA");
        var input = Template("APPROVAL_NOTICE");
        input.EventCode = "MATRIX_APPROVED";
        input.Subject = "Approved {{matrixName}}";
        input.Body = "{{schoolName}} {{actorName}}";
        var template = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(fixture.RootA + "/templates", input), HttpStatusCode.Created);
        await Read<EmailConfigItem>(await pht.PutAsJsonAsync(fixture.RootA + "/configuration/MATRIX_APPROVED", new SaveEmailConfigRequest {
            EmailTemplateVersionId = template.CurrentRevision.Id, IsActive = true,
            Targets = [new(null, fixture.IncludedA.Id, "INCLUDE")]
        }));
        var matrixId = await fixture.SeedSubmittedMatrixAsync();
        await fixture.VerifyMatrixQueueRollbackAsync(matrixId);
        var approved = await pht.PostAsync($"/api/matrices/{matrixId}/approve", null);
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync());
        var retry = await pht.PostAsync($"/api/matrices/{matrixId}/approve", null);
        Assert.False(retry.IsSuccessStatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, retry.StatusCode);
        var history = await Read<DirectoryPage<EmailHistoryItem>>(await pht.GetAsync(fixture.RootA + "/history?eventCode=MATRIX_APPROVED"));
        Assert.Single(history.Items);
        Assert.Equal(1, history.TotalCount);
        Assert.Equal(1, history.Items[0].RecipientCount);
        await fixture.DeliverAsync();
        Assert.Single(fixture.Sender.Messages);
    }

    [IdentityMySqlFact]
    public async Task ConfigurationDirectory_PagesFiltersAndRestoresSavedConfigurationWithinSchool()
    {
        await using var fixture = await EmailFixture.CreateAsync();
        using var admin = await fixture.LoginAsync("admin");
        using var pht = await fixture.LoginAsync("phtA");
        using var teacher = await fixture.LoginAsync("includedA");
        var root = fixture.RootA;
        await Read<JsonElement>(await admin.PostAsJsonAsync(root + "/events", new {
            code = "STAFF_NOTICE", name = "Staff notice", variableDefinitions = Array.Empty<object>()
        }), HttpStatusCode.Created);
        var template = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(root + "/templates", Template()), HttpStatusCode.Created);
        var saved = await Read<EmailConfigItem>(await pht.PutAsJsonAsync(root + "/configuration/MATRIX_ASSIGNED", new SaveEmailConfigRequest {
            EmailTemplateVersionId = template.CurrentRevision.Id, IsActive = true,
            Targets = [new(fixture.Group.Id, null, "INCLUDE"), new(null, fixture.ExcludedA.Id, "EXCLUDE")]
        }));

        var enabled = await Read<DirectoryPage<JsonElement>>(await pht.GetAsync(root + "/configurations?configurationState=ENABLED&pageSize=1"));
        Assert.Equal(1, enabled.TotalCount);
        var row = Assert.Single(enabled.Items);
        Assert.Equal("MATRIX_ASSIGNED", row.GetProperty("eventCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("eventName").GetString()));
        Assert.Equal("SYSTEM", row.GetProperty("triggerKind").GetString());
        Assert.Equal("ACTIVE", row.GetProperty("eventStatus").GetString());
        Assert.Equal(saved.Id, row.GetProperty("configId").GetUInt64());
        Assert.Equal(saved.Version, row.GetProperty("version").GetUInt32());
        Assert.Equal("ENABLED", row.GetProperty("configurationState").GetString());
        Assert.Equal(template.Template.Name, row.GetProperty("templateName").GetString());
        Assert.Equal(1u, row.GetProperty("revision").GetUInt32());
        Assert.Equal("ACTIVE", row.GetProperty("templateStatus").GetString());
        Assert.Equal(2, row.GetProperty("targetCount").GetInt32());

        var first = await Read<DirectoryPage<JsonElement>>(await pht.GetAsync(root + "/configurations?configurationState=UNCONFIGURED&pageSize=1"));
        var second = await Read<DirectoryPage<JsonElement>>(await pht.GetAsync(root + "/configurations?configurationState=UNCONFIGURED&pageSize=1&page=2"));
        Assert.Equal(4, first.TotalCount);
        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.NotEqual(Assert.Single(first.Items).GetProperty("eventCode").GetString(), Assert.Single(second.Items).GetProperty("eventCode").GetString());
        var manual = await Read<DirectoryPage<JsonElement>>(await pht.GetAsync(root + "/configurations?triggerKind=MANUAL&search=STAFF_NOTICE"));
        Assert.Equal(1, manual.TotalCount);
        Assert.Equal("UNCONFIGURED", Assert.Single(manual.Items).GetProperty("configurationState").GetString());
        var system = await Read<DirectoryPage<JsonElement>>(await pht.GetAsync(root + "/configurations?triggerKind=SYSTEM"));
        Assert.Equal(4, system.TotalCount);
        Assert.All(system.Items, item => Assert.Equal("SYSTEM", item.GetProperty("triggerKind").GetString()));
        var otherSchool = await Read<DirectoryPage<JsonElement>>(await admin.GetAsync(fixture.RootB + "/configurations?configurationState=ENABLED"));
        Assert.Empty(otherSchool.Items);
        Assert.Equal(0, otherSchool.TotalCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.GetAsync(fixture.RootB + "/configurations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync(root + "/configurations")).StatusCode);
        foreach (var query in new[] { "page=0", "pageSize=101", "configurationState=UNKNOWN", "triggerKind=UNKNOWN" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await pht.GetAsync(root + "/configurations?" + query)).StatusCode);

        await Read<EmailConfigItem>(await pht.PutAsJsonAsync(root + "/configuration/MATRIX_ASSIGNED", new SaveEmailConfigRequest {
            Version = saved.Version, EmailTemplateVersionId = template.CurrentRevision.Id, IsActive = false, Targets = saved.Targets.ToArray()
        }));
        var disabled = await Read<DirectoryPage<JsonElement>>(await pht.GetAsync(root + "/configurations?configurationState=DISABLED"));
        Assert.Equal("MATRIX_ASSIGNED", Assert.Single(disabled.Items).GetProperty("eventCode").GetString());
        Assert.Empty((await Read<DirectoryPage<JsonElement>>(await pht.GetAsync(root + "/configurations?configurationState=ENABLED"))).Items);
    }

    [IdentityMySqlFact]
    public async Task BranchScopedVicePrincipal_CannotReadOrModifySchoolWideEmailConfiguration()
    {
        await using var fixture = await EmailFixture.CreateAsync();
        await fixture.ScopeVicePrincipalToBranchAsync();
        using var pht = await fixture.LoginAsync("phtA");
        using var admin = await fixture.LoginAsync("admin");
        var template = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(fixture.RootA + "/templates", Template()), HttpStatusCode.Created);
        var request = new SaveEmailConfigRequest { EmailTemplateVersionId = template.CurrentRevision.Id, IsActive = true,
            Targets = [new(null, fixture.IncludedA.Id, "INCLUDE")] };
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.PutAsJsonAsync(fixture.RootA + "/configuration/MATRIX_ASSIGNED", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.GetAsync(fixture.RootA + "/configuration/MATRIX_ASSIGNED")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.GetAsync(fixture.RootA + "/configurations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.PostAsJsonAsync(fixture.RootA + "/recipients/preview", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.GetAsync("/api/email/schools")).StatusCode);
    }

    [Fact]
    public async Task NotificationSmtpMissingConfigurationIsAnObservableFailure()
    {
        var sender = new Application.Services.Implement.EmailService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Application.Services.Implement.EmailService>.Instance,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendNotificationEmailAsync("recipient@example.invalid", "Subject", "Body"));
        Assert.Equal("EMAIL_NOT_CONFIGURED", error.Message);
    }

    [IdentityMySqlFact]
    public async Task SchoolConfiguration_PinsRevisionsAndEnforcesRecipientScopeAndHistory()
    {
        await using var fixture = await EmailFixture.CreateAsync();
        using var admin = await fixture.LoginAsync("admin");
        using var pht = await fixture.LoginAsync("phtA");
        using var teacher = await fixture.LoginAsync("includedA");
        var root = fixture.RootA;

        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync(root + "/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.GetAsync(fixture.RootB + "/templates")).StatusCode);
        var schools = await Read<DirectoryPage<EmailSchoolItem>>(await pht.GetAsync("/api/email/schools?pageSize=1"));
        Assert.Equal(fixture.SchoolA.Id, Assert.Single(schools.Items).Id);
        Assert.False(schools.Items[0].CanManageTemplates);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync(root + "/templates?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.PostAsJsonAsync(root + "/templates", Template())).StatusCode);

        var first = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(root + "/templates", Template()), HttpStatusCode.Created);
        var otherSchool = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(fixture.RootB + "/templates", Template()), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(root + $"/templates/{otherSchool.Template.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(root + "/templates", Template())).StatusCode);
        var list = await Read<DirectoryPage<EmailTemplateItem>>(await admin.GetAsync(root + "/templates?pageSize=1&search=MATRIX_NOTICE"));
        Assert.Equal(first.Template.Id, Assert.Single(list.Items).Id);
        Assert.Equal(1, list.TotalCount);

        var configuration = new SaveEmailConfigRequest
        {
            EmailTemplateVersionId = first.CurrentRevision.Id, IsActive = true,
            Targets = [new(fixture.Group.Id, null, "INCLUDE"), new(null, fixture.IncludedA.Id, "INCLUDE"),
                new(null, fixture.ExcludedA.Id, "EXCLUDE"), new(null, fixture.ExtraA.Id, "INCLUDE")]
        };
        // IncludedA appears through both a role and an explicit include: still one delivery.
        // ExcludedA has that role too: exclusion wins. The same role at school B never leaks in.
        var preview = await Read<DirectoryPage<EmailRecipientOption>>(await pht.PostAsJsonAsync(root + "/recipients/preview?pageSize=1", configuration));
        Assert.Equal(2, preview.TotalCount);
        Assert.Single(preview.Items);
        var fullPreview = await Read<DirectoryPage<EmailRecipientOption>>(await pht.PostAsJsonAsync(root + "/recipients/preview", configuration));
        Assert.Equal(new[] { fixture.IncludedA.Id, fixture.ExtraA.Id }.Order(), fullPreview.Items.Select(i => i.Id).Order());
        var saved = await Read<EmailConfigItem>(await pht.PutAsJsonAsync(root + "/configuration/MATRIX_ASSIGNED", configuration));
        Assert.True(saved.IsActive);
        Assert.Equal(first.CurrentRevision.Id, saved.EmailTemplateVersionId);

        configuration.Version = saved.Version;
        configuration.Targets = [new(null, fixture.UserB.Id, "INCLUDE")];
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await pht.PutAsJsonAsync(root + "/configuration/MATRIX_ASSIGNED", configuration)).StatusCode);
        configuration.Targets = saved.Targets.ToArray();
        configuration.EmailTemplateVersionId = otherSchool.CurrentRevision.Id;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await pht.PutAsJsonAsync(root + "/configuration/MATRIX_ASSIGNED", configuration)).StatusCode);
        configuration.EmailTemplateVersionId = first.CurrentRevision.Id;
        configuration.Version = 0;
        Assert.Equal(HttpStatusCode.Conflict, (await pht.PutAsJsonAsync(root + "/configuration/MATRIX_ASSIGNED", configuration)).StatusCode);

        var update = Template();
        update.Version = first.Template.Version;
        update.Subject = "Phiên bản mới {{schoolName}}";
        var second = await Read<EmailTemplateDetail>(await admin.PutAsJsonAsync(root + $"/templates/{first.Template.Id}", update));
        Assert.Equal(2u, second.CurrentRevision.Revision);
        Assert.NotEqual(first.CurrentRevision.Id, second.CurrentRevision.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(root + $"/templates/{first.Template.Id}", update)).StatusCode);
        var stillPinned = await Read<EmailConfigItem>(await pht.GetAsync(root + "/configuration/MATRIX_ASSIGNED"));
        Assert.Equal(first.CurrentRevision.Id, stillPinned.EmailTemplateVersionId);
        await fixture.VerifyAssignmentQueueAsync(first.CurrentRevision.Id);
        var revisions = await Read<DirectoryPage<EmailRevisionItem>>(await pht.GetAsync(root + $"/templates/{first.Template.Id}/revisions?pageSize=1&page=2"));
        Assert.Equal(2, revisions.TotalCount);
        Assert.Equal(first.CurrentRevision.Subject, Assert.Single(revisions.Items).Subject);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(root + $"/templates/{first.Template.Id}?version={second.Template.Version}")).StatusCode);

        var inactive = await Read<EmailTemplateDetail>(await admin.PatchAsJsonAsync(root + $"/templates/{first.Template.Id}/status", new EmailStatusRequest("INACTIVE", second.Template.Version)));
        Assert.Equal("INACTIVE", inactive.Template.Status);
        configuration.Version = saved.Version;
        configuration.EmailTemplateVersionId = second.CurrentRevision.Id;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await pht.PutAsJsonAsync(root + "/configuration/MATRIX_ASSIGNED", configuration)).StatusCode);
        configuration.IsActive = false;
        Assert.False((await Read<EmailConfigItem>(await pht.PutAsJsonAsync(root + "/configuration/MATRIX_ASSIGNED", configuration))).IsActive);

        var unused = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(root + "/templates", Template("UNUSED_NOTICE")), HttpStatusCode.Created);
        Assert.True(await Read<bool>(await admin.DeleteAsync(root + $"/templates/{unused.Template.Id}?version={unused.Template.Version}")));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(root + $"/templates/{unused.Template.Id}")).StatusCode);
    }

    [IdentityMySqlFact]
    public async Task TestQueue_IsIdempotentAndTracksRealDeliveryOutcomeWithoutSendingExternalMail()
    {
        await using var fixture = await EmailFixture.CreateAsync();
        using var admin = await fixture.LoginAsync("admin");
        using var pht = await fixture.LoginAsync("phtA");
        var template = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(fixture.RootA + "/templates", Template()), HttpStatusCode.Created);
        var request = new SendTestEmailRequest(template.CurrentRevision.Id, fixture.IncludedA.Id, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.PostAsJsonAsync(fixture.RootA + "/test", request)).StatusCode);
        var queued = await Read<EmailQueueResult>(await admin.PostAsJsonAsync(fixture.RootA + "/test", request));
        var duplicate = await Read<EmailQueueResult>(await admin.PostAsJsonAsync(fixture.RootA + "/test", request));
        Assert.Equal(queued.NotificationId, duplicate.NotificationId);
        Assert.Empty(fixture.Sender.Messages);
        await fixture.DeliverAsync();
        var sent = Assert.Single(fixture.Sender.Messages);
        Assert.Equal(fixture.IncludedA.Email, sent.To);
        Assert.DoesNotContain("{{", sent.Subject);
        Assert.DoesNotContain("{{", sent.Body);
        var deliveries = await Read<DirectoryPage<EmailDeliveryItem>>(await pht.GetAsync(fixture.RootA + $"/history/{queued.NotificationId}/recipients?pageSize=1"));
        var delivery = Assert.Single(deliveries.Items);
        Assert.Equal("SENT", delivery.Status);
        Assert.NotNull(delivery.SentAt);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.GetAsync(fixture.RootB + $"/history/{queued.NotificationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(fixture.RootB + $"/history/{queued.NotificationId}")).StatusCode);

        fixture.Sender.Fail = true;
        var failure = await Read<EmailQueueResult>(await admin.PostAsJsonAsync(fixture.RootA + "/test", request with { RequestId = Guid.NewGuid() }));
        await fixture.DeliverAsync();
        var failedRows = await Read<DirectoryPage<EmailDeliveryItem>>(await pht.GetAsync(fixture.RootA + $"/history/{failure.NotificationId}/recipients"));
        var failed = Assert.Single(failedRows.Items);
        Assert.NotEqual("SENT", failed.Status);
        Assert.Null(failed.SentAt);
        Assert.Equal(1, failed.Attempts);
        Assert.False(string.IsNullOrWhiteSpace(failed.Error));
        Assert.Single(fixture.Sender.Messages);
    }

    [IdentityMySqlFact]
    public async Task SavedConfiguration_RestoresRevisionMetadata_AndCanDisableInactiveTargets()
    {
        await using var fixture = await EmailFixture.CreateAsync();
        using var admin = await fixture.LoginAsync("admin");
        var template = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(fixture.RootA + "/templates", Template()), HttpStatusCode.Created);
        var request = new SaveEmailConfigRequest { EmailTemplateVersionId = template.CurrentRevision.Id, IsActive = true,
            Targets = [new(null, fixture.IncludedA.Id, "INCLUDE")] };
        var saved = await Read<EmailConfigItem>(await admin.PutAsJsonAsync(fixture.RootA + "/configuration/MATRIX_ASSIGNED", request));
        var restored = await admin.GetFromJsonAsync<JsonElement>(fixture.RootA + "/configuration/MATRIX_ASSIGNED");
        Assert.Equal(template.Template.Name, restored.GetProperty("data").GetProperty("templateName").GetString());
        Assert.Equal(1u, restored.GetProperty("data").GetProperty("revision").GetUInt32());
        await fixture.DeactivateIncludedAsync();
        request.Version = saved.Version;
        request.IsActive = false;
        Assert.False((await Read<EmailConfigItem>(await admin.PutAsJsonAsync(fixture.RootA + "/configuration/MATRIX_ASSIGNED", request))).IsActive);
    }

    [IdentityMySqlFact]
    public async Task CustomEvents_ManualSend_ScheduleCancel_AndSchoolIsolation()
    {
        await using var fixture = await EmailFixture.CreateAsync();
        using var admin = await fixture.LoginAsync("admin");
        using var pht = await fixture.LoginAsync("phtA");
        var definition = new { code = "STAFF_MEETING", name = "Staff meeting", description = "School notice",
            version = 0, variableDefinitions = new[] { new { name = "topic", label = "Topic", type = "TEXT", required = true } } };
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.PostAsJsonAsync(fixture.RootA + "/events", definition)).StatusCode);
        var created = await Read<JsonElement>(await admin.PostAsJsonAsync(fixture.RootA + "/events", definition), HttpStatusCode.Created);
        Assert.Equal("MANUAL", created.GetProperty("triggerKind").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(fixture.RootA + "/events", definition)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(fixture.RootB + "/events/STAFF_MEETING")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.GetAsync(fixture.RootA + "/events?pageSize=101")).StatusCode);
        var input = Template("STAFF_NOTICE"); input.EventCode = "STAFF_MEETING"; input.Subject = "{{topic}}"; input.Body = "{{schoolName}}: {{topic}}";
        var template = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(fixture.RootA + "/templates", input), HttpStatusCode.Created);
        var config = new SaveEmailConfigRequest { EmailTemplateVersionId = template.CurrentRevision.Id, IsActive = true,
            Targets = [new(fixture.Group.Id, null, "INCLUDE"), new(null, fixture.ExcludedA.Id, "EXCLUDE")] };
        var saved = await Read<EmailConfigItem>(await pht.PutAsJsonAsync(fixture.RootA + "/configuration/STAFF_MEETING", config));
        var updatedEvent = await Read<JsonElement>(await admin.PutAsJsonAsync(fixture.RootA + "/events/STAFF_MEETING", new {
            definition.code, definition.name, definition.description, version = created.GetProperty("version").GetUInt32(),
            variableDefinitions = new[] { new { name = "meetingDate", label = "Meeting date", type = "DATE", required = true } } }));
        Assert.Equal(2u, updatedEvent.GetProperty("version").GetUInt32());
        // The pinned revision keeps its original topic variable after the event contract changes.
        var requestId = Guid.NewGuid();
        var request = new { eventCode = "STAFF_MEETING", configVersion = saved.Version, requestId,
            values = new Dictionary<string, string> { ["topic"] = "Team meeting" }, scheduledFor = DateTimeOffset.UtcNow.AddHours(1) };
        var queued = await Read<EmailQueueResult>(await pht.PostAsJsonAsync(fixture.RootA + "/send", request));
        Assert.Equal(queued.NotificationId, (await Read<EmailQueueResult>(await pht.PostAsJsonAsync(fixture.RootA + "/send", request))).NotificationId);
        await fixture.DeliverAsync(); Assert.Empty(fixture.Sender.Messages);
        Assert.Equal(HttpStatusCode.Conflict, (await pht.PostAsJsonAsync(fixture.RootA + "/send", new {
            request.eventCode, request.configVersion, request.requestId, values = new Dictionary<string,string> { ["topic"] = "Changed content" }, request.scheduledFor })).StatusCode);
        var detail = await Read<JsonElement>(await pht.GetAsync(fixture.RootA + $"/history/{queued.NotificationId}"));
        var version = detail.GetProperty("notification").GetProperty("version").GetUInt32();
        Assert.True(await Read<bool>(await pht.PostAsJsonAsync(fixture.RootA + $"/history/{queued.NotificationId}/cancel", new { version })));
        await fixture.MarkDueAsync(queued.NotificationId); await fixture.DeliverAsync(); Assert.Empty(fixture.Sender.Messages);
        var immediate = await Read<EmailQueueResult>(await pht.PostAsJsonAsync(fixture.RootA + "/send", new { request.eventCode, request.configVersion,
            requestId = Guid.NewGuid(), request.values }));
        await fixture.DeliverAsync();
        Assert.Equal("Team meeting", Assert.Single(fixture.Sender.Messages).Subject);
        Assert.Equal(HttpStatusCode.Conflict, (await pht.PostAsJsonAsync(fixture.RootA + $"/history/{immediate.NotificationId}/cancel", new { version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await pht.PostAsJsonAsync(fixture.RootA + "/send", new {
            request.eventCode, request.configVersion, requestId = Guid.NewGuid(), values = new Dictionary<string,string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pht.PostAsJsonAsync(fixture.RootB + "/send", request)).StatusCode);
    }

    [IdentityMySqlFact]
    public async Task ScheduledNotifications_OnlySendWhenDue_AndCancellationCannotRaceAClaim()
    {
        await using var fixture = await EmailFixture.CreateAsync();
        using var admin = await fixture.LoginAsync("admin");
        var root = fixture.RootA;
        await Read<JsonElement>(await admin.PostAsJsonAsync(root + "/events", new {
            code = "GENERAL_NOTICE", name = "General notice", description = "", variableDefinitions = Array.Empty<object>() }), HttpStatusCode.Created);
        var input = Template("GENERAL_TEMPLATE"); input.EventCode = "GENERAL_NOTICE"; input.Subject = "Notice {{schoolName}}"; input.Body = "From {{actorName}}";
        var template = await Read<EmailTemplateDetail>(await admin.PostAsJsonAsync(root + "/templates", input), HttpStatusCode.Created);
        var config = await Read<EmailConfigItem>(await admin.PutAsJsonAsync(root + "/configuration/GENERAL_NOTICE", new SaveEmailConfigRequest {
            EmailTemplateVersionId = template.CurrentRevision.Id, IsActive = true, Targets = [new(null, fixture.IncludedA.Id, "INCLUDE")] }));
        var request = new { eventCode = "GENERAL_NOTICE", configVersion = config.Version, requestId = Guid.NewGuid(),
            values = new Dictionary<string,string>(), scheduledFor = DateTimeOffset.UtcNow.AddHours(2) };
        var preview = await Read<EmailMessagePreview>(await admin.PostAsJsonAsync(root + "/send/preview", request));
        await fixture.ChangeRecipientEmailAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(root + "/send", new {
            request.eventCode, request.configVersion, request.requestId, request.values, request.scheduledFor, previewFingerprint = preview.Fingerprint })).StatusCode);
        var queued = await Read<EmailQueueResult>(await admin.PostAsJsonAsync(root + "/send", request));
        await fixture.MakeRetryDueAsync(queued.NotificationId);
        await fixture.DeliverAsync(); Assert.Empty(fixture.Sender.Messages);
        await fixture.MarkDueAsync(queued.NotificationId);
        fixture.Sender.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivery = fixture.DeliverAsync();
        try
        {
            await fixture.Sender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(root + $"/history/{queued.NotificationId}/cancel", new { version = 1 })).StatusCode);
            // A second worker cannot claim the same recipient while the first is sending.
            await fixture.DeliverAsync(); Assert.Single(fixture.Sender.Messages);
        }
        finally { fixture.Sender.Gate.TrySetResult(); await delivery; }
        var history = await Read<DirectoryPage<EmailHistoryItem>>(await admin.GetAsync(root + "/history?sendKind=MANUAL&pageSize=1"));
        Assert.Equal(1, Assert.Single(history.Items).SentCount);
        Assert.False(history.Items[0].CanCancel);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(root + "/send", new {
            request.eventCode, request.configVersion, requestId = Guid.NewGuid(), request.values, scheduledFor = DateTimeOffset.UtcNow.AddMinutes(-1) })).StatusCode);
        var inactive = await Read<EmailEventItem>(await admin.GetAsync(root + "/events/GENERAL_NOTICE"));
        await Read<EmailEventItem>(await admin.PatchAsJsonAsync(root + "/events/GENERAL_NOTICE/status", new EmailStatusRequest("INACTIVE", inactive.Version)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(root + "/send", new {
            request.eventCode, request.configVersion, requestId = Guid.NewGuid(), request.values })).StatusCode);
    }

    private static SaveEmailTemplateRequest Template(string code = "MATRIX_NOTICE") => new()
    {
        Code = code, Name = "Thông báo ma trận", EventCode = "MATRIX_ASSIGNED",
        Subject = "Nhiệm vụ tại {{schoolName}}", Body = "{{actorName}} giao {{taskName}}, hạn {{dueAt}}. {{actionUrl}}"
    };

    private static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {body}");
        var envelope = JsonSerializer.Deserialize<ApiResponse<T>>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(envelope!.Success);
        return envelope.Data!;
    }

    private sealed class CapturingSender : IEmailService
    {
        public ConcurrentQueue<(string To, string Subject, string Body)> Messages { get; } = new();
        public bool Fail { get; set; }
        public TaskCompletionSource? Gate { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Notification delivery must use its failure-observing sender method.");
        public Task SendNotificationEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new System.Net.Mail.SmtpException("Simulated SMTP failure");
            Messages.Enqueue((to, subject, body));
            Entered.TrySetResult();
            return Gate?.Task ?? Task.CompletedTask;
        }
    }

    // Same opt-in policy as IdentityManagementIntegrationTests. Only this randomly
    // named database is migrated/deleted; TELO_TEST_MYSQL's database is never used.
    private sealed class EmailFixture : IAsyncDisposable
    {
        private const string Prefix = "telo_email_test_";
        private readonly string database = Prefix + Guid.NewGuid().ToString("N");
        private ApplicationDbContext db = null!;
        private WebApplicationFactory<Program> factory = null!;
        public CapturingSender Sender { get; } = new();
        public School SchoolA { get; } = new() { Code = "A", Name = "Trường A" };
        public School SchoolB { get; } = new() { Code = "B", Name = "Trường B" };
        public Role Group { get; } = new() { Code = "NOTICE_GROUP", Name = "Nhóm nhận email" };
        public User IncludedA { get; private set; } = null!;
        public User ExcludedA { get; private set; } = null!;
        public User ExtraA { get; private set; } = null!;
        public User UserB { get; private set; } = null!;
        public string RootA => $"/api/schools/{SchoolA.Id}/emails";
        public string RootB => $"/api/schools/{SchoolB.Id}/emails";

        public static async Task<EmailFixture> CreateAsync()
        {
            var fixture = new EmailFixture();
            try { await fixture.InitializeAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task InitializeAsync()
        {
            var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TELO_TEST_MYSQL")!) { Database = database };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(connection.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).Options;
            db = new ApplicationDbContext(options);
            await db.Database.MigrateAsync();
            await db.GetService<IMigrator>().MigrateAsync("20260930132745_AddIdentityManagement");
            await db.Database.MigrateAsync();
            var branchA = new SchoolBranch { Code = "A1", Name = "Phân hiệu A", School = SchoolA };
            var branchB = new SchoolBranch { Code = "B1", Name = "Phân hiệu B", School = SchoolB };
            var admin = Account("admin", null);
            admin.UserRoles.Add(new UserRole { Role = new Role { Code = "ADMIN", Name = "Admin", IsSystem = true } });
            var pht = Account("phtA", branchA);
            pht.UserRoles.Add(new UserRole { Role = new Role { Code = "PHT", Name = "Hiệu phó", IsSystem = true } });
            IncludedA = Account("includedA", branchA);
            ExcludedA = Account("excludedA", branchA);
            ExtraA = Account("extraA", branchA);
            UserB = Account("userB", branchB);
            foreach (var user in new[] { IncludedA, ExcludedA, UserB }) user.UserRoles.Add(new UserRole { Role = Group });
            db.Users.AddRange(admin, pht, IncludedA, ExcludedA, ExtraA, UserB);
            await db.SaveChangesAsync();
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
                builder.UseSetting("Jwt:SigningKey", "email-integration-tests-signing-key-32-characters");
                builder.UseSetting("NotificationEmail:Enabled", "true");
                builder.UseSetting("EmailSettings:Email", "sender@example.invalid");
                builder.UseSetting("EmailSettings:Password", "test-placeholder-never-used");
                builder.ConfigureServices(services =>
                {
                    foreach (var worker in services.Where(d => d.ServiceType == typeof(IHostedService)
                                 && d.ImplementationType == typeof(WebAPI.EmailDeliveryWorker)).ToArray())
                        services.Remove(worker);
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton<IEmailService>(Sender);
                });
            });
        }

        public async Task ScopeVicePrincipalToBranchAsync()
        {
            var role = await db.Roles.SingleAsync(r => r.Code == "PHT");
            role.SchoolId = SchoolA.Id;
            role.SchoolBranchId = IncludedA.SchoolBranchId;
            await db.SaveChangesAsync();
        }

        public async Task<HttpClient> LoginAsync(string username)
        {
            var client = factory.CreateClient();
            client.BaseAddress = new Uri("https://localhost");
            var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "Email-test-password-123" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
            return client;
        }

        public async Task DeactivateIncludedAsync()
        {
            IncludedA.Status = "INACTIVE";
            await db.SaveChangesAsync();
        }

        public async Task ChangeRecipientEmailAsync()
        {
            IncludedA.Email = "changed@example.invalid";
            await db.SaveChangesAsync();
        }

        public async Task MakeRetryDueAsync(ulong id) => await db.NotificationRecipients.Where(r => r.NotificationId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.NextAttemptAt, DateTime.UtcNow.AddMinutes(-1)));

        public async Task MarkDueAsync(ulong id)
        {
            await db.Notifications.Where(n => n.Id == id).ExecuteUpdateAsync(s => s.SetProperty(n => n.ScheduledFor, DateTime.UtcNow.AddMinutes(-1)));
            await db.NotificationRecipients.Where(r => r.NotificationId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.NextAttemptAt, DateTime.UtcNow.AddMinutes(-1)));
        }

        public async Task DeliverAsync()
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IEmailNotificationService>().DeliverPendingAsync(CancellationToken.None);
        }

        public async Task<ulong> SeedSubmittedMatrixAsync()
        {
            var year = new AcademicYear { Name = "2026-2027", StartDate = new(2026, 8, 15), EndDate = new(2027, 5, 31) };
            year.AssignCode("APPROVAL_TEST_YEAR");
            var matrix = new ExamMatrix {
                Name = "Approval test matrix", Status = MatrixStatusCodes.Submitted, TotalScore = 10,
                CreatedAt = DateTime.UtcNow, CreatedByUserId = IncludedA.Id,
                AcademicContext = new AcademicContext {
                    AcademicYear = year, SchoolId = SchoolA.Id, SchoolBranchId = IncludedA.SchoolBranchId!.Value,
                    Textbook = new Textbook { Title = "Test book" }, Subject = new Subject { Name = "Math" }, GradeLevel = new GradeLevel { Name = "Grade 1" }
                }
            };
            db.ExamMatrices.Add(matrix);
            await db.SaveChangesAsync();
            return matrix.Id;
        }

        public async Task VerifyMatrixQueueRollbackAsync(ulong matrixId)
        {
            using var scope = factory.Services.CreateScope();
            var queueDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var transaction = await queueDb.Database.BeginTransactionAsync();
            var queue = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
            await queue.QueueMatrixAsync(matrixId, "MATRIX_APPROVED", IncludedA.Id, CancellationToken.None);
            await queue.QueueMatrixAsync(matrixId, "MATRIX_APPROVED", IncludedA.Id, CancellationToken.None);
            Assert.Equal(1, await queueDb.Notifications.CountAsync(n => n.EventCode == "MATRIX_APPROVED"));
            await transaction.RollbackAsync();
            Assert.False(await db.Notifications.AnyAsync(n => n.EventCode == "MATRIX_APPROVED"));
        }

        public async Task VerifyAssignmentQueueAsync(ulong revisionId)
        {
            var year = new AcademicYear { Name = "2026-2027", StartDate = new(2026, 8, 15), EndDate = new(2027, 5, 31) };
            year.AssignCode("EMAIL_TEST_YEAR");
            var context = new AcademicContext
            {
                AcademicYear = year, SchoolId = SchoolA.Id, SchoolBranchId = IncludedA.SchoolBranchId!.Value,
                Textbook = new Textbook { Title = "Sách test" }, Subject = new Subject { Name = "Toán" },
                GradeLevel = new GradeLevel { Name = "Lớp 1" }
            };
            db.AcademicContexts.Add(context);
            await db.SaveChangesAsync();
            var task = new WorkTask { Name = "Lập ma trận test", AcademicContextId = context.Id,
                CreatedByUserId = IncludedA.Id, AssignedToUserId = IncludedA.Id, TaskType = "MATRIX", Status = "ASSIGNED" };
            db.WorkTasks.Add(task);
            await db.SaveChangesAsync();
            var key = $"matrix-task:{task.Id}:assigned";
            // The event must roll back with the business transaction.
            using (var scope = factory.Services.CreateScope())
            {
                var queueDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await using var transaction = await queueDb.Database.BeginTransactionAsync();
                await scope.ServiceProvider.GetRequiredService<IEmailNotificationService>()
                    .QueueMatrixAssignmentAsync(task.Id, IncludedA.Id, CancellationToken.None);
                Assert.True(await queueDb.Notifications.AnyAsync(n => n.EventKey == key));
                await transaction.RollbackAsync();
            }
            Assert.False(await db.Notifications.AnyAsync(n => n.EventKey == key));
            using (var scope = factory.Services.CreateScope())
            {
                var queueDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await using var transaction = await queueDb.Database.BeginTransactionAsync();
                var queue = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
                await queue.QueueMatrixAssignmentAsync(task.Id, IncludedA.Id, CancellationToken.None);
                await queue.QueueMatrixAssignmentAsync(task.Id, IncludedA.Id, CancellationToken.None);
                await transaction.CommitAsync();
            }
            var notification = await db.Notifications.AsNoTracking().Include(n => n.Recipients).SingleAsync(n => n.EventKey == key);
            Assert.Equal(revisionId, notification.EmailTemplateVersionId);
            Assert.Equal("Nhiệm vụ tại Trường A", notification.Title);
            Assert.Contains(task.Name, notification.Content);
            Assert.Equal(new[] { IncludedA.Id, ExtraA.Id }.Order(), notification.Recipients.Select(r => r.UserId).Order());
            Assert.Empty(Sender.Messages);
        }

        public async ValueTask DisposeAsync()
        {
            if (factory != null) await factory.DisposeAsync();
            if (db == null) return;
            Assert.StartsWith(Prefix, database);
            Assert.Equal(database, db.Database.GetDbConnection().Database);
            await db.Database.EnsureDeletedAsync();
            await db.DisposeAsync();
        }

        private static User Account(string name, SchoolBranch? branch)
        {
            var user = new User { Username = name, FullName = name, Email = name + "@example.invalid", SchoolBranch = branch };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Email-test-password-123");
            return user;
        }
    }
}
