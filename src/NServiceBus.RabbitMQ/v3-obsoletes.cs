#pragma warning disable 1591

namespace EasyNetQ
{
    [ObsoleteEx(Message = "Multi-host support has been removed.", RemoveInVersion = "4", TreatAsErrorFromVersion = "3")]
    public interface IClusterHostSelectionStrategy<T>
    {
    }
}
