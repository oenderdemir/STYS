namespace STYS.OdaOzellikleri;

public static class OdaOzellikVeriTipleri
{
    public const string Boolean = "boolean";
    public const string Number = "number";
    public const string Text = "text";

    public static readonly HashSet<string> All = [Boolean, Number, Text];
}
