namespace TOD.Platform.AspNetCore.Logging;

public interface IDomainOperationLogger
{
    void Started(string eventName, object payload);
    void Completed(string eventName, object payload);
    void Warning(string eventName, object payload);
    void Failed(string eventName, Exception exception, object payload);
}
