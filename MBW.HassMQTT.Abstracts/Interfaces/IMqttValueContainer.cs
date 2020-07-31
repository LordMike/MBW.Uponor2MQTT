namespace MBW.HassMQTT.Interfaces
{
    public interface IMqttValueContainer
    {
        string PublishTopic { get; }
        bool Dirty { get; }

        object GetSerializedValue(bool resetDirty);
    }
}
