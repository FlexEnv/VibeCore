using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace VibeCore.Auth;

public sealed record FlexSsoTransaction(string State, string CodeVerifier, string ReturnUrl);

public sealed class FlexSsoTransactionProtector(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("VibeCore.FlexSso.Transaction.v1");

    public string Protect(FlexSsoTransaction transaction) =>
        _protector.Protect(JsonSerializer.Serialize(transaction));

    public FlexSsoTransaction Unprotect(string value) =>
        JsonSerializer.Deserialize<FlexSsoTransaction>(_protector.Unprotect(value))
        ?? throw new InvalidOperationException("The Flex SSO transaction was empty.");
}
