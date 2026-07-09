using QRCoder;

namespace Luff.Server.Features;

public static class QrCode
{
    public static string RenderSvg(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(data).GetGraphic(4);
    }
}
