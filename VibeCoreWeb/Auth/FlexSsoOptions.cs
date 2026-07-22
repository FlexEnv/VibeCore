namespace VibeCore.Auth;

public sealed class FlexSsoOptions
{
    public const string SectionName = "FlexSso";

    public bool Enabled { get; set; }
    public string Authority { get; set; } = "https://flexenv.com";
    public string? BackchannelAuthority { get; set; }
    public string AuthorizePath { get; set; } = "/preview-sso/authorize";
    public string TokenPath { get; set; } = "/preview-sso/token";
}
