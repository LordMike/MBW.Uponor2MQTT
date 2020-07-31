namespace MBW.HassMQTT.Interfaces
{
    public interface IMqttValueContainer
    {
        string Topic { get; }
        bool Dirty { get; }

        object GetSerializedValue(bool resetDirty);
    }
}
