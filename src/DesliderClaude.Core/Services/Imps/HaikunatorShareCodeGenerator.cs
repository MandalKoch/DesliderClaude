using HaikunatorLib = Haikunator.Haikunator;

namespace DesliderClaude.Core.Services.Imps;

public sealed class HaikunatorShareCodeGenerator : IShareCodeGenerator
{
    private readonly HaikunatorLib _haikunator = new();

    public string Generate() => _haikunator.Haikunate();
}
