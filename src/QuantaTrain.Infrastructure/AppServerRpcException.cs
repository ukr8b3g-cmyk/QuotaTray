namespace QuantaTrain.Infrastructure;

public sealed class AppServerRpcException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
