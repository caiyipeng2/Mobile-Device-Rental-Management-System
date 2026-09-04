namespace DeviceRental.Application.Notifications;

public sealed record NotificationPayload(
    string RecipientEmail,
    string RecipientDisplayName,
    IReadOnlyDictionary<string, string?> Values,
    Guid? RecipientUserId = null);

public sealed record RenderedNotification(
    string RecipientEmail,
    string Subject,
    string Body);

public interface INotificationTemplateRenderer
{
    RenderedNotification Render(OutboxClaim claim, NotificationPayload payload);
}

public sealed class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    public RenderedNotification Render(OutboxClaim claim, NotificationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(payload);
        var values = payload.Values;
        var recipient = Required(payload.RecipientEmail, "recipientEmail");
        var name = string.IsNullOrWhiteSpace(payload.RecipientDisplayName)
            ? "同事"
            : payload.RecipientDisplayName.Trim();

        var (subject, body) = claim.EventType switch
        {
            "ACCOUNT_EMAIL_VERIFICATION" => (
                "验证测试设备台账邮箱",
                $"{name}，\n\n请打开以下链接完成公司邮箱验证：\n{Required(values, "verificationUrl")}\n\n链接有效期为 24 小时。"),
            "ACCOUNT_PASSWORD_RESET" => (
                "重置测试设备台账密码",
                $"{name}，\n\n请打开以下链接重置密码：\n{Required(values, "resetUrl")}\n\n链接有效期为 30 分钟。"),
            "LOAN_BORROWED" => (
                "测试设备借用成功",
                $"{name}，\n\n设备：{Required(values, "deviceModel")}（{Required(values, "assetNumber")}）\n借用时间：{Required(values, "borrowedAt")}\n到期时间：{Required(values, "dueAt")}\n\n请按时归还设备。"),
            "LOAN_DUE" => (
                "测试设备借用到期提醒",
                $"{name}，\n\n设备：{Required(values, "deviceModel")}（{Required(values, "assetNumber")}）\n到期时间：{Required(values, "dueAt")}\n\n请及时归还设备或联系测试组管理员。"),
            "LOAN_ADVANCE_REMINDER" => (
                "测试设备即将到期提醒",
                $"{name}，\n\n设备：{Required(values, "deviceModel")}（{Required(values, "assetNumber")}）\n到期时间：{Required(values, "dueAt")}\n\n设备将在约 2 小时后到期，请安排归还。"),
            "LOAN_RETURNED" => (
                "测试设备已归还",
                $"{name}，\n\n设备：{Required(values, "deviceModel")}（{Required(values, "assetNumber")}）\n归还时间：{Required(values, "returnedAt")}"),
            "LOAN_FORCED_RETURN" => (
                "测试设备已被管理员归还",
                $"{name}，\n\n设备：{Required(values, "deviceModel")}（{Required(values, "assetNumber")}）\n处理原因：{Required(values, "reason")}"),
            "LOAN_EXTENDED" => (
                "测试设备借期已延长",
                $"{name}，\n\n设备：{Required(values, "deviceModel")}（{Required(values, "assetNumber")}）\n新的到期时间：{Required(values, "dueAt")}"),
            _ => throw new InvalidOperationException($"Unsupported notification event type '{claim.EventType}'."),
        };

        return new RenderedNotification(recipient, subject, body);
    }

    private static string Required(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? Required(value, key) : throw Missing(key);

    private static string Required(string? value, string key) =>
        string.IsNullOrWhiteSpace(value) ? throw Missing(key) : value.Trim();

    private static InvalidOperationException Missing(string key) =>
        new($"Notification payload is missing required value '{key}'.");
}
