namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisanslama assembly'lerinin bütünlüğünü kontrol eder.
/// Binary patch saldırılarının maliyetini artırmak için kullanılır.
/// </summary>
public interface IAssemblyIntegrityChecker
{
    bool IsIntact();
}
